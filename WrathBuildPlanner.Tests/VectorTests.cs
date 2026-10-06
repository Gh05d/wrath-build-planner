using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WrathBuildPlanner.Core;
using Xunit;

namespace WrathBuildPlanner.Tests {
    /// <summary>tests/vectors/*.json — the same cases run against the JavaScript port (tests/site). C# is the reference.</summary>
    public class VectorTests {
        static JToken Load(string file) =>
            JToken.Parse(File.ReadAllText(Path.Combine(ExampleBuildsTests.RepoFolder("tests"), "vectors", file)));

        static List<NameCandidate> Candidates(IEnumerable<JToken> lists) =>
            lists.Select(l => {
                var names = l.Select(n => (string)n).ToArray();
                return new NameCandidate { Names = names, Display = names[0], Tag = names[0] };
            }).ToList();

        [Fact]
        public void NameMatchingVectors() {
            foreach (var c in Load("name-matching.json")) {
                string name = (string)c["name"];
                var outcome = NameMatcher.Match((string)c["wanted"], Candidates(c["candidates"]));
                Assert.True((string)c["kind"] == outcome.Kind.ToString().ToLowerInvariant(), $"{name}: kind {outcome.Kind}");
                Assert.True((string)c["match"] == outcome.Match?.Display, $"{name}: match {outcome.Match?.Display}");
                Assert.True(c["tied"].Select(t => (string)t).SequenceEqual(outcome.Tied.Select(t => t.Display)), $"{name}: tied {string.Join(", ", outcome.Tied.Select(t => t.Display))}");
                Assert.True(c["suggestions"].Select(t => (string)t).SequenceEqual(outcome.Suggestions), $"{name}: suggestions {string.Join(", ", outcome.Suggestions)}");
            }
        }

        class Known : IKnownNames {
            readonly List<NameCandidate> classes, races;
            public Known(JToken root) {
                classes = Candidates(root["knownClasses"].Select(n => (JToken)new JArray(n)));
                races = Candidates(root["knownRaces"].Select(n => (JToken)new JArray(n)));
            }
            public bool ClassExists(string name) => NameMatcher.Match(name, classes).Kind == MatchKind.Unique;
            public bool RaceExists(string name) => NameMatcher.Match(name, races).Kind == MatchKind.Unique;
        }

        [Fact]
        public void ValidationVectors() {
            var root = Load("validation.json");
            var known = new Known(root);
            foreach (var c in root["cases"]) {
                string name = (string)c["name"];
                var parsed = BuildParser.Parse(c["build"].ToString(Formatting.None));
                if ((string)c["parse"] == "error") {
                    Assert.False(parsed.Ok, $"{name}: expected a parse error");
                    continue;
                }
                Assert.True(parsed.Ok, $"{name}: {string.Join("; ", parsed.Issues.Select(i => i.Message))}");
                var actual = BuildValidator.Validate(parsed.Build, known)
                    .Select(i => $"{(i.IsError ? "E" : "W")}|{i.Where}|{i.Message}").ToList();
                var expected = c["issues"].Select(i => $"{((bool)i["error"] ? "E" : "W")}|{i["where"]}|{i["message"]}").ToList();
                Assert.True(expected.SequenceEqual(actual), $"{name}:\n expected {string.Join(" ;; ", expected)}\n actual   {string.Join(" ;; ", actual)}");
            }
        }
    }
}
