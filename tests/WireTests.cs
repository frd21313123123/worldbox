using System;
using System.Text;
using WorldBoxMultiplayer;

static class TestProgram
{
    private static int tests;
    private static void Check(bool condition, string name)
    {
        tests++;
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
    private static bool Parse(string s, out Wire msg)
    {
        return Wire.TryParse(Encoding.UTF8.GetBytes(s), out msg);
    }
    public static void Main()
    {
        Wire w;
        Check(Wire.TryParse(Wire.Hello("loaded", "0.9.4"), out w) && w.Kind == "HELLO" && w.WorldfallVersion == "0.9.4", "hello");
        Check(Wire.TryParse(Wire.Action(15, "meteor", 30, 40), out w) && w.Kind == "ACT" && w.X == 30 && w.Y == 40, "action roundtrip");
        Check(Wire.TryParse(Wire.Commit(1, "elf", 0, 9999), out w) && w.Kind == "COMMIT", "commit roundtrip");
        Check(Wire.TryParse(Wire.Ack(22, false), out w) && w.Kind == "ACK" && !w.Accepted, "ack roundtrip");
        Check(!Parse("HELLO|2", out w), "version mismatch");
        Check(!Parse("HELLO|3|loaded|foo/../bar", out w), "reject unsafe version");
        Check(!Parse("HELLO|3|x|0.9.4", out w), "reject invalid worldfall status");
        Check(Wire.TryParse(Wire.Hello("none", "unknown"), out w), "worldfall absent handshake");
        Check(WorldfallInfo.IsWorldfallAssembly("Worldfall"), "worldfall loaded assembly recognized");
        Check(!WorldfallInfo.IsWorldfallAssembly("WorldBox"), "no false assembly match");
        Check(WorldfallInfo.DescribePair(WorldfallInfo.FromPeer("loaded", "0.9.4"), WorldfallInfo.FromPeer("loaded", "0.9.3")).Contains("differ"), "version mismatch report");
        Check(WorldfallInfo.DescribePair(WorldfallInfo.FromPeer("loaded", "0.9.4"), WorldfallInfo.FromPeer("none", "unknown")).Contains("not loaded"), "missing worldfall report");
        Check(!Parse("ACT|1|meteor|1|-3", out w), "negative y");
        Check(!Parse("ACT|1|meteor|10000|1", out w), "out of bounds");
        Check(!Parse("ACT|0|meteor|10|10", out w), "zero sequence");
        Check(!Parse("ACT|1|../save|10|10", out w), "untrusted power names");
        Check(!Parse("ACT|1|meteor|10|10|EXTRA", out w), "extra field");
        Check(!Parse("ACK|5|yes", out w), "invalid ack");
        Check(Wire.TryParse(Wire.Deny("busy"), out w) && w.Kind == "DENY" && w.Detail == "busy", "server deny");
        Check(!Parse("DENY|3|arbitrary_code", out w), "reject arbitrary deny codes");
        Check(!Wire.TryParse(new byte[600], out w), "oversized packet");
        Check(!Wire.ValidPower("meteor|ACT"), "delimiter blocked");
        Console.WriteLine("PASS: " + tests + " wire tests");
    }
}
