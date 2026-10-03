using HarmonyLib;
using Kingmaker.PubSubSystem;
using UnityEngine;
using WrathBuildPlanner.Localization;
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
            modEntry.OnGUI = OnGUI;

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

        static void OnGUI(UnityModManager.ModEntry modEntry) {
            var settings = Persistence.ModSettings.Current;

            GUILayout.BeginHorizontal();
            GUILayout.Label("settings.hotkey".i18n(), GUILayout.Width(320));
            string hotkey = GUILayout.TextField(settings.Hotkey ?? "", 12, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("settings.language".i18n(), GUILayout.Width(320));
            int index = System.Array.IndexOf(Strings.LanguageChoices, settings.Language);
            int chosen = GUILayout.SelectionGrid(index < 0 ? 0 : index, Strings.LanguageChoices, Strings.LanguageChoices.Length, GUILayout.Width(360));
            GUILayout.EndHorizontal();

            bool verbose = GUILayout.Toggle(settings.Verbose, " " + "settings.verbose".i18n());

            string language = Strings.LanguageChoices[chosen];
            if (hotkey != settings.Hotkey || language != settings.Language || verbose != settings.Verbose) {
                settings.Hotkey = hotkey;
                settings.Language = language;
                settings.Verbose = verbose;
                Persistence.ModSettings.Save();
            }
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
