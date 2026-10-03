using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints.Classes;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Class;
using WrathBuildPlanner.Core;

namespace WrathBuildPlanner.Engine.Steps {
    /// <summary>
    /// The mythic class is preselected while the game dictates the next one (TrySelectNextMythicClass).
    /// Where the player chooses a path (rank 3), the build's `path` is applied if the game accepts it;
    /// otherwise it is left to the player and the remaining mythic picks still run.
    /// </summary>
    public class MythicPathStep : IApplyStep {
        public string Name => "Mythic path";

        public void Run(ApplyContext context) {
            if (context.State.IsClassSelected) return;
            if (CharGenClassPhaseVM.TrySelectNextMythicClass(context.Controller)) return;

            string wanted = context.MythicRow.Path;
            if (string.IsNullOrWhiteSpace(wanted)) {
                context.Report.Steps.Add(StepResult.Open(Name, OpenReason.LeftToPlayer));
                return;
            }
            string label = "Mythic path: " + wanted;
            var paths = Game.Instance.BlueprintRoot.Progression.CharacterMythics.Select(GameNames.Of).ToList();
            var outcome = NameMatcher.Match(wanted, paths);
            if (outcome.Kind != MatchKind.Unique) {
                context.Report.Steps.Add(StepResult.Open(label,
                    outcome.Kind == MatchKind.Ambiguous ? OpenReason.Ambiguous : OpenReason.NotFound, null, outcome.Suggestions));
                return;
            }
            var path = (BlueprintCharacterClass)outcome.Match.Tag;
            context.Controller.SelectClass(path, true, false);
            if (context.State.SelectedClass == path) context.Report.Steps.Add(StepResult.Applied(label));
            else context.Report.Steps.Add(StepResult.Open(label, OpenReason.LeftToPlayer, "the game does not offer this path now"));
        }
    }
}
