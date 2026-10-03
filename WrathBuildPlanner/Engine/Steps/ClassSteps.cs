using System;
using System.Linq;
using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem.Stats;
using WrathBuildPlanner.Core;

namespace WrathBuildPlanner.Engine.Steps {
    /// <summary>
    /// The class is always set from the build: the window often preselects one. SelectClass(cls, true, false)
    /// is the call behind a click on a class and replaces an earlier class choice.
    /// </summary>
    public class ClassStep : IApplyStep {
        public string Name => "Class";

        public void Run(ApplyContext context) {
            string wanted = context.Row.Class;
            if (string.IsNullOrWhiteSpace(wanted)) return;
            string label = Messages.Get("step.class", wanted);

            var outcome = NameMatcher.Match(wanted, GameNames.Classes());
            if (outcome.Kind != MatchKind.Unique) {
                context.Report.Steps.Add(StepResult.Open(label,
                    outcome.Kind == MatchKind.Ambiguous ? OpenReason.Ambiguous : OpenReason.NotFound, null, outcome.Suggestions));
                return;
            }
            var cls = (BlueprintCharacterClass)outcome.Match.Tag;
            if (context.State.SelectedClass == cls) {
                context.Report.Steps.Add(StepResult.Already(label));
                return;
            }
            context.Controller.SelectClass(cls, true, false);
            if (context.State.SelectedClass == cls) context.Report.Steps.Add(StepResult.Applied(label));
            else context.Report.Steps.Add(StepResult.Open(label, OpenReason.NotSelectable, Messages.Get("step.class_denied")));
        }
    }

    /// <summary>Must run before picks and spells: an archetype swaps selections and can swap the spellbook.</summary>
    public class ArchetypeStep : IApplyStep {
        public string Name => "Archetype";

        public void Run(ApplyContext context) {
            string wanted = context.Row.Archetype;
            if (string.IsNullOrWhiteSpace(wanted)) return;
            string label = Messages.Get("step.archetype", wanted);
            var cls = context.State.SelectedClass;
            if (cls == null) {
                context.Report.Steps.Add(StepResult.Open(label, OpenReason.SelectionMissing, Messages.Get("step.no_class")));
                return;
            }

            var outcome = NameMatcher.Match(wanted, cls.Archetypes.Select(GameNames.Of).ToList());
            if (outcome.Kind != MatchKind.Unique) {
                context.Report.Steps.Add(StepResult.Open(label,
                    outcome.Kind == MatchKind.Ambiguous ? OpenReason.Ambiguous : OpenReason.NotFound, null, outcome.Suggestions));
                return;
            }
            var archetype = (BlueprintArchetype)outcome.Match.Tag;
            var classData = context.Controller.Preview.Progression.GetClassData(cls);
            if (classData != null && classData.Archetypes.Contains(archetype)) {
                context.Report.Steps.Add(StepResult.Already(label));
                return;
            }
            if (context.Controller.AddArchetype(archetype)) context.Report.Steps.Add(StepResult.Applied(label));
            else context.Report.Steps.Add(StepResult.Open(label, OpenReason.NotSelectable, Messages.Get("step.archetype_first")));
        }
    }

    /// <summary>The point granted every fourth level (SpendAttributePoint, the call behind the + button outside point-buy).</summary>
    public class AttributePointStep : IApplyStep {
        public string Name => "Attribute point";

        public void Run(ApplyContext context) {
            string wanted = context.Row.AbilityPoint;
            if (string.IsNullOrWhiteSpace(wanted) || context.State.AttributePoints <= 0) return;
            string label = Messages.Get("step.point", wanted);
            if (!Vocabulary.TryAttribute(wanted, out string canonical)) {
                context.Report.Steps.Add(StepResult.Open(label, OpenReason.NotFound));
                return;
            }
            var stat = (StatType)Enum.Parse(typeof(StatType), canonical);
            if (context.Controller.SpendAttributePoint(stat)) context.Report.Steps.Add(StepResult.Applied(label));
            else context.Report.Steps.Add(StepResult.Open(label, OpenReason.NotSelectable));
        }
    }
}
