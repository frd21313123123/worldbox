using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace WorldBoxMultiplayer
{
    [BepInPlugin("com.worldbox.gods.multiplayer", "WorldBox Gods Multiplayer Preview", "0.3.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static Plugin _instance; // survives MonoBehaviour being destroyed on scene transitions
        private SteamSession _steam;
        private ConfigEntry<string> _power;
        private ConfigEntry<int> _x, _y;
        private ConfigEntry<string> _joinId;
        private bool _overlay = true;
        private bool _confirmWorldReplace;
        private double _nextErrorLog;
        private void Awake()
        {
            _instance = this;
            _power = Config.Bind("GodActions", "Power", "meteor", "Allowed example: human, elf, sheep, meteor, lightning, rain");
            _x = Config.Bind("GodActions", "X", 100, "World tile X to affect with Ctrl+Shift+P");
            _y = Config.Bind("GodActions", "Y", 100, "World tile Y to affect with Ctrl+Shift+P");
            _joinId = Config.Bind("Steam", "JoinLobbyId", "0", "Lobby ID for joining manually with Ctrl+Shift+J");
            WorldfallInfo worldfall = WorldfallInfo.Detect(Application.dataPath);
            Logger.LogInfo("Worldfall compatibility: " + worldfall.State + ", version: " + worldfall.Version);
            _steam = new SteamSession(new WorldPowers(), worldfall, Application.dataPath, text => Logger.LogInfo(text));
            _steam.Initialize();
            if (!MainThreadLoop.Install(Tick)) Logger.LogError("Unable to add Steam pump to PlayerLoop.Update");
            Logger.LogInfo("WorldBox Gods 0.3.0 preview ready: Ctrl+Shift+[H host, J join, I invite, P power, L leave, M menu]. Worldfall F6/F7 preserved.");
        }
        private void Tick()
        {
            try
            {
                if (_steam == null) return;
                _steam.Pump(Time.realtimeSinceStartup);
                bool chords = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
                              (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                if (chords && Input.GetKeyDown(KeyCode.H)) _steam.Host();
                if (chords && Input.GetKeyDown(KeyCode.J))
                {
                    ulong id;
                    if (ulong.TryParse(_joinId.Value, out id)) _steam.Join(id);
                    else Logger.LogWarning("Steam.JoinLobbyId must be a number");
                }
                if (chords && Input.GetKeyDown(KeyCode.I)) _steam.InviteFriends();
                if (chords && Input.GetKeyDown(KeyCode.P)) _steam.UsePower(_power.Value, _x.Value, _y.Value);
                if (chords && Input.GetKeyDown(KeyCode.L)) _steam.Leave();
                if (chords && Input.GetKeyDown(KeyCode.M)) _overlay = !_overlay;
            }
            catch (Exception ex)
            {
                if (Time.realtimeSinceStartup > _nextErrorLog)
                {
                    Logger.LogError("Multiplayer tick: " + ex);
                    _nextErrorLog = Time.realtimeSinceStartup + 5;
                }
            }
        }
        // Best-effort GUI; hotkeys above continue even if WorldBox destroys this MonoBehaviour.
        private void OnGUI()
        {
            if (!_overlay || _steam == null) return;
            try
            {
                GUILayout.BeginArea(new Rect(15, 50, 450, 370), GUI.skin.box);
                GUILayout.Label("WORLDBOX GODS MULTIPLAYER [0.3 PREVIEW]");
                GUILayout.Label(_steam.Status);
                GUILayout.Label("Lobby: " + _steam.LobbyId + "    Players: " + _steam.MemberCount);
                GUILayout.Label("Worldfall: " + _steam.WorldfallStatus + " | peers: " + _steam.WorldfallPeerCount);
                GUILayout.Label("World sync: " + _steam.SyncStatus);
                GUILayout.Label("Ctrl+Shift: H host | J join | I invite | L leave | M menu");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Host")) _steam.Host();
                if (GUILayout.Button("Join"))
                {
                    ulong lobbyId;
                    if (ulong.TryParse(_joinId.Value, out lobbyId)) _steam.Join(lobbyId);
                }
                if (GUILayout.Button("Invite")) _steam.InviteFriends();
                if (GUILayout.Button("Leave")) _steam.Leave();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Lobby ID", GUILayout.Width(60));
                _joinId.Value = GUILayout.TextField(_joinId.Value, 22);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Power", GUILayout.Width(45));
                _power.Value = GUILayout.TextField(_power.Value, 40, GUILayout.Width(100));
                GUILayout.Label("X", GUILayout.Width(12));
                int tx; string rawX = GUILayout.TextField(_x.Value.ToString(), 5, GUILayout.Width(50));
                if (int.TryParse(rawX, out tx)) _x.Value = tx;
                GUILayout.Label("Y", GUILayout.Width(12));
                int ty; string rawY = GUILayout.TextField(_y.Value.ToString(), 5, GUILayout.Width(50));
                if (int.TryParse(rawY, out ty)) _y.Value = ty;
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Ctrl+Shift+P: Use god power")) _steam.UsePower(_power.Value, _x.Value, _y.Value);
                GUILayout.Label("CAUTION: syncing replaces YOUR unsaved world!");
                if (!_confirmWorldReplace)
                {
                    if (GUILayout.Button("Request host world (confirm required)")) _confirmWorldReplace = true;
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("CONFIRM: Replace my world"))
                    {
                        _steam.RequestWorldSync(Time.realtimeSinceStartup);
                        _confirmWorldReplace = false;
                    }
                    if (GUILayout.Button("Cancel")) _confirmWorldReplace = false;
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndArea();
            }
            catch (Exception) { /* UI optional; main networking stays alive */ }
        }
        private void OnApplicationQuit()
        {
            if (_steam != null) _steam.Leave();
        }
    }
}
