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
                if (step.Status == StepStatus.Applied && step.Detail != null) sb.Append($" ({step.Detail})");
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

        static Kingmaker.EntitySystem.Entities.UnitEntityData Unit(string who) {
            var player = Game.Instance.Player;
            if (who == "main") return player.MainCharacter.Value;
            return player.AllCharacters.Concat(player.RemoteCompanions).FirstOrDefault(u => u.UniqueId == who || u.CharacterName == who);
        }

        /// <summary>Export a character ("main", a UniqueId or a name) the way the settings window's button does.</summary>
        static string Export(string who) {
            var unit = Unit(who);
            if (unit == null) return "NO UNIT " + who;
            var outcome = CharacterExport.Export(unit);
            return outcome.Ok ? $"OK {outcome.FileName} gaps={string.Join(",", outcome.Gaps)} skipped={outcome.SkippedPicks}" : "FAILED " + outcome.Error;
        }

        static string ExportCandidates() =>
            "party: " + string.Join(", ", CharacterExport.Party().Select(u => $"{u.CharacterName}={u.UniqueId}"))
            + " | reserve: " + string.Join(", ", CharacterExport.Reserve().Select(u => $"{u.CharacterName}={u.UniqueId}"));

        /// <summary>The engine facts the character export relies on (verification probe, plan Task 1).</summary>
        static string ExportProbe(string who) {
            var unit = Unit(who);
            if (unit == null) return "NO UNIT " + who;
            var p = unit.Progression;
            var sb = new StringBuilder();
            sb.Append($"{unit.CharacterName} L{p.CharacterLevel} M{p.MythicLevel} race={p.Race?.name}");
            sb.Append(" || order: " + string.Join(" ", p.m_ClassesOrder.Select((c, i) => $"{i + 1}:{c.name}{(c.IsMythic ? "*" : "")}")));
            sb.Append(" || classes: " + string.Join(" ", p.Classes.Select(c => $"{c.CharacterClass.name}={c.Level}[{string.Join("+", c.Archetypes.Select(a => a.name))}]")));
            sb.Append(" || selections:");
            foreach (var kv in p.Selections) {
                var src = kv.Value.Source.Blueprint;
                sb.Append($" {kv.Key.name}<{src?.GetType().Name}:{src?.name}>{{{string.Join(",", kv.Value.SelectionsByLevel.Select(l => l.Key + "=" + string.Join("/", l.Value.Select(f => f.name))))}}}");
            }
            sb.Append(" || params:");
            foreach (var f in p.Features.Enumerable.Where(f => f.Param != null))
                sb.Append($" {f.Blueprint.name}({f.Param.WeaponCategory}{f.Param.SpellSchool}{f.Param.StatType}{f.Param.Blueprint?.name})<{f.m_Source?.name}@{f.SourceLevel}>");
            var granted = unit.Facts.List.SelectMany(fact => fact.Blueprint.ComponentsArray.OfType<Kingmaker.UnitLogic.FactLogic.AddKnownSpell>()
                .Select(c => $"{c.Spell?.name}@{c.SpellLevel}<{fact.Blueprint.name}>")).ToList();
            sb.Append(" || granted: " + string.Join(" ", granted));
            foreach (var book in unit.Descriptor.Spellbooks) {
                var bp = book.Blueprint;
                sb.Append($" || book {bp.name} spont={bp.Spontaneous} perLevel={bp.SpellsPerLevel} known={(bp.SpellsKnown != null ? bp.SpellsKnown.name : "null")} casterLevel={book.CasterLevel}:");
                for (int level = 0; level <= 9; level++) {
                    var known = book.GetKnownSpells(level);
                    if (known.Count > 0) sb.Append($" L{level}[{string.Join(",", known.Select(k => k.Blueprint.name + (k.CopiedFromScroll ? "(scroll)" : "")))}]");
                }
            }
            var player = Game.Instance.Player;
            sb.Append(" || party: " + string.Join(",", player.Party.Select(u => u.CharacterName)));
            sb.Append(" | all: " + string.Join(",", player.AllCharacters.Select(u => u.CharacterName + (u.IsPet ? "(pet)" : ""))));
            sb.Append(" | remote: " + string.Join(",", player.RemoteCompanions.Select(u => u.CharacterName + (u.IsPet ? "(pet)" : ""))));
            return sb.ToString();
        }

        /// <summary>Test helper: fill every still-open feature selection with its first selectable item.</summary>
        static string FillRest() {
            var controller = WindowTracker.Controller;
            if (controller == null) return "NO WINDOW";
            int filled = 0;
            for (int i = 0; i < 40; i++) {
                var selection = controller.State.Selections.FirstOrDefault(s => !s.Selected);
                if (selection == null) break;
                var unit = controller.GetUnit(selection.Selection).Descriptor;
                var item = selection.Selection.ExtractSelectionItems(controller.Unit.Descriptor, controller.Preview.Descriptor)
                    .FirstOrDefault(candidate => selection.Selection.CanSelect(unit, controller.State, selection, candidate));
                var spellsBefore = controller.State.SpellSelections.Sum(d => d.LevelCount.Sum(l => l?.SpellSelections?.Count(x => x != null) ?? 0) + (d.ExtraSelected?.Count(x => x != null) ?? 0));
                if (item == null || !controller.SelectFeature(selection, item)) break;
                int spellsAfter = controller.State.SpellSelections.Sum(d => d.LevelCount.Sum(l => l?.SpellSelections?.Count(x => x != null) ?? 0) + (d.ExtraSelected?.Count(x => x != null) ?? 0));
                if (spellsAfter < spellsBefore) Logging.Log.Engine.Warn($"FillRest: selecting '{item.Feature?.name}' dropped {spellsBefore - spellsAfter} spell pick(s)");
                filled++;
            }
            return "filled " + filled;
        }

        /// <summary>
        /// Class names resolved against a second language pack while the game keeps its own language —
        /// the mechanism GameNames.English relies on, checkable without changing the game's settings.
        /// </summary>
        static string NamesIn(string locale) {
            var target = (Kingmaker.Localization.Shared.Locale)System.Enum.Parse(typeof(Kingmaker.Localization.Shared.Locale), locale);
            var pack = Kingmaker.Localization.LocalizationManager.LoadPack(target);
            if (pack == null) return "NO PACK";
            var classes = Game.Instance.BlueprintRoot.Progression.CharacterClasses.Take(4)
                .Select(c => $"{c.Name}={c.LocalizedName.LoadString(pack, target)}");
            return $"current={Kingmaker.Localization.LocalizationManager.CurrentLocale} " + string.Join(", ", classes);
        }

        /// <summary>Assign a build to the unit in the open window (file name, or "-" to clear).</summary>
        static string Assign(string buildFileName) {
            Main.Library.Reload();
            var unit = WindowTracker.Controller?.Unit;
            Persistence.AssignmentStore.Set(unit, buildFileName == "-" ? null : buildFileName);
            UI.PlannerController.Instance?.Bar?.Refresh();
            return "assigned " + Persistence.AssignmentStore.Get(unit);
        }

        /// <summary>The same path as the bar's Apply button; returns the rendered result text on one line.</summary>
        static string PressApply() {
            var bar = UI.PlannerController.Instance?.Bar;
            if (bar == null) return "NO BAR";
            var before = bar.LastReport;
            bar.Apply();
            if (bar.LastReport == before && UI.PlannerController.Instance?.HasPendingAction == true) return "DEFERRED (read the result with BarText)";
            return bar.LastReport == null ? "NO REPORT" : UI.BuildBar.Render(bar.LastReport).Replace("\n", " || ");
        }

        /// <summary>What the bar shows: name field, Change button label, details panel (open flag and text).</summary>
        static string BarText() {
            var bar = UI.PlannerController.Instance?.Bar;
            return bar == null ? "NO BAR" : bar.DescribeForTests().Replace("\n", " || ");
        }

        /// <summary>Page titles of the open window's selections as "title=English" pairs.</summary>
        static string PageTitles() {
            var controller = WindowTracker.Controller;
            if (controller == null) return "NO WINDOW";
            return string.Join(" | ", controller.State.Selections.Select(s => {
                string title = Kingmaker.UI.MVVM._VM.CharGen.Phases.FeatureSelector.FeatureSelectionExtensions.GetMenuLabel(s);
                return $"{title}={GameNames.EnglishPageTitle(s, title)}";
            }));
        }

        /// <summary>Writes names.json into the mod folder for tools/export-names.sh. English game only.</summary>
        static string ExportNames() => NameExport.Write(Path.Combine(Main.ModPath, "names.json"));

        static string ToggleWindow() {
            UI.BuildsWindow.Toggle();
            return UI.BuildsWindow.IsOpen ? "open" : "closed";
        }

        static string LibraryScroll() => UI.BuildsWindow.LibraryScrollState();

        /// <summary>Import text given as base64 (DevBridge arguments cannot contain spaces).</summary>
        static string ImportText(string base64) {
            string text = Encoding.UTF8.GetString(System.Convert.FromBase64String(base64));
            return UI.BuildsWindow.ImportText(text);
        }
    }
}
