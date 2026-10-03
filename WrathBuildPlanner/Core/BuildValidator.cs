using System.Collections.Generic;
using System.Linq;
using WrathBuildPlanner.Models;

namespace WrathBuildPlanner.Core {
    public interface IKnownNames {
        bool ClassExists(string name);
        bool RaceExists(string name);
    }

    /// <summary>Import-time checks. Feats and spells are not checked here: whether they can be taken depends on the character.</summary>
    public static class BuildValidator {
        public const int SupportedFormat = 1;

        public static List<ImportIssue> Validate(BuildFile build, IKnownNames names) {
            var issues = new List<ImportIssue>();

            if (build.Format != SupportedFormat)
                issues.Add(ImportIssue.Error("format", Messages.Get("validate.format", build.Format, SupportedFormat)));
            if (string.IsNullOrWhiteSpace(build.Name))
                issues.Add(ImportIssue.Error("name", Messages.Get("validate.no_name")));

            var levels = build.Levels ?? new List<LevelRow>();
            var mythic = build.Mythic ?? new List<MythicRow>();
            if (levels.Count == 0 && mythic.Count == 0 && build.Start == null)
                issues.Add(ImportIssue.Error("file", Messages.Get("validate.nothing")));
            if (build.Start != null && !levels.Any(r => r != null && r.Level == 1))
                issues.Add(ImportIssue.Warning("start", Messages.Get("validate.start_without_level1")));

            CheckStart(build.Start, names, issues);
            CheckSkills(build.Skills, "skills", issues);
            CheckLevels(levels, names, issues);
            CheckMythic(mythic, issues);
            return issues;
        }

        static void CheckStart(StartBlock start, IKnownNames names, List<ImportIssue> issues) {
            if (start == null) return;
            if (!string.IsNullOrWhiteSpace(start.Race) && names != null && !names.RaceExists(start.Race))
                issues.Add(ImportIssue.Error("start.race", Messages.Get("validate.race", start.Race)));
            if (!string.IsNullOrWhiteSpace(start.RaceBonus) && !Vocabulary.TryAttribute(start.RaceBonus, out _))
                issues.Add(ImportIssue.Error("start.raceBonus", Messages.Get("validate.not_attribute", start.RaceBonus)));
            if (!string.IsNullOrWhiteSpace(start.Alignment) && !Vocabulary.TryAlignment(start.Alignment, out _))
                issues.Add(ImportIssue.Error("start.alignment", Messages.Get("validate.alignment", start.Alignment)));
            if (start.AbilityScores == null) return;
            foreach (var score in start.AbilityScores) {
                if (!Vocabulary.TryAttribute(score.Key, out _))
                    issues.Add(ImportIssue.Error("start.abilityScores", Messages.Get("validate.not_attribute", score.Key)));
                else if (score.Value < 7 || score.Value > 18)
                    issues.Add(ImportIssue.Error("start.abilityScores", Messages.Get("validate.score_range", score.Key, score.Value)));
            }
        }

        static void CheckSkills(List<string> skills, string where, List<ImportIssue> issues) {
            if (skills == null) return;
            foreach (string skill in skills) {
                if (!Vocabulary.TrySkill(skill, out _))
                    issues.Add(ImportIssue.Error(where, Messages.Get("validate.skill", skill)));
            }
        }

        static void CheckLevels(List<LevelRow> levels, IKnownNames names, List<ImportIssue> issues) {
            int previous = 0;
            var seen = new HashSet<int>();
            foreach (var row in levels) {
                if (row == null) {
                    issues.Add(ImportIssue.Error("levels", Messages.Get("validate.empty_row", "levels")));
                    continue;
                }
                string where = $"levels[level {row.Level}]";
                if (row.Level < 1 || row.Level > 20)
                    issues.Add(ImportIssue.Error(where, Messages.Get("validate.level_range", row.Level)));
                if (!seen.Add(row.Level))
                    issues.Add(ImportIssue.Error(where, Messages.Get("validate.level_twice", row.Level)));
                else if (row.Level < previous)
                    issues.Add(ImportIssue.Error(where, Messages.Get("validate.level_order", row.Level, previous)));
                previous = row.Level > previous ? row.Level : previous;

                if (string.IsNullOrWhiteSpace(row.Class))
                    issues.Add(ImportIssue.Error(where, Messages.Get("validate.no_class")));
                else if (names != null && !names.ClassExists(row.Class))
                    issues.Add(ImportIssue.Error(where, Messages.Get("validate.class", row.Class)));

                if (!string.IsNullOrWhiteSpace(row.AbilityPoint)) {
                    if (!Vocabulary.TryAttribute(row.AbilityPoint, out _))
                        issues.Add(ImportIssue.Error(where, Messages.Get("validate.not_attribute", row.AbilityPoint)));
                    else if (row.Level % 4 != 0)
                        issues.Add(ImportIssue.Warning(where, Messages.Get("validate.point_level")));
                }
                CheckSkills(row.Skills, where, issues);
                CheckPicks(row.Picks, where, issues);
                if (row.Spells != null && row.Spells.Any(string.IsNullOrWhiteSpace))
                    issues.Add(ImportIssue.Error(where, Messages.Get("validate.empty_spell")));
            }
        }

        static void CheckMythic(List<MythicRow> mythic, List<ImportIssue> issues) {
            int previous = 0;
            var seen = new HashSet<int>();
            foreach (var row in mythic) {
                if (row == null) {
                    issues.Add(ImportIssue.Error("mythic", Messages.Get("validate.empty_row", "mythic")));
                    continue;
                }
                string where = $"mythic[rank {row.Rank}]";
                if (row.Rank < 1 || row.Rank > 10)
                    issues.Add(ImportIssue.Error(where, Messages.Get("validate.rank_range", row.Rank)));
                if (!seen.Add(row.Rank))
                    issues.Add(ImportIssue.Error(where, Messages.Get("validate.rank_twice", row.Rank)));
                else if (row.Rank < previous)
                    issues.Add(ImportIssue.Error(where, Messages.Get("validate.rank_order", row.Rank, previous)));
                previous = row.Rank > previous ? row.Rank : previous;
                CheckPicks(row.Picks, where, issues);
            }
        }

        static void CheckPicks(List<PickEntry> picks, string where, List<ImportIssue> issues) {
            if (picks == null) return;
            foreach (var pick in picks) {
                if (pick == null || pick.Pick == null || pick.Pick.Count == 0 || pick.Pick.Any(string.IsNullOrWhiteSpace))
                    issues.Add(ImportIssue.Error(where, Messages.Get("validate.empty_pick")));
            }
        }
    }
}
