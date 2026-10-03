using System.Collections.Generic;
using System.Linq;
using WrathBuildPlanner.Core;
using WrathBuildPlanner.Models;
using Xunit;

namespace WrathBuildPlanner.Tests {
    public class BuildValidatorTests {
        class Names : IKnownNames {
            public bool ClassExists(string name) => name == "Fighter" || name == "Wizard";
            public bool RaceExists(string name) => name == "Human";
        }

        static BuildFile Minimal() => new BuildFile {
            Format = 1, Name = "x",
            Levels = new List<LevelRow> { new LevelRow { Level = 1, Class = "Fighter" } },
        };

        static List<string> Errors(BuildFile build) =>
            BuildValidator.Validate(build, new Names()).Where(i => i.IsError).Select(i => i.Message).ToList();

        [Fact]
        public void MinimalBuildIsValid() {
            Assert.Empty(Errors(Minimal()));
        }

        [Fact]
        public void UnsupportedFormat() {
            var build = Minimal();
            build.Format = 2;
            Assert.Contains(Errors(build), m => m.Contains("format 2"));
        }

        [Fact]
        public void MissingName() {
            var build = Minimal();
            build.Name = " ";
            Assert.Contains(Errors(build), m => m.Contains("name"));
        }

        [Fact]
        public void LevelsMustAscendWithoutDuplicates() {
            var build = Minimal();
            build.Levels.Add(new LevelRow { Level = 1, Class = "Fighter" });
            Assert.Contains(Errors(build), m => m.Contains("level 1") && m.Contains("twice"));

            build = Minimal();
            build.Levels.Insert(0, new LevelRow { Level = 3, Class = "Fighter" });
            Assert.Contains(Errors(build), m => m.Contains("ascending"));
        }

        [Fact]
        public void LevelRangeAndGaps() {
            var build = Minimal();
            build.Levels.Add(new LevelRow { Level = 21, Class = "Fighter" });
            Assert.Contains(Errors(build), m => m.Contains("1 to 20"));

            build = Minimal();
            build.Levels.Add(new LevelRow { Level = 5, Class = "Fighter" });
            Assert.Empty(Errors(build));
        }

        [Fact]
        public void ClassRequiredAndKnown() {
            var build = Minimal();
            build.Levels[0].Class = null;
            Assert.Contains(Errors(build), m => m.Contains("class"));

            build = Minimal();
            build.Levels[0].Class = "Figther";
            Assert.Contains(Errors(build), m => m.Contains("Figther"));
        }

        [Fact]
        public void NullNamesSkipsClassAndRaceChecks() {
            var build = Minimal();
            build.Levels[0].Class = "Anything";
            Assert.DoesNotContain(BuildValidator.Validate(build, null), i => i.IsError);
        }

        [Fact]
        public void StartBlockChecks() {
            var build = Minimal();
            build.Start = new StartBlock {
                Race = "Hooman", RaceBonus = "Luck", Alignment = "Lawful Nice",
                AbilityScores = new Dictionary<string, int> { { "Strength", 19 }, { "Speed", 10 } },
            };
            var errors = Errors(build);
            Assert.Contains(errors, m => m.Contains("Hooman"));
            Assert.Contains(errors, m => m.Contains("Luck"));
            Assert.Contains(errors, m => m.Contains("Lawful Nice"));
            Assert.Contains(errors, m => m.Contains("19") && m.Contains("7 to 18"));
            Assert.Contains(errors, m => m.Contains("Speed"));
        }

        [Fact]
        public void SkillAndAbilityPointNames() {
            var build = Minimal();
            build.Skills = new List<string> { "Perception", "Trickery", "Knowledge (Arcana)", "Cooking" };
            build.Levels[0].AbilityPoint = "Stamina";
            var errors = Errors(build);
            Assert.Single(errors, m => m.Contains("Cooking"));
            Assert.Contains(errors, m => m.Contains("Stamina"));
        }

        [Fact]
        public void AbilityPointOnOtherLevelIsAWarning() {
            var build = Minimal();
            build.Levels[0].AbilityPoint = "Strength";
            var issues = BuildValidator.Validate(build, new Names());
            Assert.DoesNotContain(issues, i => i.IsError);
            Assert.Contains(issues, i => !i.IsError && i.Message.Contains("every fourth level"));
        }

        [Fact]
        public void EmptyPickAndMythicRank() {
            var build = Minimal();
            build.Levels[0].Picks.Add(new PickEntry { In = "Feat" });
            build.Mythic.Add(new MythicRow { Rank = 11 });
            var errors = Errors(build);
            Assert.Contains(errors, m => m.Contains("pick") && m.Contains("empty"));
            Assert.Contains(errors, m => m.Contains("1 to 10"));
        }

        [Fact]
        public void BuildWithNothingToApply() {
            var build = new BuildFile { Format = 1, Name = "x" };
            Assert.Contains(Errors(build), m => m.Contains("nothing to apply"));
        }
    }
}
