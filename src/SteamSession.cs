using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;

namespace WorldBoxMultiplayer
{
    // Steam handles membership, friend invitations and relay-capable reliable messages.
    // This class must only be accessed on Unity's main thread.
    internal sealed class SteamSession
    {
        private sealed class SnapshotJob
        {
            internal byte[] Data;
            internal int Id, Index, Total, BaseCommit;
            internal string Hash;
            internal double ExpiresAt;
        }
        private readonly GameWorldSnapshot _worldSnapshot = new GameWorldSnapshot();
        private readonly Dictionary<ulong, SnapshotJob> _snapshotsOut = new Dictionary<ulong, SnapshotJob>();
        private readonly Dictionary<ulong, double> _lastSnapshotRequest = new Dictionary<ulong, double>();
        private SnapshotAssembler _snapshotIn;
        private readonly List<Wire> _pendingAfterSnapshot = new List<Wire>();
        private bool _syncRequested;
        private bool _syncHold;
        private bool _waitingForMapLoad;
        private double _loadStarted;
        private double _syncTimeout;
        private int _nextTransferId;
        internal string SyncStatus { get; private set; }
        private readonly WorldPowers _powers;
        private readonly Action<string> _log;
        private WorldfallInfo _worldfall;
        private readonly string _gameDataDirectory;
        private double _nextWorldfallProbeAt;
        private readonly Dictionary<ulong, WorldfallInfo> _peerWorldfall = new Dictionary<ulong, WorldfallInfo>();
        internal string WorldfallStatus { get { return _worldfall.State + " (" + _worldfall.Version + ")"; } }
        internal int WorldfallPeerCount { get { return _peerWorldfall.Count; } }
        private Callback<LobbyCreated_t> _created;
        private Callback<LobbyEnter_t> _entered;
        private Callback<GameLobbyJoinRequested_t> _invite;
        private Callback<SteamNetworkingMessagesSessionRequest_t> _request;
        private readonly IntPtr[] _received = new IntPtr[32];
        private readonly Dictionary<ulong, int> _lastActionSequence = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, double> _lastActionAt = new Dictionary<ulong, double>();
        private CSteamID _lobby;
        private CSteamID _owner;
        private bool _isHost;
        private bool _joined;
        private bool _pendingLobby;
        private bool _ready;
        private int _actionSequence;
        private int _commitSequence;
        private int _lastCommitSequence;
        private double _nextHelloAt;
        internal string Status { get; private set; }
        internal ulong LobbyId { get { return _joined ? _lobby.m_SteamID : 0UL; } }
        internal bool IsHost { get { return _joined && _isHost; } }
        internal bool Connected { get { return _joined; } }
        internal int MemberCount { get { return _joined ? SteamMatchmaking.GetNumLobbyMembers(_lobby) : 0; } }

        internal SteamSession(WorldPowers powers, WorldfallInfo worldfall, string gameDataDirectory, Action<string> log)
        { _powers = powers; _worldfall = worldfall; _gameDataDirectory = gameDataDirectory;
          _log = log; Status = "Steam not initialized"; SyncStatus = "World sync not requested"; }

