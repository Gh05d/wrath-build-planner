using System;
using System.Collections.Generic;
using Kingmaker.EntitySystem.Stats;
using WrathBuildPlanner.Core;

namespace WrathBuildPlanner.Engine.Steps {
    /// <summary>
    /// Spends skill points in the build's priority order, one rank per skill per pass. SpendSkillPoint.Check
    /// caps a skill's rank at the character level, so a pass never over-spends. Without a list, skills stay open.
    /// </summary>
    public class SkillStep : IApplyStep {
        const int MaxPasses = 40;
        public string Name => "Skills";

        public void Run(ApplyContext context) {
            var wanted = LevelPlanner.SkillsFor(context.Build, context.Row);
            if (wanted.Count == 0 || context.State.SkillPointsRemaining <= 0) return;

            var order = new List<StatType>();
            foreach (string name in wanted) {
                if (Vocabulary.TrySkill(name, out string canonical)) order.Add((StatType)Enum.Parse(typeof(StatType), canonical));
            }

            int spent = 0;
            bool progress = true;
            // SpendSkillPoint returning true always costs a point, so this ends; the cap is a seat belt.
            for (int pass = 0; pass < MaxPasses && progress && context.State.SkillPointsRemaining > 0; pass++) {
                progress = false;
                foreach (var skill in order) {
                    if (context.State.SkillPointsRemaining <= 0) break;
                    if (!context.Controller.SpendSkillPoint(skill)) continue;
                    spent++;
                    progress = true;
                }
            }

            int left = context.State.SkillPointsRemaining;
            string label = Messages.Get("step.skills", spent);
            if (left == 0) context.Report.Steps.Add(StepResult.Applied(label));
            else context.Report.Steps.Add(StepResult.Open(label, OpenReason.LeftToPlayer, Messages.Get("step.skills_left", left)));
        }
    }
}
