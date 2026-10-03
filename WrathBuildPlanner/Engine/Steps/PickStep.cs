using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.UnitLogic.Class.LevelUp;
using WrathBuildPlanner.Core;
using WrathBuildPlanner.Models;

namespace WrathBuildPlanner.Engine.Steps {
    public class PickStep : IApplyStep {
        const int MaxPasses = 80;
        readonly Func<ApplyContext, bool> interleave;

        public PickStep(Func<ApplyContext, bool> interleave = null) {
            this.interleave = interleave;
        }

        public string Name => "Picks";

        class Pending {
            public PickEntry Entry;
            public List<string> Chain;
            public int Position;
            public IFeatureSelectionItem Parent;
            public StepResult Failure;
            public bool Done;
            public bool WasAlreadySet;
        }

        public static List<PickEntry> Picks(ApplyContext context) =>
            (context.MythicRow != null ? context.MythicRow.Picks : context.Row?.Picks) ?? new List<PickEntry>();

        public void Run(ApplyContext context) {
            var pending = Picks(context).Select(e => new Pending { Entry = e, Chain = new List<string>(e.Pick) }).ToList();
            foreach (var pick in pending) Resume(context, pick);

            // Picks to a fixed point, then spells to a fixed point, and again while spells still change something.
            // Order matters: the game can drop spell picks when a feature is selected after them, and the spell
            // pass re-selects whatever is missing.
            for (int pass = 0; pass < MaxPasses; pass++) {
                bool picked = true;
                for (int round = 0; picked && round < MaxPasses; round++) {
                    picked = false;
                    foreach (var pick in pending.Where(p => !p.Done)) {
                        var before = SpellStep.PickedNow(context);
                        if (!TryAdvance(context, pick)) continue;
                        picked = true;
                        var lost = before.Except(SpellStep.PickedNow(context)).ToList();
                        if (lost.Count > 0)
                            Logging.Log.Engine.Warn($"selecting '{pick.Chain[pick.Position - 1]}' made the game drop spell picks: {string.Join(", ", lost)} (re-selected by the spell pass)");
                    }
                }
                bool spells = false;
                for (int round = 0; interleave != null && round < MaxPasses && interleave(context); round++) spells = true;
                if (!spells) break;
            }

            foreach (var pick in pending) {
                if (pick.WasAlreadySet) context.Report.Steps.Add(StepResult.Already(pick.Entry.Label));
                else if (pick.Done) context.Report.Steps.Add(StepResult.Applied(pick.Entry.Label));
                else context.Report.Steps.Add(pick.Failure ?? StepResult.Open(pick.Entry.Label, OpenReason.SelectionMissing));
            }
        }

        // Links of the chain that are already selected in this level are skipped, so a second apply (or a pick the
        // player made by hand) is reported as set instead of as a missing selection.
        static void Resume(ApplyContext context, Pending pick) {
            int matched = Selected(context, pick.Entry.In, pick.Chain, out var last);
            if (matched == 0 && pick.Chain.Count == 1 && NameMatcher.TrySplitParenChain(pick.Chain[0], out string head, out string tail)) {
                var split = new List<string> { head, tail };
                int splitMatched = Selected(context, pick.Entry.In, split, out var splitLast);
                if (splitMatched > 0) {
                    pick.Chain = split;
                    matched = splitMatched;
                    last = splitLast;
                }
            }
            if (matched == 0) return;
            pick.Position = matched;
            pick.Parent = last;
            if (matched >= pick.Chain.Count) {
                pick.Done = true;
                pick.WasAlreadySet = true;
            }
        }

        // How many leading links of the chain are selected already, and the item of the last one.
        static int Selected(ApplyContext context, string scope, List<string> chain, out IFeatureSelectionItem last) {
            last = null;
            var selected = context.State.Selections.Where(s => s.Selected && s.SelectedItem != null).ToList();
            int matched = 0;
            foreach (string name in chain) {
                IEnumerable<FeatureSelectionState> pool;
                if (matched > 0) {
                    var parent = last;
                    pool = selected.Where(s => ReferenceEquals(s.Selection, parent.Feature));
                } else if (scope != null) {
                    var outcome = NameMatcher.Match(scope, selected.Select(GameNames.OfSelection).ToList());
                    if (outcome.Kind == MatchKind.None) return 0;
                    pool = outcome.Kind == MatchKind.Unique
                        ? new List<FeatureSelectionState> { (FeatureSelectionState)outcome.Match.Tag }
                        : outcome.Tied.Select(t => (FeatureSelectionState)t.Tag).ToList();
                } else {
                    pool = selected;
                }
                var hit = pool.FirstOrDefault(s =>
                    NameMatcher.Match(name, new List<NameCandidate> { GameNames.OfItem(s.SelectedItem) }).Kind == MatchKind.Unique);
                if (hit == null) break;
                last = hit.SelectedItem;
                matched++;
            }
            return matched;
        }

