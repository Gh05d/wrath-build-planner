using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WrathBuildPlanner.Core;
using WrathBuildPlanner.Models;
using WrathBuildPlanner.Persistence;
using Xunit;

namespace WrathBuildPlanner.Tests {
    public class BuildExporterTests : IDisposable {
        readonly string dir = Path.Combine(Path.GetTempPath(), "wbp-export-" + Guid.NewGuid().ToString("N"));

        public BuildExporterTests() {
            Directory.CreateDirectory(dir);
        }

        public void Dispose() {
            Directory.Delete(dir, true);
        }

        static PickEntry Pick(string page, params string[] chain) => new PickEntry { In = page, Pick = chain.ToList() };

        static CharacterRecord Record(string name = "Seelah", int levels = 2) {
            var record = new CharacterRecord { Name = name, Race = "Human", CharacterLevel = levels };
            for (int level = 1; level <= levels; level++) {
                record.Levels.Add(new RecordLevel {
                    Level = level, Class = "Fighter", Archetype = level == 1 ? "Two-Handed Fighter" : null,
                    Picks = { Pick("Feat", level == 1 ? "Power Attack" : "Cleave") },
                });
            }
            return record;
        }

        [Fact]
        public void NameAndForComeFromTheCharacter() {
            var build = BuildExporter.ToBuild(Record());
            Assert.Equal("Seelah (export)", build.Name);
            Assert.Equal("Seelah", build.For);
            Assert.Equal(1, build.Format);
        }

        [Fact]
        public void RaceOnlyInStart() {
            var build = BuildExporter.ToBuild(Record());
            Assert.Equal("Human", build.Start.Race);
            Assert.Null(build.Start.AbilityScores);
            Assert.Null(build.Start.RaceBonus);
            Assert.Null(build.Start.Alignment);

            var noRace = Record();
            noRace.Race = null;
            Assert.Null(BuildExporter.ToBuild(noRace).Start);
        }

        [Fact]
        public void LevelsCopiedWithoutAbilityPoint() {
            var record = Record();
            record.Levels.Add(new RecordLevel { Level = 3, Class = "Wizard", Spells = { "Magic Missile", "Grease" } });
            var build = BuildExporter.ToBuild(record);
            Assert.Equal(3, build.Levels.Count);
            Assert.Equal("Fighter", build.Levels[0].Class);
            Assert.Equal("Two-Handed Fighter", build.Levels[0].Archetype);
            Assert.Equal("Feat: Power Attack", build.Levels[0].Picks.Single().Label);
            Assert.Equal(new[] { "Magic Missile", "Grease" }, build.Levels[2].Spells);
            Assert.All(build.Levels, row => Assert.Null(row.AbilityPoint));
        }

        [Fact]
        public void MythicRanksCopied() {
            var record = Record();
            record.Mythic.Add(new RecordRank { Rank = 1, Picks = { Pick("Mythic Ability", "Abundant Casting") } });
            record.Mythic.Add(new RecordRank { Rank = 3, Path = "Angel" });
            var build = BuildExporter.ToBuild(record);
            Assert.Equal(new[] { 1, 3 }, build.Mythic.Select(r => r.Rank));
            Assert.Null(build.Mythic[0].Path);
            Assert.Equal("Angel", build.Mythic[1].Path);
        }

        [Fact]
        public void SkillsHighestFirst() {
            var record = Record();
            record.SkillRanks = new Dictionary<string, int> {
                { "SkillPerception", 5 }, { "SkillAthletics", 9 }, { "SkillStealth", 0 }, { "SkillMobility", 5 },
            };
            Assert.Equal(new[] { "Athletics", "Mobility", "Perception" }, BuildExporter.ToBuild(record).Skills);
        }

        [Fact]
        public void NoSkillsMeansNoSkillList() {
            Assert.Null(BuildExporter.ToBuild(Record()).Skills);
        }

        [Fact]
        public void EveryPickCarriesItsPage() {
            var record = Record(levels: 1);
            record.Levels[0].Picks.Add(Pick("Bonus Combat Feat", "Weapon Focus", "Greatsword"));
            var parsed = BuildParser.Parse(BuildExporter.ToJson(BuildExporter.ToBuild(record)));
            Assert.True(parsed.Ok);
            Assert.Equal(new[] { "Feat: Power Attack", "Bonus Combat Feat: Weapon Focus > Greatsword" },
                parsed.Build.Levels[0].Picks.Select(p => p.Label));
        }

        [Fact]
        public void JsonRoundTripsAndValidates() {
            string json = BuildExporter.ToJson(BuildExporter.ToBuild(Record()));
            var parsed = BuildParser.Parse(json);
            Assert.True(parsed.Ok, string.Join("; ", parsed.Issues.Select(i => i.Message)));
            Assert.DoesNotContain(BuildValidator.Validate(parsed.Build, null), i => i.IsError);
            Assert.DoesNotContain("abilityPoint", json);
            Assert.DoesNotContain("null", json);
            Assert.DoesNotContain("[]", json);
        }

        [Fact]
        public void GapsNameWhatIsMissing() {
            var low = Record(levels: 3);
            Assert.Equal(new[] { "export.gap.scores" }, BuildExporter.Gaps(low, BuildExporter.ToBuild(low)));

            var high = Record(levels: 4);
            Assert.Equal(new[] { "export.gap.scores", "export.gap.ability_points" }, BuildExporter.Gaps(high, BuildExporter.ToBuild(high)));

            high.SpellsIncomplete = true;
            high.CharacterLevel = 6;
            Assert.Equal(new[] { "export.gap.history", "export.gap.scores", "export.gap.ability_points", "export.gap.spells" },
                BuildExporter.Gaps(high, BuildExporter.ToBuild(high)));
        }

        [Fact]
        public void ExportNameSlugsSafely() {
            var library = new BuildLibrary(dir, null);
            var result = library.ImportText(BuildExporter.ToJson(BuildExporter.ToBuild(Record("司祭", 1))));
            Assert.True(result.Ok, string.Join("; ", result.Issues.Select(i => i.Message)));
            Assert.Equal("export.json", result.FileName);
        }

        [Fact]
        public void SecondExportGetsSuffix() {
            var library = new BuildLibrary(dir, null);
            string json = BuildExporter.ToJson(BuildExporter.ToBuild(Record()));
            Assert.Equal("seelah-export.json", library.ImportText(json).FileName);
            string first = File.ReadAllText(Path.Combine(dir, "seelah-export.json"));
            Assert.Equal("seelah-export-2.json", library.ImportText(json).FileName);
            Assert.Equal(first, File.ReadAllText(Path.Combine(dir, "seelah-export.json")));
        }
    }
}
