using System;
using System.IO;
using WorldBoxMultiplayer;

// Fake WorldBox API surface. This is a contract test, NOT an in-game test.
public sealed class MapBox { public static MapBox instance = new MapBox(); }
public static class Config { public static bool worldLoading; }
public static class SaveManager
{
    public static byte[] Expected = { 0, 1, 4, 7, 13, 42 };
    public static byte[] LastLoaded;
    public static void saveWorldToDirectory(string path, bool compress, bool checkFolder)
    {
        if (!compress || !checkFolder) throw new Exception("Expected save options");
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, "map.wbox"), Expected);
    }
    public static void loadMapFromBytes(byte[] save) { LastLoaded = save; }
}
public static class GameAdapterContractTests
{
    public static void Main()
    {
        var provider = new GameWorldSnapshot();
        byte[] saved = provider.Capture();
        if (saved == null || saved.Length != SaveManager.Expected.Length || saved[5] != 42)
            throw new Exception("Capture failed: " + provider.LastError);
        if (!provider.Load(saved) || SaveManager.LastLoaded == null || SaveManager.LastLoaded[5] != 42)
            throw new Exception("Load invocation failed: " + provider.LastError);
        Config.worldLoading = true;
        if (provider.Capture() != null || !provider.LastError.Contains("loading"))
            throw new Exception("Loading guard failed");
        Console.WriteLine("PASS SaveManager save/load reflective contract (fake API)");
    }
}
