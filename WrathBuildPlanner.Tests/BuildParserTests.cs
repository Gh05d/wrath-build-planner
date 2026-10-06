using System.Linq;
using WrathBuildPlanner.Core;
using Xunit;

namespace WrathBuildPlanner.Tests {
    public class BuildParserTests {
        const string Full = @"{
  ""format"": 1, ""name"": ""Two-Handed Fighter"", ""author"": ""me"", ""source"": ""https://example.org"", ""for"": ""main"",
  ""start"": { ""race"": ""Human"", ""raceBonus"": ""Strength"",
    ""abilityScores"": { ""Strength"": 16, ""Dexterity"": 14, ""Constitution"": 14, ""Intelligence"": 12, ""Wisdom"": 12, ""Charisma"": 11 },
    ""alignment"": ""Lawful Good"" },
  ""skills"": [""Perception"", ""Athletics""],
  ""levels"": [
    { ""level"": 1, ""class"": ""Fighter"", ""archetype"": ""Two-Handed Fighter"",
      ""picks"": [ { ""in"": ""Feat"", ""pick"": ""Power Attack"" },
                   { ""in"": ""Bonus Combat Feat"", ""pick"": [""Weapon Focus"", ""Greatsword""] },
                   ""Cleave"" ] },
    { ""level"": 4, ""class"": ""Fighter"", ""abilityPoint"": ""Strength"" },
    { ""level"": 5, ""class"": ""Wizard"", ""spells"": [""Magic Missile"", ""Grease""] }
  ],
  ""mythic"": [ { ""rank"": 1, ""picks"": [ { ""in"": ""Mythic Ability"", ""pick"": ""Last Stand"" } ] }, { ""rank"": 3, ""path"": ""Angel"" } ]
}";

        [Fact]
        public void ParsesTheFullExample() {
            var result = BuildParser.Parse(Full);
            Assert.True(result.Ok, string.Join("; ", result.Issues.Select(i => i.Message)));
            var build = result.Build;
            Assert.Equal("Two-Handed Fighter", build.Name);
            Assert.Equal(16, build.Start.AbilityScores["Strength"]);
            Assert.Equal(3, build.Levels.Count);
            Assert.Equal("Wizard", build.Levels[2].Class);
            Assert.Equal(new[] { "Magic Missile", "Grease" }, build.Levels[2].Spells);
            Assert.Equal(3, build.Mythic[1].Rank);
            Assert.Equal("Angel", build.Mythic[1].Path);
        }

        [Fact]
        public void PickFormsScopedChainAndBare() {
            var picks = BuildParser.Parse(Full).Build.Levels[0].Picks;
            Assert.Equal("Feat", picks[0].In);
            Assert.Equal(new[] { "Power Attack" }, picks[0].Pick);
            Assert.Equal(new[] { "Weapon Focus", "Greatsword" }, picks[1].Pick);
            Assert.Null(picks[2].In);
            Assert.Equal(new[] { "Cleave" }, picks[2].Pick);
            Assert.Equal("Bonus Combat Feat: Weapon Focus > Greatsword", picks[1].Label);
            Assert.Equal("Cleave", picks[2].Label);
        }

        [Fact]
        public void StripsMarkdownFenceAndBom() {
            string fenced = "﻿```json\n" + Full + "\n```\n";
            Assert.True(BuildParser.Parse(fenced).Ok);
        }

        [Fact]
        public void MalformedJsonNamesLineAndPosition() {
            var result = BuildParser.Parse("{ \"format\": 1,\n \"name\": \"x\",\n \"levels\": [ { \"level\": 1 \"class\": \"Fighter\" } ] }");
            Assert.False(result.Ok);
            Assert.Null(result.Build);
            Assert.Contains("line 3", result.Issues[0].Message);
        }

        [Fact]
        public void UnknownFieldIsAnErrorNamingTheField() {
            var result = BuildParser.Parse("{ \"format\": 1, \"name\": \"x\", \"levles\": [] }");
            Assert.False(result.Ok);
            Assert.Contains("levles", result.Issues[0].Message);
        }

        [Fact]
        public void UnknownFieldInsidePickIsAnError() {
            var result = BuildParser.Parse("{ \"format\": 1, \"name\": \"x\", \"levels\": [ { \"level\": 1, \"class\": \"Fighter\", \"picks\": [ { \"in\": \"Feat\", \"choose\": \"Cleave\" } ] } ] }");
            Assert.False(result.Ok);
            Assert.Contains("choose", result.Issues[0].Message);
        }

        [Fact]
        public void ProseAroundJsonIsFound() {
            // Changed 2026-10-06: players paste the AI's whole answer into the game, as the build page allows.
            var result = BuildParser.Parse("Here is your build:\n" + Full + "\nEnjoy!");
            Assert.True(result.Ok, string.Join("; ", result.Issues.Select(i => i.Message)));
        }

        [Fact]
        public void EmptyTextFails() {
            Assert.False(BuildParser.Parse("   ").Ok);
            Assert.False(BuildParser.Parse(null).Ok);
        }
    }
}
