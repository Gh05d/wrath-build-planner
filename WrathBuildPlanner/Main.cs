using HarmonyLib;
using UnityModManagerNet;

namespace WrathBuildPlanner {
    static class Main {
        static Harmony harmony;
        public static UnityModManager.ModEntry ModEntry;
        public static string ModPath;

        static bool Load(UnityModManager.ModEntry modEntry) {
            ModEntry = modEntry;
            ModPath = modEntry.Path;
            modEntry.OnUnload = OnUnload;

            Logging.DebugLog.Init(modEntry.Path);
            harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            modEntry.Logger.Log("Wrath Build Planner loaded.");
            return true;
        }

        static bool OnUnload(UnityModManager.ModEntry modEntry) {
            try {
                harmony.UnpatchAll(modEntry.Info.Id);
            } finally {
                Logging.DebugLog.Shutdown();
            }
            return true;
        }
    }
}
