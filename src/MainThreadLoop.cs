using System;
using System.Collections.Generic;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace WorldBoxMultiplayer
{
    // Plugin MonoBehaviour.Update may disappear in WorldBox scene transitions.
    // PlayerLoop runs the Steam callbacks and gameplay on Unity's main thread instead.
    internal static class MainThreadLoop
    {
        private struct WorldBoxMultiplayerTick { }
        private static Action _tick;
        internal static bool Install(Action callback)
        {
            _tick = callback;
            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            if (!Inject(ref root)) return false;
            PlayerLoop.SetPlayerLoop(root);
            return true;
        }
        private static void Tick() { if (_tick != null) _tick(); }
        private static bool Inject(ref PlayerLoopSystem system)
        {
            if (system.subSystemList == null) return false;
            var children = new List<PlayerLoopSystem>(system.subSystemList);
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].type == typeof(Update))
                {
                    PlayerLoopSystem phase = children[i];
                    var updated = new List<PlayerLoopSystem>(phase.subSystemList ?? new PlayerLoopSystem[0]);
                    // Idempotent if an earlier plugin load already installed the hook.
                    foreach (PlayerLoopSystem entry in updated)
                        if (entry.type == typeof(WorldBoxMultiplayerTick)) return true;
                    updated.Add(new PlayerLoopSystem { type = typeof(WorldBoxMultiplayerTick), updateDelegate = Tick });
                    phase.subSystemList = updated.ToArray();
                    children[i] = phase;
                    system.subSystemList = children.ToArray();
                    return true;
                }
                PlayerLoopSystem nested = children[i];
                if (Inject(ref nested))
                {
                    children[i] = nested;
                    system.subSystemList = children.ToArray();
                    return true;
                }
            }
            return false;
        }
    }
}
