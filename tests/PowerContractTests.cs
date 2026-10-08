using System;
using WorldBoxMultiplayer;

// This executable mocks the WorldBox 0.51.2 reflection contracts. It is
// intentionally named Assembly-CSharp so Type.GetType resolves to a test stub.
public sealed class WorldTile { }
public sealed class MapBox
{
    public static MapBox instance = new MapBox();
    public static int width = 8;
    public static int height = 8;
    public WorldTile[,] tiles_map = new WorldTile[8, 8];
    public ActorManager units = new ActorManager();
    public MapBox()
    {
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                tiles_map[x, y] = new WorldTile();
    }
}
public sealed class ActorManager
{
    public string LastSpawned;
    public object spawnNewUnit(string id, WorldTile tile, bool spawnSound = false,
        bool miracle = false, float spawnHeight = 6f, object sub = null,
        bool giveOwnerlessItems = false, bool adult = false)
    {
        LastSpawned = id;
        return new object();
    }
}
public static class AssetManager { public static FakePowers powers = new FakePowers(); }
public sealed class FakePowers
{
    public FakePower get(string id)
    {
        return id == "meteor" ? new FakePower { click_action = (tile, name) => name == "meteor" } : null;
    }
}
public sealed class FakePower
{
    public Func<WorldTile, string, bool> click_action;
    public Func<WorldTile, FakePower, bool> click_power_action;
}
public static class PowerContractTests
{
    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL " + label);
        Console.WriteLine("PASS " + label);
    }
    public static void Main()
    {
        var powers = new WorldPowers();
        Check(powers.Apply("dragon", 3, 4) && MapBox.instance.units.LastSpawned == "dragon", "dragon via ActorManager");
        Check(powers.Apply("human", 1, 2) && MapBox.instance.units.LastSpawned == "human", "human via ActorManager");
        Check(powers.Apply("meteor", 0, 0), "meteor via GodPower delegate");
        Check(!powers.Apply("dragon", -1, 2), "reject negative x");
        Check(!powers.Apply("unknown", 1, 1), "reject unknown ability");
        Check(!powers.Apply("human", 10000, 2), "reject out-of-bounds spawn");
    }
}
