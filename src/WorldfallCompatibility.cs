using System;
using System.IO;
using System.Reflection;

namespace WorldBoxMultiplayer
{
    // Integration intentionally avoids depending on private Worldfall implementation details.
    // No Harmony patches, 3D camera overrides, or redistribution of Worldfall assets.
    internal sealed class WorldfallInfo
    {
        internal string State { get; private set; }
        internal string Version { get; private set; }
        internal bool Loaded { get { return State == "loaded"; } }

        private WorldfallInfo(string state, string version)
        { State = state; Version = version; }

        internal static WorldfallInfo FromPeer(string state, string version)
        {
            if (state != "loaded" && state != "installed" && state != "none")
                throw new ArgumentException("Invalid Worldfall state");
            if (!Wire.ValidTag(version)) throw new ArgumentException("Invalid Worldfall version");
            return new WorldfallInfo(state, version);
        }

        internal static WorldfallInfo Detect(string gameDataDirectory)
        {
            // Runtime assembly takes precedence over presence of files on disk.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    string name = assembly.GetName().Name;
                    if (!IsWorldfallAssembly(name)) continue;
                    string version = assembly.GetName().Version == null ? "unknown" : assembly.GetName().Version.ToString();
                    if (version == "0.0.0.0" || !Wire.ValidTag(version)) version = "unknown";
                    return new WorldfallInfo("loaded", version);
                }
                catch (Exception) { /* another assembly could be unloading */ }
            }
            if (!string.IsNullOrEmpty(gameDataDirectory))
            {
                try
                {
                    string root = Path.GetDirectoryName(gameDataDirectory);
                    string direct = Path.Combine(gameDataDirectory, "StreamingAssets", "mods", "Worldfall.dll");
                    string nml = Path.Combine(root, "Mods", "Worldfall");
                    if (File.Exists(direct) || File.Exists(Path.Combine(nml, "Worldfall.dll")) ||
                        File.Exists(Path.Combine(nml, "mod.json")))
                        return new WorldfallInfo("installed", "unknown");
                }
                catch (Exception) { /* unavailable game directory */ }
            }
            return new WorldfallInfo("none", "unknown");
        }

        internal static bool IsWorldfallAssembly(string assemblyName)
        {
            return string.Equals(assemblyName, "Worldfall", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assemblyName, "Worldfall3D", StringComparison.OrdinalIgnoreCase);
        }

        // 'installed' and 'loaded' are diagnostic hints, not proof of networked 3D gameplay.
        internal static string DescribePair(WorldfallInfo local, WorldfallInfo remote)
        {
            if (!local.Loaded || !remote.Loaded)
                return "Worldfall is not loaded on both peers; 3D display only where installed";
            if (local.Version != "unknown" && remote.Version != "unknown" && local.Version != remote.Version)
                return "Worldfall assembly versions differ; 3D behaviour may diverge";
            return "Both peers report Worldfall loaded; 3D avatars are NOT synchronized";
        }
    }
}
