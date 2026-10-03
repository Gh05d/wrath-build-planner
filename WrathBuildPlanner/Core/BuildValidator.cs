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
                issues.Add(ImportIssue.Error("format", $"This build uses format {build.Format}; this version of the mod reads format {SupportedFormat}."));
            if (string.IsNullOrWhiteSpace(build.Name))
                issues.Add(ImportIssue.Error("name", "The build has no name."));

            var levels = build.Levels ?? new List<LevelRow>();
            var mythic = build.Mythic ?? new List<MythicRow>();
            if (levels.Count == 0 && mythic.Count == 0 && build.Start == null)
                issues.Add(ImportIssue.Error("file", "The build has nothing to apply: no start block, no levels, no mythic ranks."));

            CheckStart(build.Start, names, issues);
            CheckSkills(build.Skills, "skills", issues);
            CheckLevels(levels, names, issues);
            CheckMythic(mythic, issues);
            return issues;
        }

        static void CheckStart(StartBlock start, IKnownNames names, List<ImportIssue> issues) {
            if (start == null) return;
            if (!string.IsNullOrWhiteSpace(start.Race) && names != null && !names.RaceExists(start.Race))
                issues.Add(ImportIssue.Error("start.race", $"Unknown race '{start.Race}'."));
            if (!string.IsNullOrWhiteSpace(start.RaceBonus) && !Vocabulary.TryAttribute(start.RaceBonus, out _))
                issues.Add(ImportIssue.Error("start.raceBonus", $"'{start.RaceBonus}' is not an attribute."));
            if (!string.IsNullOrWhiteSpace(start.Alignment) && !Vocabulary.TryAlignment(start.Alignment, out _))
                issues.Add(ImportIssue.Error("start.alignment", $"Unknown alignment '{start.Alignment}'."));
            if (start.AbilityScores == null) return;
            foreach (var score in start.AbilityScores) {
                if (!Vocabulary.TryAttribute(score.Key, out _))
                    issues.Add(ImportIssue.Error("start.abilityScores", $"'{score.Key}' is not an attribute."));
                else if (score.Value < 7 || score.Value > 18)
                    issues.Add(ImportIssue.Error("start.abilityScores", $"{score.Key} is {score.Value}; starting scores go from 7 to 18 (before the racial bonus)."));
            }
        }

        static void CheckSkills(List<string> skills, string where, List<ImportIssue> issues) {
            if (skills == null) return;
            foreach (string skill in skills) {
                if (!Vocabulary.TrySkill(skill, out _))
                    issues.Add(ImportIssue.Error(where, $"Unknown skill '{skill}'."));
            }
        }

        static void CheckLevels(List<LevelRow> levels, IKnownNames names, List<ImportIssue> issues) {
            int previous = 0;
            var seen = new HashSet<int>();
            foreach (var row in levels) {
                string where = $"levels[level {row.Level}]";
                if (row.Level < 1 || row.Level > 20)
                    issues.Add(ImportIssue.Error(where, $"Level {row.Level} is out of range; character levels go from 1 to 20."));
                if (!seen.Add(row.Level))
                    issues.Add(ImportIssue.Error(where, $"The build lists level {row.Level} twice."));
                else if (row.Level < previous)
                    issues.Add(ImportIssue.Error(where, $"Levels must be in ascending order; level {row.Level} comes after level {previous}."));
                previous = row.Level > previous ? row.Level : previous;

                if (string.IsNullOrWhiteSpace(row.Class))
                    issues.Add(ImportIssue.Error(where, "The level has no class."));
                else if (names != null && !names.ClassExists(row.Class))
                    issues.Add(ImportIssue.Error(where, $"Unknown class '{row.Class}'."));

                if (!string.IsNullOrWhiteSpace(row.AbilityPoint)) {
                    if (!Vocabulary.TryAttribute(row.AbilityPoint, out _))
                        issues.Add(ImportIssue.Error(where, $"'{row.AbilityPoint}' is not an attribute."));
                    else if (row.Level % 4 != 0)
                        issues.Add(ImportIssue.Warning(where, "An attribute point is only granted every fourth level; this entry will be ignored."));
                }
                CheckSkills(row.Skills, where, issues);
                CheckPicks(row.Picks, where, issues);
                if (row.Spells != null && row.Spells.Any(string.IsNullOrWhiteSpace))
                    issues.Add(ImportIssue.Error(where, "A spell name is empty."));
            }
        }

        static void CheckMythic(List<MythicRow> mythic, List<ImportIssue> issues) {
            int previous = 0;
            var seen = new HashSet<int>();
            foreach (var row in mythic) {
                string where = $"mythic[rank {row.Rank}]";
                if (row.Rank < 1 || row.Rank > 10)
                    issues.Add(ImportIssue.Error(where, $"Mythic rank {row.Rank} is out of range; ranks go from 1 to 10."));
                if (!seen.Add(row.Rank))
                    issues.Add(ImportIssue.Error(where, $"The build lists mythic rank {row.Rank} twice."));
                else if (row.Rank < previous)
                    issues.Add(ImportIssue.Error(where, $"Mythic ranks must be in ascending order; rank {row.Rank} comes after rank {previous}."));
                previous = row.Rank > previous ? row.Rank : previous;
                CheckPicks(row.Picks, where, issues);
            }
        }

        static void CheckPicks(List<PickEntry> picks, string where, List<ImportIssue> issues) {
            if (picks == null) return;
            foreach (var pick in picks) {
                if (pick == null || pick.Pick == null || pick.Pick.Count == 0 || pick.Pick.Any(string.IsNullOrWhiteSpace))
                    issues.Add(ImportIssue.Error(where, "A pick is empty or has an empty name in its chain."));
            }
        }
    }
}
