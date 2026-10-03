using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using WrathBuildPlanner.Core;
using WrathBuildPlanner.Models;

namespace WrathBuildPlanner.Persistence {
    public class LibraryEntry {
        public string FileName;
        public BuildFile Build;
        public List<ImportIssue> Issues = new List<ImportIssue>();
        public bool Ok => Build != null && !Issues.Any(i => i.IsError);
    }

    public class ImportResult {
        public bool Ok;
        public string FileName;
        public List<ImportIssue> Issues = new List<ImportIssue>();
    }

    /// <summary>The Builds folder: one JSON file per build, identified by file name. Pasted builds become files and never overwrite one.</summary>
    public class BuildLibrary {
        readonly string directory;
        readonly IKnownNames names;
        List<LibraryEntry> entries = new List<LibraryEntry>();

        public BuildLibrary(string directory, IKnownNames names) {
            this.directory = directory;
            this.names = names;
        }

        public IReadOnlyList<LibraryEntry> Entries => entries;

        public LibraryEntry Find(string fileName) =>
            fileName == null ? null : entries.FirstOrDefault(e => string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase));

        public void Reload() {
            Directory.CreateDirectory(directory);
            var loaded = new List<LibraryEntry>();
            foreach (string path in Directory.GetFiles(directory, "*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase)) {
                var entry = new LibraryEntry { FileName = Path.GetFileName(path) };
                // One unreadable or odd file must not take the rest of the library with it.
                try {
                    entry.Issues.AddRange(Check(File.ReadAllText(path), out entry.Build, out _));
                } catch (Exception e) {
                    entry.Build = null;
                    entry.Issues.Add(ImportIssue.Error("file", Messages.Get("import.unreadable", e.Message)));
                }
                loaded.Add(entry);
            }
            entries = loaded;
        }

        public ImportResult ImportText(string text) {
            var result = new ImportResult();
            result.Issues.AddRange(Check(text, out var build, out string clean));
            if (build == null || result.Issues.Any(i => i.IsError)) return result;

            try {
                Directory.CreateDirectory(directory);
                string slug = Slug(build.Name);
                string fileName = slug + ".json";
                for (int n = 2; File.Exists(Path.Combine(directory, fileName)); n++) fileName = $"{slug}-{n}.json";
                // The cleaned JSON is stored, so the file is valid JSON even if the paste came with a fence or prose.
                File.WriteAllText(Path.Combine(directory, fileName), clean);
                result.Ok = true;
                result.FileName = fileName;
            } catch (Exception e) {
                result.Issues.Add(ImportIssue.Error("file", Messages.Get("import.not_stored", e.Message)));
                return result;
            }
            Reload();
            return result;
        }

        List<ImportIssue> Check(string text, out BuildFile build, out string clean) {
            var parsed = BuildParser.Parse(text);
            build = parsed.Build;
            clean = parsed.CleanText;
            var issues = new List<ImportIssue>(parsed.Issues);
            if (parsed.Ok) issues.AddRange(BuildValidator.Validate(parsed.Build, names));
            return issues;
        }

        // "Äxte & Schwerter!! / v2" -> "axte-schwerter-v2"
        static string Slug(string name) {
            var builder = new StringBuilder();
            bool dash = false;
            foreach (char c in (name ?? "").Normalize(NormalizationForm.FormD)) {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (c < 128 && char.IsLetterOrDigit(c)) {
                    if (dash && builder.Length > 0) builder.Append('-');
                    builder.Append(char.ToLowerInvariant(c));
                    dash = false;
                } else {
                    dash = true;
                }
            }
            string slug = builder.ToString().Trim('-');
            if (slug.Length > MaxSlugLength) slug = slug.Substring(0, MaxSlugLength).Trim('-');
            if (slug.Length == 0) return "build";
            // Windows device names are not usable as file names (the game runs on Windows or under Proton).
            return ReservedNames.Contains(slug) ? "build-" + slug : slug;
        }

        const int MaxSlugLength = 60;
        static readonly HashSet<string> ReservedNames = new HashSet<string> {
            "con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
        };
    }
}
