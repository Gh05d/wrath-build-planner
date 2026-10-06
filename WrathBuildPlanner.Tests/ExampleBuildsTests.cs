using System;
using System.IO;
using System.Linq;
using WrathBuildPlanner.Core;
using Xunit;

namespace WrathBuildPlanner.Tests {
    public class ExampleBuildsTests {
        internal static string RepoFolder(string name) {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, name))) dir = dir.Parent;
            Assert.True(dir != null, $"folder '{name}' not found above the test output");
            return Path.Combine(dir.FullName, name);
        }

        [Theory]
        [InlineData("Builds-examples")]
        [InlineData("tests/builds")]
        public void ShippedAndTestBuildsParseAndValidate(string folder) {
            var files = Directory.GetFiles(RepoFolder(folder.Split('/')[0]), "*.json", SearchOption.AllDirectories)
                .Where(f => f.Replace('\\', '/').Contains(folder)).ToList();
            Assert.NotEmpty(files);
            foreach (string file in files) {
                var parsed = BuildParser.Parse(File.ReadAllText(file));
                Assert.True(parsed.Ok, $"{file}: {string.Join("; ", parsed.Issues.Select(i => i.Message))}");
                var errors = BuildValidator.Validate(parsed.Build, null).Where(i => i.IsError).Select(i => i.Message).ToList();
                Assert.True(errors.Count == 0, $"{file}: {string.Join("; ", errors)}");
            }
        }
    }
}
