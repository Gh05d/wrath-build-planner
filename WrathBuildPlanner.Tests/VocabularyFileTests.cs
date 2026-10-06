using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WrathBuildPlanner.Core;
using Xunit;

namespace WrathBuildPlanner.Tests {
    /// <summary>site/data/vocabulary.json is the page's copy of Core/Vocabulary. UPDATE_VOCABULARY=1 rewrites it.</summary>
    public class VocabularyFileTests {
        static JObject Expected() => new JObject {
            ["attributes"] = new JArray(Vocabulary.Attributes),
            ["skills"] = new JObject(Vocabulary.SkillList.Select(kv => new JProperty(kv.Key, new JArray(kv.Value)))),
            ["alignments"] = new JObject(Vocabulary.AlignmentList.Select(kv => new JProperty(kv.Key, new JArray(kv.Value)))),
        };

        [Fact]
        public void SiteVocabularyMatchesCore() {
            string path = Path.Combine(ExampleBuildsTests.RepoFolder("site"), "data", "vocabulary.json");
            if (Environment.GetEnvironmentVariable("UPDATE_VOCABULARY") == "1")
                File.WriteAllText(path, Expected().ToString(Formatting.Indented) + "\n");
            Assert.True(File.Exists(path), $"{path} missing — run the tests once with UPDATE_VOCABULARY=1");
            var actual = JObject.Parse(File.ReadAllText(path));
            Assert.True(JToken.DeepEquals(Expected(), actual), "site/data/vocabulary.json differs from Core/Vocabulary — rerun with UPDATE_VOCABULARY=1");
        }
    }
}
