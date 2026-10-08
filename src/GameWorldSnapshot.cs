using System;
using System.IO;
using System.Reflection;

namespace WorldBoxMultiplayer
{
    // WorldBox 0.51.2 API bridge; intentionally reflection-based to fail closed on signature drift.
    // Every call must run on Unity's main thread. Exact save->load flow needs a live in-game test.
    internal sealed class GameWorldSnapshot
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        internal string LastError { get; private set; }

        internal byte[] Capture()
        {
            string temp = Path.Combine(Path.GetTempPath(), "wbmp-" + Guid.NewGuid().ToString("N"));
            try
            {
                Type type = Type.GetType("SaveManager, Assembly-CSharp", false);
                Type map = Type.GetType("MapBox, Assembly-CSharp", false);
                if (type == null || map == null) throw new InvalidOperationException("WorldBox API not found");
                FieldInfo instance = map.GetField("instance", Flags);
                if (instance == null || instance.GetValue(null) == null) throw new InvalidOperationException("Load a world first");
                Type config = Type.GetType("Config, Assembly-CSharp", false);
                if (config != null && ReadStaticBoolean(config, "worldLoading"))
                    throw new InvalidOperationException("World is still loading");
                MethodInfo save = type.GetMethod("saveWorldToDirectory", Flags, null,
                    new[] { typeof(string), typeof(bool), typeof(bool) }, null);
                if (save == null) throw new MissingMethodException("SaveManager.saveWorldToDirectory signature changed");
                Directory.CreateDirectory(temp);
                save.Invoke(null, new object[] { temp, true, true });
                string savefile = Path.Combine(temp, "map.wbox");
                if (!File.Exists(savefile)) throw new FileNotFoundException("map.wbox missing from save directory");
                var info = new FileInfo(savefile);
                if (info.Length < 1 || info.Length > SnapshotTransfer.MaxBytes)
                    throw new InvalidOperationException("World save exceeds 64 MiB safety limit");
                byte[] data = File.ReadAllBytes(savefile);
                LastError = null;
                return data;
            }
            catch (Exception ex) { LastError = (ex.InnerException ?? ex).Message; return null; }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch (Exception) { /* no user files live in this unique temporary folder */ }
            }
        }
        internal bool Load(byte[] data)
        {
            if (data == null || data.Length < 1 || data.Length > SnapshotTransfer.MaxBytes)
            { LastError = "Invalid world save size"; return false; }
            try
            {
                Type type = Type.GetType("SaveManager, Assembly-CSharp", false);
                MethodInfo method = type == null ? null : type.GetMethod("loadMapFromBytes", Flags,
                    null, new[] { typeof(byte[]) }, null);
                if (method == null) throw new MissingMethodException("SaveManager.loadMapFromBytes signature changed");
                method.Invoke(null, new object[] { data });
                LastError = null;
                return true; // Async loader started, NOT evidence the map finished loading.
            }
            catch (Exception ex) { LastError = (ex.InnerException ?? ex).Message; return false; }
        }
        private static bool ReadStaticBoolean(Type type, string name)
        {
            var field = type.GetField(name, Flags);
            if (field != null && field.FieldType == typeof(bool)) return (bool)field.GetValue(null);
            var property = type.GetProperty(name, Flags);
            return property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(null, null);
        }
    }
}
