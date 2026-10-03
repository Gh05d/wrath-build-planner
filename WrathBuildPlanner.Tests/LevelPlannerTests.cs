using System.Collections.Generic;
using WrathBuildPlanner.Core;
using WrathBuildPlanner.Models;
using Xunit;

namespace WrathBuildPlanner.Tests {
    public class LevelPlannerTests {
        static BuildFile Build() => new BuildFile {
            Format = 1, Name = "x",
            Skills = new List<string> { "Perception", "Athletics" },
            Levels = new List<LevelRow> {
                new LevelRow { Level = 1, Class = "Fighter" },
                new LevelRow { Level = 2, Class = "Fighter", Skills = new List<string> { "Stealth" } },
                new LevelRow { Level = 3, Class = "Rogue" },
                new LevelRow { Level = 5, Class = "Fighter" },
            },
            Mythic = new List<MythicRow> { new MythicRow { Rank = 1 }, new MythicRow { Rank = 3, Path = "Angel" } },
        };

        [Fact]
        public void RowLookup() {
            var build = Build();
            Assert.Equal("Rogue", LevelPlanner.RowFor(build, 3).Class);
            Assert.Null(LevelPlanner.RowFor(build, 4));
            Assert.Null(LevelPlanner.RowFor(build, 21));
            Assert.Null(LevelPlanner.RowFor(new BuildFile { Levels = null }, 1));
            Assert.Equal("Angel", LevelPlanner.MythicRowFor(build, 3).Path);
            Assert.Null(LevelPlanner.MythicRowFor(build, 2));
        }

        [Fact]
        public void SkillsPreferTheRowThenTheBuild() {
            var build = Build();
            Assert.Equal(new[] { "Stealth" }, LevelPlanner.SkillsFor(build, build.Levels[1]));
            Assert.Equal(new[] { "Perception", "Athletics" }, LevelPlanner.SkillsFor(build, build.Levels[0]));
            build.Skills = null;
            Assert.Empty(LevelPlanner.SkillsFor(build, build.Levels[0]));
        }

        [Fact]
        public void ExpectedClassLevelsCountsRowsBelowTheLevel() {
            var expected = LevelPlanner.ExpectedClassLevels(Build(), 4, name => name.ToLowerInvariant());
            Assert.Equal(2, expected["fighter"]);
            Assert.Equal(1, expected["rogue"]);
        }

        [Fact]
        public void ExpectedClassLevelsIsNullWhenARowIsMissing() {
            Assert.Null(LevelPlanner.ExpectedClassLevels(Build(), 6, name => name));
        }

        [Fact]
        public void ExpectedClassLevelsForLevelOneIsEmpty() {
            Assert.Empty(LevelPlanner.ExpectedClassLevels(Build(), 1, name => name));
        }

        [Fact]
        public void HistoryMatches() {
            var expected = new Dictionary<string, int> { { "Fighter", 2 }, { "Rogue", 1 } };
            var actual = new Dictionary<string, int> { { "Rogue", 1 }, { "Fighter", 2 } };
            var check = LevelPlanner.CompareHistory(expected, actual);
            Assert.True(check.Comparable);
            Assert.True(check.Matches);
        }

        [Fact]
        public void HistoryMismatchDescribesBothSides() {
            var expected = new Dictionary<string, int> { { "Fighter", 3 } };
            var actual = new Dictionary<string, int> { { "Fighter", 2 }, { "Rogue", 1 } };
            var check = LevelPlanner.CompareHistory(expected, actual);
            Assert.True(check.Comparable);
            Assert.False(check.Matches);
            Assert.Equal("Fighter 3", check.Expected);
            Assert.Equal("Fighter 2 / Rogue 1", check.Actual);
        }

        [Fact]
        public void HistoryNotComparableWithoutExpectation() {
            var check = LevelPlanner.CompareHistory(null, new Dictionary<string, int> { { "Fighter", 2 } });
            Assert.False(check.Comparable);
        }

        [Fact]
        public void ReportCounts() {
            var report = new ApplyReport();
            report.Steps.Add(StepResult.Applied("Class: Fighter"));
            report.Steps.Add(StepResult.Already("Race: Human"));
            report.Steps.Add(StepResult.Open("Feat: Sage", OpenReason.NotFound, null, new List<string> { "Sage Sorcerer" }));
            Assert.Equal(1, report.AppliedCount);
            Assert.Equal(1, report.OpenCount);
            Assert.Equal("Sage Sorcerer", report.Steps[2].Suggestions[0]);
        }
    }
}
