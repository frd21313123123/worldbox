using System;
using System.IO;
using System.Security.Cryptography;

namespace WorldBoxMultiplayer
{
    // Pure protocol code: no Steam or WorldBox dependencies. One-way transfer from lobby owner only.
    internal static class SnapshotTransfer
    {
        internal const int ChunkSize = 16 * 1024;
        internal const int MaxBytes = 64 * 1024 * 1024;
        internal const int HeaderBytes = 12;
        private static readonly byte[] Magic = { (byte)'W', (byte)'B', (byte)'M', (byte)'3' };

        internal static int Count(int size)
        {
            if (size < 1 || size > MaxBytes) throw new ArgumentOutOfRangeException("size");
            return (size + ChunkSize - 1) / ChunkSize;
        }
        internal static byte[] Chunk(int transferId, int index, byte[] data)
        {
            if (transferId < 1 || data == null || data.Length < 1 || data.Length > MaxBytes)
                throw new ArgumentException("Invalid snapshot");
            int count = Count(data.Length);
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException("index");
            int offset = index * ChunkSize;
            int length = Math.Min(ChunkSize, data.Length - offset);
            var packet = new byte[HeaderBytes + length];
            Buffer.BlockCopy(Magic, 0, packet, 0, Magic.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(transferId), 0, packet, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(index), 0, packet, 8, 4);
            Buffer.BlockCopy(data, offset, packet, HeaderBytes, length);
            return packet;
        }
        internal static bool IsChunk(byte[] packet)
        {
            return packet != null && packet.Length >= 4 && packet[0] == Magic[0] &&
                packet[1] == Magic[1] && packet[2] == Magic[2] && packet[3] == Magic[3];
        }
        internal static bool TryHeader(byte[] packet, out int id, out int index)
        {
            id = index = 0;
            if (!IsChunk(packet) || packet.Length <= HeaderBytes || packet.Length > HeaderBytes + ChunkSize) return false;
            id = BitConverter.ToInt32(packet, 4);
            index = BitConverter.ToInt32(packet, 8);
            return id > 0 && index >= 0;
        }
        internal static string Sha256(byte[] data)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }

    internal sealed class SnapshotAssembler
    {
        private readonly byte[] _buffer;
        private readonly bool[] _received;
        private readonly string _expectedHash;
        internal int TransferId { get; private set; }
        internal int TotalChunks { get { return _received.Length; } }
        internal int ReceivedChunks { get; private set; }
        internal int BaseCommit { get; private set; }
        internal bool Complete { get { return ReceivedChunks == TotalChunks; } }
        internal SnapshotAssembler(int id, int size, int count, int baseCommit, string hash)
        {
            if (id < 1 || size < 1 || size > SnapshotTransfer.MaxBytes ||
                count != SnapshotTransfer.Count(size) || baseCommit < 0 || !ValidHash(hash))
                throw new ArgumentException("Invalid snapshot manifest");
            TransferId = id;
            _buffer = new byte[size];
            _received = new bool[count];
            BaseCommit = baseCommit;
            _expectedHash = hash.ToLowerInvariant();
        }
        internal bool Add(byte[] packet)
        {
            int id, index;
            if (!SnapshotTransfer.TryHeader(packet, out id, out index) || id != TransferId || index >= TotalChunks)
                return false;
            int offset = index * SnapshotTransfer.ChunkSize;
            int expected = Math.Min(SnapshotTransfer.ChunkSize, _buffer.Length - offset);
            if (packet.Length != SnapshotTransfer.HeaderBytes + expected) return false;
            if (_received[index]) return true; // duplicate delivery must not advance progress
            Buffer.BlockCopy(packet, SnapshotTransfer.HeaderBytes, _buffer, offset, expected);
            _received[index] = true;
            ReceivedChunks++;
            return true;
        }
        internal bool TryFinish(out byte[] snapshot)
        {
            snapshot = null;
            if (!Complete || SnapshotTransfer.Sha256(_buffer) != _expectedHash) return false;
            snapshot = _buffer;
            return true;
        }
        internal static bool ValidHash(string hash)
        {
            if (hash == null || hash.Length != 64) return false;
            foreach (char c in hash)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }
    }
}
