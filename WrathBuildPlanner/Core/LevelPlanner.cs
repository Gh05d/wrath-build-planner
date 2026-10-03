using System;
using System.Collections.Generic;
using System.Linq;
using WrathBuildPlanner.Models;

namespace WrathBuildPlanner.Core {
    public static class LevelPlanner {
        public static LevelRow RowFor(BuildFile build, int level) =>
            build?.Levels?.FirstOrDefault(r => r.Level == level);

        public static MythicRow MythicRowFor(BuildFile build, int rank) =>
            build?.Mythic?.FirstOrDefault(r => r.Rank == rank);

        public static List<string> SkillsFor(BuildFile build, LevelRow row) =>
            row?.Skills ?? build?.Skills ?? new List<string>();

        /// <summary>
        /// Class levels the build expects the character to have before <paramref name="belowLevel"/>.
        /// Null when the build has no row for one of those levels: the history cannot be judged then.
        /// </summary>
        public static Dictionary<string, int> ExpectedClassLevels(BuildFile build, int belowLevel, Func<string, string> canonical) {
            var expected = new Dictionary<string, int>();
            for (int level = 1; level < belowLevel; level++) {
                var row = RowFor(build, level);
                if (row == null || string.IsNullOrWhiteSpace(row.Class)) return null;
                string key = canonical(row.Class);
                expected[key] = expected.TryGetValue(key, out int count) ? count + 1 : 1;
            }
            return expected;
        }

        public static HistoryCheck CompareHistory(Dictionary<string, int> expected, Dictionary<string, int> actual) {
            var check = new HistoryCheck();
            if (expected == null || actual == null) return check;
            check.Comparable = true;
            check.Expected = Describe(expected);
            check.Actual = Describe(actual);
            check.Matches = expected.Count == actual.Count
                && expected.All(e => actual.TryGetValue(e.Key, out int have) && have == e.Value);
            return check;
        }

        static string Describe(Dictionary<string, int> levels) =>
            string.Join(" / ", levels.OrderByDescending(l => l.Value).ThenBy(l => l.Key).Select(l => $"{l.Key} {l.Value}"));
    }
}
