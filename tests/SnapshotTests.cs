using System;
using System.Text;
using WorldBoxMultiplayer;

static class SnapshotTestProgram
{
    static int n;
    static void Check(bool ok, string name) { n++; if (!ok) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
    public static void Main()
    {
        var random = new Random(42);
        byte[] bytes = new byte[1024 * 1024 + 3];
        random.NextBytes(bytes);
        var sha = SnapshotTransfer.Sha256(bytes);
        int count = SnapshotTransfer.Count(bytes.Length);
        Wire w;
        Check(Wire.TryParse(Wire.Begin(22, bytes.Length, count, 12, sha), out w) &&
            w.Kind == "BEGIN" && w.BaseCommit == 12, "manifest parsed");
        var collector = new SnapshotAssembler(w.TransferId, w.Size, w.Chunks, w.BaseCommit, w.Sha256);
        for (int i = count - 1; i >= 0; i--)
        {
            byte[] chunk = SnapshotTransfer.Chunk(22, i, bytes);
            Check(collector.Add(chunk), "chunk " + i);
        }
        byte[] output;
        Check(collector.Complete && collector.TryFinish(out output), "all chunks verify sha256");
        Check(output.Length == bytes.Length && SnapshotTransfer.Sha256(output) == sha, "roundtrip integrity");
        Check(collector.Add(SnapshotTransfer.Chunk(22, 0, bytes)) && collector.ReceivedChunks == count, "duplicate idempotence");
        var tampered = SnapshotTransfer.Chunk(22, 0, bytes); tampered[12] ^= 0xFF;
        var corrupt = new SnapshotAssembler(22, bytes.Length, count, 12, sha);
        Check(corrupt.Add(tampered), "corrupt payload accepted for hash-check later");
        for (int i = 1; i < count; i++) corrupt.Add(SnapshotTransfer.Chunk(22, i, bytes));
        Check(!corrupt.TryFinish(out output), "sha256 rejects corruption");
        Check(!collector.Add(SnapshotTransfer.Chunk(21, 0, bytes)), "wrong transfer rejected");
        Check(!Wire.TryParse(Encoding.ASCII.GetBytes("BEGIN|3|1|0|1|0|" + sha), out w), "zero size rejected");
        Check(!Wire.TryParse(Encoding.ASCII.GetBytes("BEGIN|3|1|1048576|99999|0|" + sha), out w), "invalid chunk count rejected");
        Check(Wire.TryParse(Wire.SyncRequest(), out w) && w.Kind == "SYNCREQ", "request parsed");
        Console.WriteLine("PASS " + n + " snapshot transfer tests");
    }
}
