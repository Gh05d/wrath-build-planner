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
                try {
                    entry.Issues.AddRange(Check(File.ReadAllText(path), out entry.Build));
                } catch (IOException e) {
                    entry.Issues.Add(ImportIssue.Error("file", "The file could not be read: " + e.Message));
                }
                loaded.Add(entry);
            }
            entries = loaded;
        }

        public ImportResult ImportText(string text) {
            var result = new ImportResult();
            result.Issues.AddRange(Check(text, out var build));
            if (build == null || result.Issues.Any(i => i.IsError)) return result;

            Directory.CreateDirectory(directory);
            string slug = Slug(build.Name);
            string fileName = slug + ".json";
            for (int n = 2; File.Exists(Path.Combine(directory, fileName)); n++) fileName = $"{slug}-{n}.json";
            File.WriteAllText(Path.Combine(directory, fileName), text.Trim().TrimStart('﻿'));
            result.Ok = true;
            result.FileName = fileName;
            Reload();
            return result;
        }

        List<ImportIssue> Check(string text, out BuildFile build) {
            var parsed = BuildParser.Parse(text);
            build = parsed.Build;
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
            return builder.Length > 0 ? builder.ToString() : "build";
        }
    }
}
