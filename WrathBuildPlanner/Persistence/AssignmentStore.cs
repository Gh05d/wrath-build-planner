using System;
using System.Collections.Generic;
using System.IO;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Newtonsoft.Json;
using WrathBuildPlanner.Logging;

namespace WrathBuildPlanner.Persistence {
    /// <summary>
    /// Which character follows which build: per save, keyed by the unit's unique id, value = build file name.
    /// While a new game's character is being created there is no save yet; that choice is held in memory
    /// and bound to the main character when the first area loads.
    /// </summary>
    public static class AssignmentStore {
        static Dictionary<string, string> current;
        static string loadedForGame;
        static string pendingForNewGame;

        static string Dir => Path.Combine(Main.ModPath, "UserSettings");

        static string GameId => Game.Instance?.Player?.GameId;

        static string PathFor(string gameId) => Path.Combine(Dir, $"assignments-{gameId}.json");

        static bool InRunningGame => !string.IsNullOrEmpty(GameId) && Game.Instance.CurrentlyLoadedArea != null;

        public static string Get(UnitEntityData unit) {
            if (!InRunningGame) return pendingForNewGame;
            EnsureLoaded();
            return unit != null && current.TryGetValue(unit.UniqueId, out string file) ? file : null;
        }

        public static void Set(UnitEntityData unit, string fileName) {
            if (!InRunningGame) {
                pendingForNewGame = fileName;
                return;
            }
            if (unit == null) return;
            EnsureLoaded();
            if (fileName == null) current.Remove(unit.UniqueId);
            else current[unit.UniqueId] = fileName;
            Save();
        }

        /// <summary>Called on every area load: reload for the active save, bind a creation-time choice to the main character.</summary>
        public static void OnAreaLoaded() {
            current = null;
            if (pendingForNewGame == null || string.IsNullOrEmpty(GameId)) return;
            var main = Game.Instance.Player.MainCharacter.Value;
            if (main == null) return;
            EnsureLoaded();
            if (!current.ContainsKey(main.UniqueId)) {
                current[main.UniqueId] = pendingForNewGame;
                Save();
                Log.Persistence.Info($"bound creation-time build '{pendingForNewGame}' to the main character");
            }
            pendingForNewGame = null;
        }

        /// <summary>
        /// The creation window was closed without completing (CharGenVM.Close). Without this, a build chosen
        /// in a cancelled creation would be bound to the main character of whatever save is loaded next.
        /// </summary>
        public static void ClearPending() {
            pendingForNewGame = null;
        }

        public static void Reset() {
            current = null;
            loadedForGame = null;
        }

        static void EnsureLoaded() {
            string gameId = GameId;
            if (current != null && loadedForGame == gameId) return;
            loadedForGame = gameId;
            current = new Dictionary<string, string>();
            string path = PathFor(gameId);
            if (!File.Exists(path)) return;
            try {
                current = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path)) ?? new Dictionary<string, string>();
            } catch (JsonException e) {
                Log.Persistence.Error(e, "assignments file is not valid JSON; starting empty (file kept)");
            } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
                Log.Persistence.Error(e, "assignments file could not be read; starting empty");
            }
        }

        static void Save() {
            try {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(PathFor(GameId), JsonConvert.SerializeObject(current, Formatting.Indented));
            } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
                Log.Persistence.Error(e, "assignments could not be saved");
            }
        }
    }
}
