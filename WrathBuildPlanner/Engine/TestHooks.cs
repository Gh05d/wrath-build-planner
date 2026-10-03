using System.IO;
using System.Linq;
using System.Text;
using Kingmaker;
using Kingmaker.EntitySystem.Stats;
using WrathBuildPlanner.Core;

namespace WrathBuildPlanner.Engine {
    /// <summary>
    /// Static entry points for in-game tests through DevBridge
    /// (invoke WrathBuildPlanner.Engine.TestHooks.Apply fighter.json). No UI involved.
    /// </summary>
    static class TestHooks {
        static string Apply(string buildFileName) {
            if (WindowTracker.Controller == null) return "NO WINDOW";
            string path = Path.Combine(Main.ModPath, "Builds", buildFileName);
            if (!File.Exists(path)) return "NO FILE " + path;
            var parsed = BuildParser.Parse(File.ReadAllText(path));
            if (!parsed.Ok) return "PARSE " + string.Join("; ", parsed.Issues.Select(i => i.Message));
            var issues = BuildValidator.Validate(parsed.Build, GameNames.Known).Where(i => i.IsError).ToList();
            if (issues.Count > 0) return "INVALID " + string.Join("; ", issues.Select(i => i.Message));

            var report = LevelApplier.Apply(WindowTracker.Controller, parsed.Build);
            if (report.NoWindow) return "NO WINDOW";
            if (report.NoRow) return $"NO ROW level={report.Level} mythic={report.Mythic}";
            var sb = new StringBuilder();
            sb.Append($"level={report.Level} applied={report.AppliedCount} open={report.OpenCount}");
            if (report.History != null && report.History.Comparable && !report.History.Matches)
                sb.Append($" HISTORY expected[{report.History.Expected}] actual[{report.History.Actual}]");
            foreach (var step in report.Steps) {
                sb.Append($" || {step.Status} {step.Label}");
                if (step.Status == StepStatus.Open) sb.Append($" ({step.Reason}{(step.Detail != null ? ": " + step.Detail : "")}{(step.Suggestions.Count > 0 ? "; similar: " + string.Join(", ", step.Suggestions) : "")})");
            }
            return sb.ToString();
        }

        static string Describe() {
            var unit = Game.Instance.Player.MainCharacter.Value;
            var sb = new StringBuilder();
            sb.Append($"{unit.CharacterName} L{unit.Progression.CharacterLevel} mythic {unit.Progression.MythicLevel} | ");
            sb.Append(string.Join(", ", unit.Progression.Classes.Select(c =>
                $"{c.CharacterClass.name} {c.Level}{(c.Archetypes.Count > 0 ? " (" + string.Join("+", c.Archetypes.Select(a => a.name)) + ")" : "")}")));
            sb.Append(" | " + string.Join(" ", StatTypeHelper.Attributes.Select(a => $"{a.ToString().Substring(0, 3)} {unit.Stats.GetStat(a).BaseValue}")));
            sb.Append(" | skills " + string.Join(" ", StatTypeHelper.Skills.Select(s => $"{s.ToString().Substring(5)} {unit.Stats.GetStat(s).BaseValue}")));
            foreach (var book in unit.Descriptor.Spellbooks) {
                sb.Append($" | {book.Blueprint.name}:");
                for (int level = 1; level <= 9; level++) {
                    var known = book.GetKnownSpells(level);
                    if (known.Count > 0) sb.Append($" L{level}[{string.Join(", ", known.Select(k => k.Blueprint.Name))}]");
                }
            }
            sb.Append(" | selections: " + string.Join("; ", unit.Progression.Selections.SelectMany(kv =>
                kv.Value.SelectionsByLevel.SelectMany(l => l.Value.Select(f => $"{kv.Key.name}@{l.Key}={f.name}")))));
            sb.Append(" | party: " + string.Join(", ", Game.Instance.Player.PartyAndPets.Select(p => p.Blueprint.name)));
            return sb.ToString();
        }
    }
}
