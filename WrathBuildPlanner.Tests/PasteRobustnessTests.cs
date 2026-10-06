using System.Linq;
using WrathBuildPlanner.Core;
using Xunit;

namespace WrathBuildPlanner.Tests {
    /// <summary>
    /// A player copies the AI's whole answer straight into "Paste from clipboard", skipping the build page.
    /// Every shape below came out of the LLM acceptance runs or is what a chat UI copies (2026-10-06).
    /// </summary>
    public class PasteRobustnessTests {
        const string Json = "{ \"format\": 1, \"name\": \"x\", \"levels\": [ { \"level\": 1, \"class\": \"Fighter\" } ] }";

        static void AssertParses(string text) {
            var result = BuildParser.Parse(text);
            Assert.True(result.Ok, string.Join("; ", result.Issues.Select(i => i.Message)));
            Assert.Equal("x", result.Build.Name);
        }

        [Fact]
        public void JsonBlockAfterAnotherBlock() =>
            AssertParses("Here you go:\n```text\nnotes\n```\n```json\n" + Json + "\n```\nLeft out:\n- traits");

        [Fact]
        public void JsonBlockFollowedByAFencedLeftOutList() =>
            AssertParses("```json\n" + Json + "\n```\n\n```\nLeft out:\n- deity\n```");

        [Fact]
        public void UnlabelledFenceAfterProse() =>
            AssertParses("Sure! Here is the build.\n```\n" + Json + "\n```\nHope this helps.");

        [Fact]
        public void BareObjectInsideProse() =>
            AssertParses("Sure! " + Json + " Hope this helps.");

        [Fact]
        public void BracesInsideStringsDoNotEndTheObject() {
            var clean = BuildParser.Parse("Sure: " + Json.Replace("\"name\": \"x\"", "\"name\": \"x\", \"author\": \"a } b\"") + " done");
            Assert.True(clean.Ok, string.Join("; ", clean.Issues.Select(i => i.Message)));
            Assert.Equal("a } b", clean.Build.Author);
        }

        [Fact]
        public void TrailingCommasAndComments() =>
            AssertParses("{\n  // the build\n  \"format\": 1,\n  \"name\": \"x\", /* name */\n  \"levels\": [ { \"level\": 1, \"class\": \"Fighter\", }, ],\n}");

        [Fact]
        public void TheCleanTextIsTheObjectAlone() {
            var result = BuildParser.Parse("Intro\n```text\nnotes\n```\n```json\n" + Json + "\n```\nOutro");
            Assert.StartsWith("{", result.CleanText);
            Assert.EndsWith("}", result.CleanText);
        }

        [Fact]
        public void TypographicQuotesGiveAReadableError() {
            var result = BuildParser.Parse("{ “format”: 1, “name”: “x” }");
            Assert.False(result.Ok);
            Assert.Contains(result.Issues, i => i.Message.Contains("straight quotes"));
        }

        [Fact]
        public void NoJsonAtAll() {
            var result = BuildParser.Parse("Level 1: Fighter, Power Attack. Level 2: Cleave.");
            Assert.False(result.Ok);
        }
    }
}
