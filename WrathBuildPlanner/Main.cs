using HarmonyLib;
using Kingmaker.PubSubSystem;
using UnityModManagerNet;

namespace WrathBuildPlanner {
    static class Main {
        static Harmony harmony;
        public static UnityModManager.ModEntry ModEntry;
        public static string ModPath;
        public static Persistence.BuildLibrary Library;
        static AreaWatcher areaWatcher;

        static bool Load(UnityModManager.ModEntry modEntry) {
            ModEntry = modEntry;
            ModPath = modEntry.Path;
            modEntry.OnUnload = OnUnload;

            Logging.DebugLog.Init(modEntry.Path);
            Localization.Strings.Initialise();
            harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            UI.AssetLoader.Init();
            UI.ThemeProvider.Init();
            UI.PlannerController.Install();

            Library = new Persistence.BuildLibrary(System.IO.Path.Combine(ModPath, "Builds"), Engine.GameNames.Known);
            EventBus.Subscribe(areaWatcher = new AreaWatcher());

            modEntry.Logger.Log("Wrath Build Planner loaded.");
            return true;
        }

        static bool OnUnload(UnityModManager.ModEntry modEntry) {
            try {
                UI.PlannerController.Uninstall();
                if (areaWatcher != null) EventBus.Unsubscribe(areaWatcher);
                harmony.UnpatchAll(modEntry.Info.Id);
            } finally {
                Logging.DebugLog.Shutdown();
            }
            return true;
        }

        class AreaWatcher : IAreaHandler {
            public void OnAreaDidLoad() {
                Persistence.AssignmentStore.OnAreaLoaded();
            }

            public void OnAreaBeginUnloading() { }
        }
    }
}
