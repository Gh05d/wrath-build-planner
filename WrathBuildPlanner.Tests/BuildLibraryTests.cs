using System;
using System.IO;
using System.Linq;
using WrathBuildPlanner.Persistence;
using Xunit;

namespace WrathBuildPlanner.Tests {
    public class BuildLibraryTests : IDisposable {
        readonly string dir = Path.Combine(Path.GetTempPath(), "wbp-tests-" + Guid.NewGuid().ToString("N"));

        const string Valid = "{ \"format\": 1, \"name\": \"Two-Handed Fighter\", \"levels\": [ { \"level\": 1, \"class\": \"Fighter\" } ] }";

        public BuildLibraryTests() {
            Directory.CreateDirectory(dir);
        }

        public void Dispose() {
            Directory.Delete(dir, true);
        }

        [Fact]
        public void ReloadListsValidAndBrokenFiles() {
            File.WriteAllText(Path.Combine(dir, "good.json"), Valid);
            File.WriteAllText(Path.Combine(dir, "broken.json"), "{ not json");
            File.WriteAllText(Path.Combine(dir, "notes.txt"), "ignored");
            var library = new BuildLibrary(dir, null);
            library.Reload();
            Assert.Equal(2, library.Entries.Count);
            Assert.True(library.Find("good.json").Ok);
            Assert.False(library.Find("broken.json").Ok);
            Assert.NotEmpty(library.Find("broken.json").Issues);
            Assert.Null(library.Find("notes.txt"));
        }

        [Fact]
        public void MissingDirectoryIsCreatedAndEmpty() {
            string missing = Path.Combine(dir, "sub");
            var library = new BuildLibrary(missing, null);
            library.Reload();
            Assert.Empty(library.Entries);
            Assert.True(Directory.Exists(missing));
        }

        [Fact]
        public void ImportStoresAFileNamedAfterTheBuild() {
            var library = new BuildLibrary(dir, null);
            var result = library.ImportText(Valid);
            Assert.True(result.Ok);
            Assert.Equal("two-handed-fighter.json", result.FileName);
            Assert.True(File.Exists(Path.Combine(dir, "two-handed-fighter.json")));
            Assert.True(library.Find("two-handed-fighter.json").Ok);
        }

        [Fact]
        public void ImportNeverOverwrites() {
            var library = new BuildLibrary(dir, null);
            library.ImportText(Valid);
            string original = File.ReadAllText(Path.Combine(dir, "two-handed-fighter.json"));
            var second = library.ImportText(Valid.Replace("\"Fighter\" } ]", "\"Wizard\" } ]"));
            Assert.True(second.Ok);
            Assert.Equal("two-handed-fighter-2.json", second.FileName);
            Assert.Equal(original, File.ReadAllText(Path.Combine(dir, "two-handed-fighter.json")));
        }

        [Fact]
        public void InvalidImportStoresNothing() {
            var library = new BuildLibrary(dir, null);
            var result = library.ImportText("{ \"format\": 1, \"name\": \"x\", \"levels\": [ { \"level\": 30, \"class\": \"Fighter\" } ] }");
            Assert.False(result.Ok);
            Assert.Null(result.FileName);
            Assert.Empty(Directory.GetFiles(dir));
            Assert.Contains(result.Issues, i => i.Message.Contains("1 to 20"));
        }

        [Fact]
        public void FileRemovedAfterLoadIsGoneAfterReload() {
            File.WriteAllText(Path.Combine(dir, "good.json"), Valid);
            var library = new BuildLibrary(dir, null);
            library.Reload();
            File.Delete(Path.Combine(dir, "good.json"));
            library.Reload();
            Assert.Null(library.Find("good.json"));
        }

        [Fact]
        public void SlugHandlesOddNames() {
            var library = new BuildLibrary(dir, null);
            var result = library.ImportText(Valid.Replace("Two-Handed Fighter", "  Äxte & Schwerter!! / v2  "));
            Assert.Equal("axte-schwerter-v2.json", result.FileName);
            var unnamed = library.ImportText(Valid.Replace("Two-Handed Fighter", "***"));
            Assert.False(unnamed.Ok == false && unnamed.FileName != null);
            Assert.Equal("build.json", unnamed.FileName);
        }
    }
}