        internal bool Initialize()
        {
            try
            {
                if (!SteamAPI.Init()) { Status = "SteamAPI.Init failed. Launch WorldBox from Steam."; return false; }
                _created = Callback<LobbyCreated_t>.Create(OnCreated);
                _entered = Callback<LobbyEnter_t>.Create(OnEntered);
                _invite = Callback<GameLobbyJoinRequested_t>.Create(OnInvite);
                _request = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnRequest);
                _ready = true;
                Status = "Steam ready: " + SteamUser.GetSteamID().m_SteamID;
                _log(Status);
                return true;
            }
            catch (Exception e)
            {
                Status = "Steam init error: " + e.Message;
                _log(Status);
                return false;
            }
        }
        internal void Pump(double realtime)
        {
            if (!_ready) return;
            SteamAPI.RunCallbacks();
            // Steam will automatically migrate lobby ownership; simulation state is not migrated.
            // Fail closed rather than silently pretending the new owner is a synchronized host.
            if (_joined && SteamMatchmaking.GetLobbyOwner(_lobby) != _owner)
            {
                Leave();
                Status = "Original host disconnected. Create a new lobby; host migration unsupported.";
                return;
            }
            if (_syncHold && realtime > _syncTimeout)
            {
                _syncHold = _syncRequested = _waitingForMapLoad = false;
                _snapshotIn = null;
                _pendingAfterSnapshot.Clear();
                SyncStatus = "World synchronization timed out. Please retry.";
            }
            if (_waitingForMapLoad && realtime - _loadStarted > 1.0 && !IsWorldLoading())
            {
                _waitingForMapLoad = false;
                _syncHold = _syncRequested = false;
                _pendingAfterSnapshot.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
                foreach (Wire action in _pendingAfterSnapshot)
                    ApplyCommit(action);
                _pendingAfterSnapshot.Clear();
                SyncStatus = "Snapshot load requested; replayed pending actions. Simulation can still diverge.";
            }
            PumpSnapshotsOut(realtime);
            if (realtime >= _nextWorldfallProbeAt)
            {
                _nextWorldfallProbeAt = realtime + 5;
                WorldfallInfo refreshed = WorldfallInfo.Detect(_gameDataDirectory);
                if (refreshed.State != _worldfall.State || refreshed.Version != _worldfall.Version)
                {
                    _worldfall = refreshed;
                    _log("Worldfall changed: " + WorldfallStatus);
                    if (_joined && _isHost)
                    {
                        SteamMatchmaking.SetLobbyData(_lobby, "wbmp_worldfall", _worldfall.State);
                        SteamMatchmaking.SetLobbyData(_lobby, "wbmp_wf_ver", _worldfall.Version);
                    }
                    if (_joined && !_isHost) _nextHelloAt = 0;
                }
            }
            if (_joined && !_isHost && realtime >= _nextHelloAt)
            {
                Send(_owner, Wire.Hello(_worldfall.State, _worldfall.Version));
                _nextHelloAt = realtime + 5;
            }
            int n = SteamNetworkingMessages.ReceiveMessagesOnChannel(0, _received, _received.Length);
            for (int i = 0; i < n; i++)
            {
                IntPtr ptr = _received[i];
                if (ptr == IntPtr.Zero) continue;
                try
                {
                    SteamNetworkingMessage_t msg = (SteamNetworkingMessage_t)Marshal.PtrToStructure(ptr, typeof(SteamNetworkingMessage_t));
                    if (msg.m_cbSize < 1 || msg.m_cbSize > SnapshotTransfer.HeaderBytes + SnapshotTransfer.ChunkSize) continue;
                    byte[] payload = new byte[msg.m_cbSize];
                    Marshal.Copy(msg.m_pData, payload, 0, payload.Length);
                    CSteamID sender = msg.m_identityPeer.GetSteamID();
                    if (SnapshotTransfer.IsChunk(payload))
                    {
                        if (IsMember(sender) && !_isHost && sender == _owner && _syncHold && _snapshotIn != null)
                            OnChunk(payload, realtime);
                        continue;
                    }
                    Wire packet;
                    if (Wire.TryParse(payload, out packet)) Handle(sender, packet, realtime);
                }
                catch (Exception ex) { _log("Invalid/failed Steam message: " + ex.Message); }
                finally
                {
                    // Native SteamNetworkingMessage_t allocations require the native Release callback.
                    SteamNetworkingMessage_t message = (SteamNetworkingMessage_t)Marshal.PtrToStructure(ptr, typeof(SteamNetworkingMessage_t));
                    message.Release();
                    _received[i] = IntPtr.Zero;
                }
            }
        }
        internal void Host()
        {
            if (!_ready || _joined || _pendingLobby) return;
            _pendingLobby = true;
            Status = "Creating Steam friends-only lobby...";
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 8);
        }
        internal void Join(ulong lobbyId)
        {
            if (!_ready || _joined || _pendingLobby || lobbyId == 0) return;
            _pendingLobby = true;
            Status = "Joining lobby " + lobbyId;
            SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
        }
        internal void InviteFriends()
        {
            if (!_ready || !_joined) { Status = "Create/join a lobby first"; return; }
            SteamFriends.ActivateGameOverlayInviteDialog(_lobby);
        }
        internal void Leave()
        {
            if (!_joined) return;
            foreach (CSteamID member in Members())
            {
                if (member == SteamUser.GetSteamID()) continue;
                var ident = new SteamNetworkingIdentity(); ident.SetSteamID(member);
                SteamNetworkingMessages.CloseSessionWithUser(ref ident);
            }
            SteamMatchmaking.LeaveLobby(_lobby);
            _joined = false; _isHost = _pendingLobby = false;
            _lobby = default(CSteamID); _owner = default(CSteamID);
            _lastActionSequence.Clear(); _lastActionAt.Clear(); _peerWorldfall.Clear();
            _lastCommitSequence = 0; _commitSequence = 0;
            _snapshotsOut.Clear(); _lastSnapshotRequest.Clear();
            _snapshotIn = null; _syncHold = _syncRequested = _waitingForMapLoad = false;
            _pendingAfterSnapshot.Clear();
            SyncStatus = "World sync not requested";
            Status = "Left lobby";
        }
        // Explicit opt-in: this replaces the client's loaded map, losing any unsaved changes.
        internal void RequestWorldSync(double realtime)
        {
            if (!_joined || _isHost) { SyncStatus = "Join a friend's lobby first"; return; }
            if (_syncHold) { SyncStatus = "Synchronization already in progress"; return; }
            _snapshotIn = null;
            _pendingAfterSnapshot.Clear();
            _syncHold = _syncRequested = true;
            _syncTimeout = realtime + 90;
            SyncStatus = "Requesting authoritative world save from host...";
            if (!Send(_owner, Wire.SyncRequest()))
            {
                _syncHold = _syncRequested = false;
                SyncStatus = "Failed to send sync request";
            }
        }
        internal void UsePower(string power, int x, int y)
        {
            if (_syncHold) { Status = "Wait for world synchronization to finish"; return; }
            if (!_joined) { Status = "Not connected. Use Ctrl+Shift+H host or Ctrl+Shift+J join."; return; }
            if (!Wire.ValidPower(power)) { Status = "Invalid power id"; return; }
            int seq = ++_actionSequence;
            if (_isHost)
            {
                bool applied = _powers.Apply(power, x, y);
                Status = applied ? "Host applied " + power : "Power rejected: " + _powers.LastError;
                if (applied) Broadcast(Wire.Commit(++_commitSequence, power, x, y));
            }
            else
            {
                bool sent = Send(_owner, Wire.Action(seq, power, x, y));
                Status = sent ? "Sent " + power + " to host" : "Failed to queue Steam packet";
            }
        }
        private void Handle(CSteamID sender, Wire packet, double realtime)
        {
            if (!_joined || !IsMember(sender)) return;
            if (packet.Kind == "HELLO")
            {
                _peerWorldfall[sender.m_SteamID] = WorldfallInfo.FromPeer(packet.WorldfallState, packet.WorldfallVersion);
                if (_isHost)
                {
                    Send(sender, Wire.Hello(_worldfall.State, _worldfall.Version));
                }
                else if (sender == _owner)
                {
                    Status = WorldfallInfo.DescribePair(_worldfall, _peerWorldfall[sender.m_SteamID]);
                }
                return;
            }
            if (packet.Kind == "SYNCREQ" && _isHost && sender != SteamUser.GetSteamID())
            {
                double last;
                if (_lastSnapshotRequest.TryGetValue(sender.m_SteamID, out last) && realtime - last < 20)
                { Send(sender, Wire.Deny("cooldown")); return; }
                if (_snapshotsOut.Count >= 2)
                { Send(sender, Wire.Deny("busy")); return; }
                _lastSnapshotRequest[sender.m_SteamID] = realtime;
                var bytes = _worldSnapshot.Capture();
                if (bytes == null)
                {
                    Status = "World snapshot failed: " + _worldSnapshot.LastError;
                    Send(sender, Wire.Deny("unavailable"));
                    return;
                }
                int id = ++_nextTransferId;
                if (id < 1) id = _nextTransferId = 1;
                _snapshotsOut[sender.m_SteamID] = new SnapshotJob
                {
                    Data = bytes, Id = id, Index = -1,
                    Total = SnapshotTransfer.Count(bytes.Length), BaseCommit = _commitSequence,
                    Hash = SnapshotTransfer.Sha256(bytes), ExpiresAt = realtime + 90
                };
                Status = "World snapshot queued for " + sender.m_SteamID + " (" + bytes.Length + " bytes)";
                return;
            }
            if (packet.Kind == "DENY" && !_isHost && sender == _owner && _syncRequested)
            {
                _syncHold = _syncRequested = false;
                _snapshotIn = null;
                _pendingAfterSnapshot.Clear();
                SyncStatus = "Host could not provide world: " + packet.Detail;
                return;
            }
            if (packet.Kind == "BEGIN" && !_isHost && sender == _owner && _syncRequested)
            {
                try
                {
                    _snapshotIn = new SnapshotAssembler(packet.TransferId, packet.Size,
                        packet.Chunks, packet.BaseCommit, packet.Sha256);
                    _lastCommitSequence = packet.BaseCommit;
                    _pendingAfterSnapshot.RemoveAll(a => a.Sequence <= packet.BaseCommit);
                    _syncTimeout = realtime + 90;
                    SyncStatus = "Receiving world snapshot: 0/" + packet.Chunks;
                }
                catch (Exception e) { SyncStatus = "Bad snapshot: " + e.Message; }
                return;
            }
            if (packet.Kind == "ACT" && _isHost && sender != SteamUser.GetSteamID())
            {
                int prev;
                if (_lastActionSequence.TryGetValue(sender.m_SteamID, out prev) && packet.Sequence <= prev) return;
                double lastTime;
                if (_lastActionAt.TryGetValue(sender.m_SteamID, out lastTime) && realtime - lastTime < 0.15) return;
                _lastActionSequence[sender.m_SteamID] = packet.Sequence;
                _lastActionAt[sender.m_SteamID] = realtime;
                bool ok = _powers.Apply(packet.Power, packet.X, packet.Y);
                Send(sender, Wire.Ack(packet.Sequence, ok));
                if (ok)
                {
                    Broadcast(Wire.Commit(++_commitSequence, packet.Power, packet.X, packet.Y));
                    Status = "Player " + sender.m_SteamID + " used " + packet.Power;
                }
                else { _log("God power rejected: " + _powers.LastError); }
                return;
            }
            if (_isHost || sender != _owner) return;
            if (packet.Kind == "ACK")
            {
                Status = packet.Accepted ? "Host accepted action #" + packet.Sequence : "Host rejected action #" + packet.Sequence;
            }
            else if (packet.Kind == "COMMIT" && packet.Sequence > _lastCommitSequence)
            {
                if (_syncHold)
                {
                    if (_pendingAfterSnapshot.Count < 4096 &&
                        !_pendingAfterSnapshot.Exists(a => a.Sequence == packet.Sequence))
                        _pendingAfterSnapshot.Add(packet);
                    else if (_pendingAfterSnapshot.Count >= 4096)
                        SyncStatus = "Commit backlog exceeded limit. World may diverge.";
                }
                else ApplyCommit(packet);
            }
        }
        private void ApplyCommit(Wire packet)
        {
            if (packet.Sequence <= _lastCommitSequence) return;
            if (packet.Sequence != _lastCommitSequence + 1)
            {
                Status = "Missing host event #" + (_lastCommitSequence + 1) + "; request world sync";
                return; // never silently apply an out-of-order commit
            }
            _lastCommitSequence = packet.Sequence;
            bool ok = _powers.Apply(packet.Power, packet.X, packet.Y);
            Status = ok ? "Host event: " + packet.Power : "Replay failed: " + _powers.LastError;
        }
        private void PumpSnapshotsOut(double realtime)
        {
            if (!_joined || !_isHost || _snapshotsOut.Count == 0) return;
            var finished = new List<ulong>();
            foreach (var entry in _snapshotsOut)
            {
                CSteamID peer = new CSteamID(entry.Key);
                SnapshotJob job = entry.Value;
                if (!IsMember(peer) || realtime > job.ExpiresAt)
                { finished.Add(entry.Key); continue; }
                if (job.Index == -1)
                {
                    if (!Send(peer, Wire.Begin(job.Id, job.Data.Length, job.Total,
                        job.BaseCommit, job.Hash))) continue;
                    job.Index = 0;
                }
                // Limit transfer to two 16 KiB messages per frame to avoid Steam queue spikes.
                for (int i = 0; i < 2 && job.Index < job.Total; i++)
                {
                    if (!Send(peer, SnapshotTransfer.Chunk(job.Id, job.Index, job.Data))) break;
                    job.Index++;
                }
                if (job.Index == job.Total) finished.Add(entry.Key);
            }
            foreach (ulong id in finished) _snapshotsOut.Remove(id);
        }
        private void OnChunk(byte[] bytes, double realtime)
        {
            if (!_snapshotIn.Add(bytes)) { SyncStatus = "Invalid snapshot chunk"; return; }
            _syncTimeout = realtime + 90;
            if (_snapshotIn.ReceivedChunks % 32 == 0 || _snapshotIn.Complete)
                SyncStatus = "Receiving world: " + _snapshotIn.ReceivedChunks + "/" + _snapshotIn.TotalChunks;
            if (!_snapshotIn.Complete) return;
            byte[] save;
            if (!_snapshotIn.TryFinish(out save))
            {
                SyncStatus = "World checksum mismatch. Loading cancelled.";
                _syncRequested = _syncHold = false; _snapshotIn = null;
                _pendingAfterSnapshot.Clear();
                return;
            }
            // Game invokes asynchronous world loading; do not claim a completed load here.
            bool begun = _worldSnapshot.Load(save);
            _snapshotIn = null;
            if (!begun)
            {
                SyncStatus = "World load rejected: " + _worldSnapshot.LastError;
                _syncRequested = _syncHold = false;
                _pendingAfterSnapshot.Clear();
                return;
            }
            _loadStarted = realtime;
            _waitingForMapLoad = true;
            SyncStatus = "Game world load started; waiting for loader...";
        }
        private static bool IsWorldLoading()
        {
            try
            {
                Type t = Type.GetType("Config, Assembly-CSharp", false);
                if (t == null) return true;
                var p = t.GetProperty("worldLoading", System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (p != null) return (bool)p.GetValue(null, null);
                var f = t.GetField("worldLoading", System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                return f == null || (bool)f.GetValue(null);
            }
            catch (Exception) { return true; }
        }
        private void Broadcast(byte[] bytes)
        {
            foreach (CSteamID member in Members())
                if (member != SteamUser.GetSteamID()) Send(member, bytes);
        }
        private bool Send(CSteamID peer, byte[] bytes)
        {
            if (!_ready || bytes == null || bytes.Length < 1 ||
                bytes.Length > (SnapshotTransfer.IsChunk(bytes) ?
                    SnapshotTransfer.HeaderBytes + SnapshotTransfer.ChunkSize : Wire.MaxPacketBytes)) return false;
            IntPtr buffer = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, buffer, bytes.Length);
                var id = new SteamNetworkingIdentity(); id.SetSteamID(peer);
                EResult result = SteamNetworkingMessages.SendMessageToUser(
                    ref id, buffer, (uint)bytes.Length, Constants.k_nSteamNetworkingSend_Reliable, 0);
                return result == EResult.k_EResultOK;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        private bool IsMember(CSteamID id)
        {
            foreach (CSteamID peer in Members()) if (peer == id) return true;
            return false;
        }
        private IEnumerable<CSteamID> Members()
        {
            if (!_joined) yield break;
            int total = Math.Min(8, SteamMatchmaking.GetNumLobbyMembers(_lobby));
            for (int i = 0; i < total; i++) yield return SteamMatchmaking.GetLobbyMemberByIndex(_lobby, i);
        }
        private void OnCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            { _pendingLobby = false; Status = "Steam lobby create failed: " + callback.m_eResult; return; }
            _lobby = new CSteamID(callback.m_ulSteamIDLobby);
            _isHost = true;
            SteamMatchmaking.SetLobbyData(_lobby, "wbmp_protocol", Wire.Version.ToString());
            SteamMatchmaking.SetLobbyData(_lobby, "wbmp_name", "WorldBox Gods 0.3.0");
            SteamMatchmaking.SetLobbyData(_lobby, "wbmp_worldfall", _worldfall.State);
            SteamMatchmaking.SetLobbyData(_lobby, "wbmp_wf_ver", _worldfall.Version);
            SteamMatchmaking.SetLobbyJoinable(_lobby, true);
            Status = "Lobby created " + _lobby.m_SteamID + ". Use Ctrl+Shift+I to invite friends.";
            _log(Status);
        }
        private void OnEntered(LobbyEnter_t callback)
        {
            _pendingLobby = false;
            if (callback.m_EChatRoomEnterResponse != 1) { Status = "Steam lobby refused join"; return; }
            CSteamID room = new CSteamID(callback.m_ulSteamIDLobby);
            bool owner = SteamMatchmaking.GetLobbyOwner(room) == SteamUser.GetSteamID();
            string proto = SteamMatchmaking.GetLobbyData(room, "wbmp_protocol");
            if (!owner && proto != Wire.Version.ToString())
            {
                SteamMatchmaking.LeaveLobby(room);
                Status = "Wrong/unsupported multiplayer lobby protocol";
                return;
            }
            _lobby = room; _owner = SteamMatchmaking.GetLobbyOwner(room);
            _isHost = owner; _joined = true; _lastCommitSequence = 0;
            _nextHelloAt = 0;
            _peerWorldfall.Clear();
            _snapshotsOut.Clear(); _lastSnapshotRequest.Clear();
            _syncHold = _syncRequested = _waitingForMapLoad = false;
            _snapshotIn = null; _pendingAfterSnapshot.Clear();
            _commitSequence = 0;
            SyncStatus = "World sync not requested";
            Status = (_isHost ? "Hosting " : "Joined ") + _lobby.m_SteamID;
            _log(Status);
        }
        private void OnInvite(GameLobbyJoinRequested_t callback)
        {
            if (_joined) Leave();
            Join(callback.m_steamIDLobby.m_SteamID);
        }
        private void OnRequest(SteamNetworkingMessagesSessionRequest_t callback)
        {
            CSteamID who = callback.m_identityRemote.GetSteamID();
            // Never accept a stranger's incoming messages merely because they know our Steam ID.
            if (_joined && IsMember(who))
            {
                var identity = callback.m_identityRemote;
                SteamNetworkingMessages.AcceptSessionWithUser(ref identity);
            }
        }
    }
}
