using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.UnitLogic.Class.LevelUp;
using WrathBuildPlanner.Core;
using WrathBuildPlanner.Engine.Steps;
using WrathBuildPlanner.Models;

namespace WrathBuildPlanner.Engine {
    public class ApplyContext {
        public LevelUpController Controller;
        public BuildFile Build;
        public LevelRow Row;
        public MythicRow MythicRow;
        public ApplyReport Report;

        /// <summary>Always read fresh: any controller call can rebuild the state object.</summary>
        public LevelUpState State => Controller.State;
    }

    public interface IApplyStep {
        string Name { get; }
        void Run(ApplyContext context);
    }

    /// <summary>
    /// Applies one level of a build to the open window through the calls the game's own UI makes on click.
    /// Never commits. Each step is guarded so one failure cannot stop the others or reach the game.
    /// </summary>
    public static class LevelApplier {
        public static ApplyReport Apply(LevelUpController controller, BuildFile build) {
            var report = new ApplyReport();
            if (controller == null || controller.State == null || build == null) {
                report.NoWindow = true;
                return report;
            }

            var mode = controller.State.Mode;
            var context = new ApplyContext { Controller = controller, Build = build, Report = report };
            report.Mythic = mode == LevelUpState.CharBuildMode.Mythic;
            if (report.Mythic) {
                report.Level = controller.State.NextMythicLevel;
                context.MythicRow = LevelPlanner.MythicRowFor(build, report.Level);
                report.NoRow = context.MythicRow == null;
            } else {
                report.Level = controller.State.NextCharacterLevel;
                context.Row = LevelPlanner.RowFor(build, report.Level);
                report.NoRow = context.Row == null;
            }
            Logging.Log.Engine.Info($"apply '{build.Name}' mode={mode} level={report.Level} row={(report.NoRow ? "none" : "found")}");
            if (report.NoRow) return report;

            report.History = History(context);

            foreach (var step in StepsFor(mode)) {
                try {
                    step.Run(context);
                } catch (Exception e) {
                    Logging.Log.Engine.Error(e, $"step {step.Name} failed");
                    report.Steps.Add(StepResult.Open(step.Name, OpenReason.InternalError, e.Message));
                }
            }

            foreach (var result in report.Steps)
                Logging.Log.Engine.Info($"  {result.Status} {result.Label}{(result.Status == StepStatus.Open ? " (" + result.Reason + (result.Detail != null ? ": " + result.Detail : "") + ")" : "")}");
            return report;
        }

        static IEnumerable<IApplyStep> StepsFor(LevelUpState.CharBuildMode mode) {
            if (mode == LevelUpState.CharBuildMode.Mythic) {
                yield return new PickStep();
                yield break;
            }
            if (mode == LevelUpState.CharBuildMode.CharGen) {
                yield return new LeavePregenStep();
                yield return new RaceStep();
            }
            yield return new ClassStep();
            yield return new ArchetypeStep();
            if (mode == LevelUpState.CharBuildMode.CharGen) {
                yield return new PointBuyStep();
                yield return new AlignmentStep();
            } else {
                yield return new AttributePointStep();
            }
            yield return new SkillStep();
            yield return new PickStep();
        }

        static HistoryCheck History(ApplyContext context) {
            if (context.Row == null) return new HistoryCheck();
            var classes = GameNames.Classes();
            Func<string, string> canonical = name => {
                var outcome = NameMatcher.Match(name, classes);
                return outcome.Kind == MatchKind.Unique ? outcome.Match.Display : name;
            };
            var expected = LevelPlanner.ExpectedClassLevels(context.Build, context.Report.Level, canonical);
            var actual = context.Controller.Unit.Progression.Classes
                .Where(c => !c.CharacterClass.IsMythic)
                .ToDictionary(c => c.CharacterClass.Name, c => c.Level);
            return LevelPlanner.CompareHistory(expected, actual);
        }
    }
}
