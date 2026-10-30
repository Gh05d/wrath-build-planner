using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using WrathBuildPlanner.Core;

namespace WrathBuildPlanner.Engine {
    public class ExportOutcome {
        public bool Ok;
        public string FileName;
        public List<string> Gaps = new List<string>();
        public int SkippedPicks;
        public string Error;
    }

    /// <summary>A recruited character written as a build file into the Builds folder.</summary>
    static class CharacterExport {
        public static List<UnitEntityData> Party() =>
            Game.Instance.Player.Party.Where(u => u != null && !u.IsPet).Distinct().ToList();

        /// <summary>Recruited characters outside the active party (camp, capital). Pets are left out: the planner has no pet path.</summary>
        public static List<UnitEntityData> Reserve() {
            var party = new HashSet<string>(Party().Select(u => u.UniqueId));
            return Game.Instance.Player.AllCharacters.Concat(Game.Instance.Player.RemoteCompanions)
                .Where(u => u != null && !u.IsPet && !party.Contains(u.UniqueId))
                .GroupBy(u => u.UniqueId).Select(g => g.First()).ToList();
        }

        public static ExportOutcome Export(UnitEntityData unit) {
            var outcome = new ExportOutcome();
            try {
                var record = CharacterReader.Read(unit);
                outcome.SkippedPicks = record.SkippedPicks;
                if (record.Levels.Count == 0) {
                    outcome.Error = Localization.Strings.Format("export.no_history", unit.CharacterName);
                    return outcome;
                }
                var build = BuildExporter.ToBuild(record);
                var result = Main.Library.ImportText(BuildExporter.ToJson(build));
                outcome.Ok = result.Ok;
                outcome.FileName = result.FileName;
                outcome.Gaps = BuildExporter.Gaps(record, build);
                if (!result.Ok) outcome.Error = string.Join("; ", result.Issues.Where(i => i.IsError).Select(i => i.Message));
            } catch (Exception e) {
                outcome.Error = e.Message;
                Logging.Log.Engine.Error(e, $"Export of {unit?.CharacterName} failed");
            }
            if (outcome.Ok) Logging.Log.Engine.Info($"Exported {unit.CharacterName} as {outcome.FileName} (gaps: {string.Join(", ", outcome.Gaps)}; skipped picks: {outcome.SkippedPicks})");
            return outcome;
        }
    }
}
