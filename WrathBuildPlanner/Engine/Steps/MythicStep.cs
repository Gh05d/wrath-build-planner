using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Root;
using Kingmaker.DLC;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Class;
using WrathBuildPlanner.Core;

namespace WrathBuildPlanner.Engine.Steps {
    /// <summary>
    /// The mythic class is preselected while the game dictates the next one (TrySelectNextMythicClass).
    /// Where the player chooses a path (rank 3), the build's `path` is applied only if the game offers it.
    /// SelectClass itself checks nothing but prerequisites: the story/DLC gate lives in the page's view model
    /// (CharGenMythicSelectorItemVM: IsMythicClassUnlocked, then not DLC-restricted and MeetsPrerequisites),
    /// so the same three checks are made here. A path that is not offered is left to the player.
    /// </summary>
    public class MythicPathStep : IApplyStep {
        public string Name => Messages.Get("step.path_open");

        public void Run(ApplyContext context) {
            if (context.State.IsClassSelected) return;
            if (CharGenClassPhaseVM.TrySelectNextMythicClass(context.Controller)) return;

            string wanted = context.MythicRow.Path;
            if (string.IsNullOrWhiteSpace(wanted)) {
                context.Report.Steps.Add(StepResult.Open(Name, OpenReason.LeftToPlayer));
                return;
            }
            string label = Messages.Get("step.path", wanted);
            var paths = Game.Instance.BlueprintRoot.Progression.CharacterMythics.Select(GameNames.Of).ToList();
            var outcome = NameMatcher.Match(wanted, paths);
            if (outcome.Kind != MatchKind.Unique) {
                context.Report.Steps.Add(StepResult.Open(label,
                    outcome.Kind == MatchKind.Ambiguous ? OpenReason.Ambiguous : OpenReason.NotFound, null, outcome.Suggestions));
                return;
            }
            var path = (BlueprintCharacterClass)outcome.Match.Tag;
            if (!IsOffered(context, path)) {
                context.Report.Steps.Add(StepResult.Open(label, OpenReason.LeftToPlayer, Messages.Get("step.path_locked")));
                return;
            }
            context.Controller.SelectClass(path, true, false);
            if (context.State.SelectedClass == path) context.Report.Steps.Add(StepResult.Applied(label));
            else context.Report.Steps.Add(StepResult.Open(label, OpenReason.LeftToPlayer, Messages.Get("step.path_locked")));
        }

        public static bool IsOffered(ApplyContext context, BlueprintCharacterClass path) =>
            BlueprintRoot.Instance.MythicsSettings.IsMythicClassUnlocked(path)
            && !path.IsDlcRestricted()
            && path.MeetsPrerequisites(context.Controller.Unit, context.State, false);
    }
}
