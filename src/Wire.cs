using System;
using System.Text;
using System.Globalization;

namespace WorldBoxMultiplayer
{
    // Deliberately tiny ASCII-on-UTF8 protocol. No deserialization of arbitrary objects.
    // All packets are bounded to 512 bytes; only the host may transmit COMMIT.
    internal sealed class Wire
    {
        internal const int Version = 3;
        internal const int MaxPacketBytes = 512;
        internal string Kind, Power, Detail, WorldfallState, WorldfallVersion, Sha256;
        internal int Sequence, X, Y, TransferId, Size, Chunks, BaseCommit;
        internal bool Accepted;

        internal static byte[] Hello(string state, string version)
        {
            if (!ValidState(state) || !ValidTag(version)) throw new ArgumentException("Invalid capability tag");
            return Encoding.UTF8.GetBytes("HELLO|3|" + state + "|" + version);
        }
        internal static byte[] SyncRequest() { return Encoding.UTF8.GetBytes("SYNCREQ|3"); }
        internal static byte[] Deny(string reason)
        {
            if (reason != "busy" && reason != "cooldown" && reason != "unavailable")
                throw new ArgumentException("Invalid deny code");
            return Encoding.ASCII.GetBytes("DENY|3|" + reason);
        }
        internal static byte[] Begin(int id, int size, int chunks, int baseCommit, string sha256)
        {
            if (id < 1 || size < 1 || size > SnapshotTransfer.MaxBytes ||
                chunks != SnapshotTransfer.Count(size) || baseCommit < 0 ||
                !SnapshotAssembler.ValidHash(sha256)) throw new ArgumentException("Invalid manifest");
            return Encoding.ASCII.GetBytes("BEGIN|3|" + id + "|" + size + "|" + chunks + "|" + baseCommit + "|" + sha256);
        }
        internal static bool ValidState(string s)
        { return s == "loaded" || s == "installed" || s == "none"; }
        internal static bool ValidTag(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 24) return false;
            foreach (char c in s)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_')) return false;
            return true;
        }
        internal static byte[] Action(int seq, string id, int x, int y)
        { return Pack("ACT", seq, id, x, y); }
        internal static byte[] Commit(int seq, string id, int x, int y)
        { return Pack("COMMIT", seq, id, x, y); }
        internal static byte[] Ack(int seq, bool accepted)
        { return Encoding.UTF8.GetBytes("ACK|" + seq.ToString(CultureInfo.InvariantCulture) + "|" + (accepted ? "1" : "0")); }
        private static byte[] Pack(string type, int seq, string id, int x, int y)
        {
            if (!ValidPower(id)) throw new ArgumentException("Invalid power id");
            return Encoding.UTF8.GetBytes(type + "|" + seq.ToString(CultureInfo.InvariantCulture) + "|" + id + "|"
                + x.ToString(CultureInfo.InvariantCulture) + "|" + y.ToString(CultureInfo.InvariantCulture));
        }
        internal static bool ValidPower(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 40) return false;
            foreach (char c in s)
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')) return false;
            return true;
        }
        internal static bool TryParse(byte[] bytes, out Wire packet)
        {
            packet = null;
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxPacketBytes) return false;
            foreach (byte b in bytes) if (b < 32 || b > 126) return false;
            string[] parts = Encoding.UTF8.GetString(bytes).Split('|');
            if (parts.Length == 3 && parts[0] == "DENY" && parts[1] == "3" &&
                (parts[2] == "busy" || parts[2] == "cooldown" || parts[2] == "unavailable"))
            {
                packet = new Wire { Kind = "DENY", Detail = parts[2] };
                return true;
            }
            if (parts.Length == 2 && parts[0] == "SYNCREQ" && parts[1] == "3")
            {
                packet = new Wire { Kind = "SYNCREQ" };
                return true;
            }
            if (parts.Length == 7 && parts[0] == "BEGIN" && parts[1] == "3")
            {
                int id, size, chunks, baseCommit;
                if (!ParsePositive(parts[2], out id) ||
                    !ParsePositive(parts[3], out size) ||
                    !ParsePositive(parts[4], out chunks) ||
                    !int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out baseCommit) ||
                    baseCommit < 0 || !SnapshotAssembler.ValidHash(parts[6])) return false;
                if (size > SnapshotTransfer.MaxBytes || chunks != SnapshotTransfer.Count(size)) return false;
                packet = new Wire { Kind = "BEGIN", TransferId = id, Size = size,
                    Chunks = chunks, BaseCommit = baseCommit, Sha256 = parts[6] };
                return true;
            }
            if (parts.Length == 4 && parts[0] == "HELLO" && parts[1] == "3" &&
                ValidState(parts[2]) && ValidTag(parts[3]))
            {
                packet = new Wire { Kind = "HELLO", WorldfallState = parts[2], WorldfallVersion = parts[3] };
                return true;
            }
            if (parts.Length == 3 && parts[0] == "ACK")
            {
                int seq;
                if (!ParsePositive(parts[1], out seq) || (parts[2] != "0" && parts[2] != "1")) return false;
                packet = new Wire { Kind = "ACK", Sequence = seq, Accepted = parts[2] == "1" };
                return true;
            }
            if (parts.Length == 5 && (parts[0] == "ACT" || parts[0] == "COMMIT"))
            {
                int seq, x, y;
                if (!ParsePositive(parts[1], out seq) || !ValidPower(parts[2])
                    || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                    || !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out y)) return false;
                if (x < 0 || y < 0 || x > 9999 || y > 9999) return false;
                packet = new Wire { Kind = parts[0], Sequence = seq, Power = parts[2], X = x, Y = y };
                return true;
            }
            return false;
        }
        private static bool ParsePositive(string text, out int result)
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out result)) return false;
            return result > 0;
        }
    }
}
