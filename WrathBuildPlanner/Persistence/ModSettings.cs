using System;
using System.IO;
using Newtonsoft.Json;
using WrathBuildPlanner.Logging;

namespace WrathBuildPlanner.Persistence {
    public class ModSettingsData {
        public string Hotkey = "P";
        public string Language = "auto";
        public bool Verbose;
    }

    /// <summary>Global (not per save) mod settings: Mods/WrathBuildPlanner/settings.json.</summary>
    public static class ModSettings {
        static ModSettingsData current;

        static string FilePath => Path.Combine(Main.ModPath, "settings.json");

        public static ModSettingsData Current {
            get {
                if (current != null) return current;
                current = new ModSettingsData();
                if (!File.Exists(FilePath)) return current;
                try {
                    current = JsonConvert.DeserializeObject<ModSettingsData>(File.ReadAllText(FilePath)) ?? new ModSettingsData();
                } catch (JsonException e) {
                    Log.Persistence.Error(e, "settings.json is not valid JSON; using defaults");
                } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
                    Log.Persistence.Error(e, "settings.json could not be read; using defaults");
                }
                return current;
            }
        }

        public static void Save() {
            try {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(Current, Formatting.Indented));
            } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
                Log.Persistence.Error(e, "settings.json could not be saved");
            }
        }
    }
}