        // One link of the chain. Returns true when a selection was made.
        bool TryAdvance(ApplyContext context, Pending pick) {
            var state = context.State;
            var open = state.Selections.Where(s => !s.Selected).ToList();
            string wanted = pick.Chain[pick.Position];
            string label = pick.Entry.Label;

            List<FeatureSelectionState> targets;
            if (pick.Position > 0) {
                // The child selection of a picked item is the open selection whose Selection is that feature.
                targets = open.Where(s => ReferenceEquals(s.Selection, pick.Parent.Feature)).ToList();
                if (targets.Count == 0) {
                    pick.Failure = StepResult.Open(label, OpenReason.SelectionMissing, $"'{pick.Chain[pick.Position - 1]}' offers no further choice");
                    return false;
                }
            } else if (pick.Entry.In != null) {
                var outcome = NameMatcher.Match(pick.Entry.In, open.Select(GameNames.OfSelection).ToList());
                if (outcome.Kind == MatchKind.None) {
                    pick.Failure = StepResult.Open(label, OpenReason.SelectionMissing, null, outcome.Suggestions);
                    return false;
                }
                // Several open selections with the same name (two "Feat" slots) are tried in order.
                targets = outcome.Kind == MatchKind.Unique
                    ? new List<FeatureSelectionState> { (FeatureSelectionState)outcome.Match.Tag }
                    : outcome.Tied.Select(t => (FeatureSelectionState)t.Tag).ToList();
            } else {
                targets = open;
            }

            // Resolve the name in every target; keep unique hits.
            var hits = new List<KeyValuePair<FeatureSelectionState, IFeatureSelectionItem>>();
            MatchOutcome lastOutcome = null;
            foreach (var selection in targets) {
                var outcome = NameMatcher.Match(wanted, Items(context, selection).Select(GameNames.OfItem).ToList());
                lastOutcome = Prefer(lastOutcome, outcome);
                if (outcome.Kind == MatchKind.Unique)
                    hits.Add(new KeyValuePair<FeatureSelectionState, IFeatureSelectionItem>(selection, (IFeatureSelectionItem)outcome.Match.Tag));
            }

            if (hits.Count == 0) {
                // "Weapon Focus (Greatsword)" written as one name: retry as a chain.
                if (pick.Chain.Count == 1 && NameMatcher.TrySplitParenChain(wanted, out string head, out string tail)) {
                    pick.Chain = new List<string> { head, tail };
                    return TryAdvance(context, pick);
                }
                if (lastOutcome != null && lastOutcome.Kind == MatchKind.Ambiguous)
                    pick.Failure = StepResult.Open(label, OpenReason.Ambiguous, string.Join(", ", lastOutcome.Tied.Select(t => t.Display)));
                else if (targets.Count > 0)
                    pick.Failure = StepResult.Open(label, OpenReason.NotFound, null, lastOutcome?.Suggestions);
                else if (pick.Failure == null)
                    // Nothing open at all. Keep a more specific reason from an earlier pass if there is one.
                    pick.Failure = StepResult.Open(label, OpenReason.SelectionMissing);
                return false;
            }

            // A bare name must not be guessed across different selections.
            if (pick.Position == 0 && pick.Entry.In == null) {
                var blueprints = hits.Select(h => h.Key.Selection).Distinct().ToList();
                if (blueprints.Count > 1) {
                    pick.Failure = StepResult.Open(label, OpenReason.Ambiguous,
                        string.Join(", ", hits.Select(h => GameNames.OfSelection(h.Key).Display).Distinct()));
                    return false;
                }
            }

            foreach (var hit in hits) {
                var unit = context.Controller.GetUnit(hit.Key.Selection).Descriptor;
                if (!hit.Key.Selection.CanSelect(unit, state, hit.Key, hit.Value)) {
                    pick.Failure = StepResult.Open(label, OpenReason.NotSelectable, Unmet(hit.Key, hit.Value, context));
                    continue;
                }
                if (!context.Controller.SelectFeature(hit.Key, hit.Value)) {
                    pick.Failure = StepResult.Open(label, OpenReason.NotSelectable);
                    continue;
                }
                pick.Parent = hit.Value;
                pick.Failure = null;
                pick.Position++;
                pick.Done = pick.Position >= pick.Chain.Count;
                return true;
            }
            return false;
        }

        // An ambiguity is more useful to report than "not found"; a unique hit beats both.
        static MatchOutcome Prefer(MatchOutcome current, MatchOutcome next) {
            if (current == null) return next;
            if (next.Kind == MatchKind.Ambiguous && current.Kind == MatchKind.None) return next;
            if (next.Kind == MatchKind.None && current.Kind == MatchKind.None && next.Suggestions.Count > current.Suggestions.Count) return next;
            return current;
        }

        // Same enumeration the UI uses for a selection page.
        static IEnumerable<IFeatureSelectionItem> Items(ApplyContext context, FeatureSelectionState selection) =>
            selection.Selection.ExtractSelectionItems(context.Controller.Unit.Descriptor, context.Controller.Preview.Descriptor);

        // The game's own wording for prerequisites that are not met.
        static string Unmet(FeatureSelectionState selection, IFeatureSelectionItem item, ApplyContext context) {
            if (item.Feature == null) return null;
            var unit = context.Controller.GetUnit(selection.Selection).Descriptor;
            var texts = new List<string>();
            foreach (var prerequisite in item.Feature.GetComponents<Prerequisite>()) {
                if (prerequisite.Check(selection, unit, context.State)) continue;
                string text = prerequisite.GetUIText(unit);
                if (!string.IsNullOrEmpty(text)) texts.Add(text);
            }
            return texts.Count > 0 ? string.Join("; ", texts) : null;
        }
    }
}
