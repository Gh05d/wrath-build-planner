using System;
using System.IO;
using System.Linq;
using WrathBuildPlanner.Core;
using WrathBuildPlanner.Persistence;
using Xunit;

namespace WrathBuildPlanner.Tests {
    /// <summary>Review findings: input that used to throw instead of being reported.</summary>
    public class RobustInputTests : IDisposable {
        readonly string dir = Path.Combine(Path.GetTempPath(), "wbp-robust-" + Guid.NewGuid().ToString("N"));

        const string Valid = "{ \"format\": 1, \"name\": \"Good\", \"levels\": [ { \"level\": 1, \"class\": \"Fighter\" } ] }";

        public RobustInputTests() {
            Directory.CreateDirectory(dir);
        }

        public void Dispose() {
            Directory.Delete(dir, true);
        }

        [Theory]
        [InlineData("{ \"format\": 1, \"name\": \"x\", \"levels\": [ null ] }")]
        [InlineData("{ \"format\": 1, \"name\": \"x\", \"mythic\": [ null ] }")]
        [InlineData("{ \"format\": 1, \"name\": \"x\", \"levels\": [ { \"level\": 1, \"class\": \"Fighter\", \"picks\": [ null ] } ] }")]
        public void NullEntriesAreReportedNotThrown(string json) {
            var parsed = BuildParser.Parse(json);
            Assert.NotNull(parsed.Build);
            var errors = BuildValidator.Validate(parsed.Build, null).Where(i => i.IsError).ToList();
            Assert.NotEmpty(errors);
        }

        [Theory]
        [InlineData("{ \"in\": \"Feat\", \"pick\": { \"a\": 1 } }")]
        [InlineData("{ \"in\": \"Feat\", \"pick\": [ [ \"a\" ] ] }")]
        [InlineData("{ \"in\": [ \"Feat\" ], \"pick\": \"Cleave\" }")]
        [InlineData("7")]
        public void WrongTypesInsideAPickAreParseErrors(string pick) {
            var parsed = BuildParser.Parse("{ \"format\": 1, \"name\": \"x\", \"levels\": [ { \"level\": 1, \"class\": \"Fighter\", \"picks\": [ " + pick + " ] } ] }");
            Assert.False(parsed.Ok);
            Assert.Single(parsed.Issues);
        }

        [Fact]
        public void NullListsBecomeEmptyLists() {
            var parsed = BuildParser.Parse("{ \"format\": 1, \"name\": \"x\", \"mythic\": null, \"levels\": [ { \"level\": 1, \"class\": \"Fighter\", \"picks\": null, \"spells\": null } ] }");
            Assert.True(parsed.Ok);
            Assert.NotNull(parsed.Build.Mythic);
            Assert.NotNull(parsed.Build.Levels[0].Picks);
            Assert.NotNull(parsed.Build.Levels[0].Spells);
            var levelsNull = BuildParser.Parse("{ \"format\": 1, \"name\": \"x\", \"levels\": null, \"mythic\": [ { \"rank\": 1, \"picks\": null } ] }");
            Assert.NotNull(levelsNull.Build.Levels);
            Assert.NotNull(levelsNull.Build.Mythic[0].Picks);
        }

        [Fact]
        public void ProseAroundAFencedBlockIsAccepted() {
            var parsed = BuildParser.Parse("Here is your build:\n\n```json\n" + Valid + "\n```\n\nEnjoy!");
            Assert.True(parsed.Ok, string.Join("; ", parsed.Issues.Select(i => i.Message)));
            Assert.Equal("Good", parsed.Build.Name);
        }

        [Fact]
        public void StructureErrorsKeepTheirLocation() {
            var parsed = BuildParser.Parse("{ \"format\": 1,\n \"name\": \"x\",\n \"levles\": [] }");
            Assert.Contains("levles", parsed.Issues[0].Message);
            Assert.Contains("line 3", parsed.Issues[0].Message);
        }

        [Fact]
        public void TopLevelArrayGivesOneShortMessage() {
            var parsed = BuildParser.Parse("[ 1, 2 ]");
            Assert.False(parsed.Ok);
            Assert.DoesNotContain("\n", parsed.Issues[0].Message);
        }

        [Fact]
        public void StartBlockWithoutLevelOneIsAWarning() {
            var parsed = BuildParser.Parse("{ \"format\": 1, \"name\": \"x\", \"start\": { \"race\": \"Human\" }, \"levels\": [ { \"level\": 2, \"class\": \"Fighter\" } ] }");
            var issues = BuildValidator.Validate(parsed.Build, null);
            Assert.DoesNotContain(issues, i => i.IsError);
            Assert.Contains(issues, i => !i.IsError && i.Message.Contains("level 1"));
        }

        [Fact]
        public void ReloadSurvivesABrokenFileNextToAGoodOne() {
            File.WriteAllText(Path.Combine(dir, "a-broken.json"), "{ \"format\": 1, \"name\": \"x\", \"levels\": [ null ] }");
            File.WriteAllText(Path.Combine(dir, "b-wrongtype.json"), "{ \"format\": 1, \"name\": \"x\", \"levels\": [ { \"level\": 1, \"class\": \"Fighter\", \"picks\": [ { \"pick\": { } } ] } ] }");
            File.WriteAllText(Path.Combine(dir, "c-good.json"), Valid);
            var library = new BuildLibrary(dir, null);
            library.Reload();
            Assert.Equal(3, library.Entries.Count);
            Assert.False(library.Find("a-broken.json").Ok);
            Assert.False(library.Find("b-wrongtype.json").Ok);
            Assert.True(library.Find("c-good.json").Ok);
        }

        [Fact]
        public void ImportStoresTheCleanedJson() {
            var library = new BuildLibrary(dir, null);
            var result = library.ImportText("Sure!\n```json\n" + Valid + "\n```");
            Assert.True(result.Ok);
            string stored = File.ReadAllText(Path.Combine(dir, result.FileName));
            Assert.StartsWith("{", stored);
            Assert.EndsWith("}", stored);
        }

        [Fact]
        public void FileNamesAreCappedAndAvoidDeviceNames() {
            var library = new BuildLibrary(dir, null);
            var longName = library.ImportText(Valid.Replace("Good", new string('a', 400)));
            Assert.True(longName.Ok, string.Join("; ", longName.Issues.Select(i => i.Message)));
            Assert.True(longName.FileName.Length <= 70);
            var device = library.ImportText(Valid.Replace("Good", "CON"));
            Assert.True(device.Ok);
            Assert.NotEqual("con.json", device.FileName);
        }

        [Fact]
        public void EveryMessageHasATranslationInTheFourOtherLanguages() {
            var dirInfo = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dirInfo != null && !Directory.Exists(Path.Combine(dirInfo.FullName, "WrathBuildPlanner", "Localization"))) dirInfo = dirInfo.Parent;
            Assert.NotNull(dirInfo);
            foreach (string file in new[] { "de_DE.json", "fr_FR.json", "ru_RU.json", "zh_CN.json" }) {
                string json = File.ReadAllText(Path.Combine(dirInfo.FullName, "WrathBuildPlanner", "Localization", file));
                var pack = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string>>(json);
                var missing = Messages.Keys.Where(k => !pack.ContainsKey("msg." + k)).ToList();
                Assert.True(missing.Count == 0, file + " lacks: " + string.Join(", ", missing));
            }
        }
    }
}
