using System;
using System.Reflection;
using System.Collections.Generic;

namespace WorldBoxMultiplayer
{
    // Strict allowlist, not a remote reflection / arbitrary RPC facility.
    // All lookups target the hosting game's Assembly-CSharp types.
    internal sealed class WorldPowers
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly HashSet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "human", "elf", "orc", "dwarf", "sheep", "wolf", "dragon", "meteor",
            "lightning", "rain", "fire", "tornado", "bomb", "plague", "volcano"
        };
        internal string LastError { get; private set; }
        internal bool Apply(string id, int x, int y)
        {
            LastError = "";
            if (!Allowed.Contains(id)) { LastError = "Power not in safe allowlist"; return false; }
            try
            {
                Type mapType = Type.GetType("MapBox, Assembly-CSharp", false);
                Type assets = Type.GetType("AssetManager, Assembly-CSharp", false);
                if (mapType == null || assets == null) throw new Exception("WorldBox reflection types unavailable");
                object map = ReadField(mapType, null, "instance");
                if (map == null) throw new Exception("World not loaded");
                int width = Convert.ToInt32(ReadField(mapType, null, "width"));
                int height = Convert.ToInt32(ReadField(mapType, null, "height"));
                if (x < 0 || y < 0 || x >= width || y >= height) throw new Exception("Coordinates out of map bounds");
                var tileMap = ReadField(mapType, map, "tiles_map") as Array;
                if (tileMap == null) throw new Exception("World tiles not loaded");
                object tile = tileMap.GetValue(x, y);
                if (tile == null) throw new Exception("Tile is null");
                object powers = ReadField(assets, null, "powers");
                if (powers == null) throw new Exception("AssetManager.powers missing");
                MethodInfo get = powers.GetType().GetMethod("get", All, null, new [] { typeof(string) }, null);
                if (get == null) throw new Exception("Power library API changed");
                object power = get.Invoke(powers, new object[] { id });
                if (power == null) throw new Exception("Unknown GodPower " + id);
                // First prefer the game's single-tile delegates to avoid changing local brush state.
                var singleWithId = ReadField(power.GetType(), power, "click_action") as Delegate;
                var singleWithPower = ReadField(power.GetType(), power, "click_power_action") as Delegate;
                Delegate chosen = singleWithId ?? singleWithPower;
                if (chosen == null) throw new Exception("Power requires a brush, toggle, or mouse-specific delegate");
                object value = chosen.DynamicInvoke(tile, singleWithId != null ? (object)id : power);
                if (value is bool && !(bool)value) { LastError = "Game rejected power"; return false; }
                return true;
            }
            catch (Exception e)
            {
                LastError = (e.InnerException ?? e).Message;
                return false;
            }
        }
        private static object ReadField(Type type, object obj, string name)
        {
            // Includes fields inherited from generic library base classes.
            for (Type cursor = type; cursor != null; cursor = cursor.BaseType)
            {
                FieldInfo f = cursor.GetField(name, All | BindingFlags.DeclaredOnly);
                if (f != null) return f.GetValue(obj);
            }
            return null;
        }
    }
}
