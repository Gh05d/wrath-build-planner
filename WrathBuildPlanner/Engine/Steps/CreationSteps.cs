using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Pregen;
using WrathBuildPlanner.Core;

namespace WrathBuildPlanner.Engine.Steps {
    /// <summary>
    /// Creation opens with a premade character applied (race, class and every selection fixed).
    /// The page's own "custom character" handler sets SelectedPregenEntity to null; its subscriber
    /// calls SelectPregen(null) and updates the page.
    /// </summary>
    public class LeavePregenStep : IApplyStep {
        public string Name => Messages.Get("step.custom");

        public void Run(ApplyContext context) {
            if (!context.State.IsPregen) return;
            var page = WindowTracker.Window?.m_PhasesList.OfType<CharGenPregenPhaseVM>().FirstOrDefault();
            page?.SelectCreateCustomCharacter();
            if (context.State.IsPregen) context.Controller.SelectPregen(null);
            if (context.State.IsPregen) context.Report.Steps.Add(StepResult.Open(Name, OpenReason.NotSelectable));
        }
    }

    public class RaceStep : IApplyStep {
        public string Name => "Race";

        public void Run(ApplyContext context) {
            var start = context.Build.Start;
            if (start == null || string.IsNullOrWhiteSpace(start.Race)) return;
            string label = Messages.Get("step.race", start.Race);

            var outcome = NameMatcher.Match(start.Race, GameNames.Races());
            if (outcome.Kind != MatchKind.Unique) {
                context.Report.Steps.Add(StepResult.Open(label,
                    outcome.Kind == MatchKind.Ambiguous ? OpenReason.Ambiguous : OpenReason.NotFound, null, outcome.Suggestions));
                return;
            }
            var race = (Kingmaker.Blueprints.Classes.BlueprintRace)outcome.Match.Tag;
            if (context.State.SelectedRace == race) {
                context.Report.Steps.Add(StepResult.Already(label));
            // No CanSelectRace check here: SelectRace.Apply sets it false once any race is chosen (a mercenary
            // starts with one), and LevelUpController.SelectRace removes that choice before adding the new one —
            // the same call the race page makes (IL 2026-10-06). The call itself is the gate.
            } else if (!context.Controller.SelectRace(race)) {
                context.Report.Steps.Add(StepResult.Open(label, OpenReason.NotSelectable));
                return;
            } else {
                context.Report.Steps.Add(StepResult.Applied(label));
            }

            if (string.IsNullOrWhiteSpace(start.RaceBonus) || !context.State.CanSelectRaceStat) return;
            string bonusLabel = Messages.Get("step.race_bonus", start.RaceBonus);
            if (!Vocabulary.TryAttribute(start.RaceBonus, out string canonical)) {
                context.Report.Steps.Add(StepResult.Open(bonusLabel, OpenReason.NotFound));
                return;
            }
            var stat = (StatType)Enum.Parse(typeof(StatType), canonical);
            if (context.State.SelectedRaceStat == stat) context.Report.Steps.Add(StepResult.Already(bonusLabel));
            else if (context.Controller.SelectRaceStat(stat)) context.Report.Steps.Add(StepResult.Applied(bonusLabel));
            else context.Report.Steps.Add(StepResult.Open(bonusLabel, OpenReason.NotSelectable));
        }
    }

    /// <summary>
    /// Point-buy through AddStatPoint / RemoveStatPoint, the calls behind the +/- buttons. Values are the
    /// scores before the racial bonus. Lower first so the refunded points are available for the raises.
    /// </summary>
    public class PointBuyStep : IApplyStep {
        const int MaxSteps = 20;
        public string Name => "Ability scores";

        public void Run(ApplyContext context) {
            var scores = context.Build.Start?.AbilityScores;
            if (scores == null || scores.Count == 0) return;
            if (!context.State.StatsDistribution.Available) return;

            var targets = new Dictionary<StatType, int>();
            foreach (var score in scores) {
                if (!Vocabulary.TryAttribute(score.Key, out string canonical)) continue;
                targets[(StatType)Enum.Parse(typeof(StatType), canonical)] = score.Value;
            }
            int moved = 0;
            foreach (var target in targets) moved += Move(context, target.Key, target.Value, false);
            foreach (var target in targets) moved += Move(context, target.Key, target.Value, true);

            var values = context.State.StatsDistribution.StatValues;
            var missed = targets.Where(t => values[t.Key] != t.Value).Select(t => Messages.Get("step.scores_missed", t.Key, values[t.Key], t.Value)).ToList();
            string label = Messages.Get("step.scores", string.Join(" ", StatTypeHelper.Attributes.Select(a => values[a].ToString())));
            if (missed.Count > 0) context.Report.Steps.Add(StepResult.Open(label, OpenReason.NotSelectable, string.Join(", ", missed)));
            else if (moved == 0) context.Report.Steps.Add(StepResult.Already(label));
            else context.Report.Steps.Add(StepResult.Applied(label));
        }

        // Returns how many points were moved.
        static int Move(ApplyContext context, StatType stat, int target, bool raise) {
            int moved = 0;
            for (int i = 0; i < MaxSteps; i++) {
                var distribution = context.State.StatsDistribution;
                int current = distribution.StatValues[stat];
                if (raise ? current >= target : current <= target) break;
                if (raise ? !distribution.CanAdd(stat) : !distribution.CanRemove(stat)) break;
                bool ok = raise ? context.Controller.AddStatPoint(stat) : context.Controller.RemoveStatPoint(stat);
                if (!ok) break;
                moved++;
            }
            return moved;
        }
    }

    public class AlignmentStep : IApplyStep {
        public string Name => "Alignment";

        public void Run(ApplyContext context) {
            string wanted = context.Build.Start?.Alignment;
            if (string.IsNullOrWhiteSpace(wanted) || !context.State.CanSelectAlignment) return;
            string label = Messages.Get("step.alignment", wanted);
            if (!Vocabulary.TryAlignment(wanted, out string canonical)) {
                context.Report.Steps.Add(StepResult.Open(label, OpenReason.NotFound));
                return;
            }
            var alignment = (Alignment)Enum.Parse(typeof(Alignment), canonical);
            if (context.Controller.Preview.Descriptor.Alignment.ValueRaw == alignment) {
                context.Report.Steps.Add(StepResult.Already(label));
                return;
            }
            if (context.State.IsAlignmentRestricted(alignment)) {
                context.Report.Steps.Add(StepResult.Open(label, OpenReason.NotSelectable, Messages.Get("step.alignment_class")));
                return;
            }
            if (context.Controller.SelectAlignment(alignment)) context.Report.Steps.Add(StepResult.Applied(label));
            else context.Report.Steps.Add(StepResult.Open(label, OpenReason.NotSelectable));
        }
    }
}
