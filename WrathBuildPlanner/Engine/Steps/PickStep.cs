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
        }

        public static List<PickEntry> Picks(ApplyContext context) =>
            (context.MythicRow != null ? context.MythicRow.Picks : context.Row?.Picks) ?? new List<PickEntry>();

        public void Run(ApplyContext context) {
            var pending = Picks(context).Select(e => new Pending { Entry = e, Chain = new List<string>(e.Pick) }).ToList();

            for (int pass = 0; pass < MaxPasses; pass++) {
                bool changed = false;
                foreach (var pick in pending.Where(p => !p.Done)) {
                    if (TryAdvance(context, pick)) changed = true;
                }
                if (interleave != null && interleave(context)) changed = true;
                if (!changed) break;
            }

            foreach (var pick in pending) {
                if (pick.Done) context.Report.Steps.Add(StepResult.Applied(pick.Entry.Label));
                else context.Report.Steps.Add(pick.Failure ?? StepResult.Open(pick.Entry.Label, OpenReason.SelectionMissing));
            }
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
