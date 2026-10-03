using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using WrathBuildPlanner.Models;

namespace WrathBuildPlanner.Core {
    public class ImportIssue {
        public bool IsError;
        public string Where;
        public string Message;

        public static ImportIssue Error(string where, string message) =>
            new ImportIssue { IsError = true, Where = where, Message = message };

        public static ImportIssue Warning(string where, string message) =>
            new ImportIssue { IsError = false, Where = where, Message = message };
    }

    public class ParseResult {
        public BuildFile Build;
        /// <summary>The JSON that was actually parsed: the input without BOM, prose and Markdown fence.</summary>
        public string CleanText;
        public List<ImportIssue> Issues = new List<ImportIssue>();
        public bool Ok => Build != null && !Issues.Any(i => i.IsError);
    }

    /// <summary>Text to BuildFile. Checks form only; meaning is BuildValidator's job. Never throws.</summary>
    public static class BuildParser {
        static readonly Regex Location = new Regex(@"line (\d+), position (\d+)");

        public static ParseResult Parse(string text) {
            var result = new ParseResult();
            string json = Clean(text);
            result.CleanText = json;
            if (json.Length == 0) {
                result.Issues.Add(ImportIssue.Error("file", Messages.Get("import.empty")));
                return result;
            }
            if (!json.StartsWith("{")) {
                result.Issues.Add(ImportIssue.Error("file", Messages.Get("import.not_object")));
                return result;
            }

            var settings = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };
            try {
                result.Build = JsonConvert.DeserializeObject<BuildFile>(json, settings);
                if (result.Build == null) result.Issues.Add(ImportIssue.Error("file", Messages.Get("import.not_object")));
                else Normalise(result.Build);
            } catch (JsonReaderException e) {
                result.Build = null;
                result.Issues.Add(ImportIssue.Error("file", Messages.Get("import.bad_json", e.LineNumber, e.LinePosition, FirstSentence(e.Message))));
            } catch (Exception e) {
                // JsonSerializationException for unknown fields and wrong shapes; anything else a converter or cast throws.
                result.Build = null;
                result.Issues.Add(ImportIssue.Error("file", WithLocation(Messages.Get("import.bad_structure", FirstSentence(e.Message)), e.Message)));
            }
            return result;
        }

        // An explicit JSON null for a list would otherwise survive as a null reference.
        static void Normalise(BuildFile build) {
            build.Levels = build.Levels ?? new List<LevelRow>();
            build.Mythic = build.Mythic ?? new List<MythicRow>();
            foreach (var row in build.Levels) {
                if (row == null) continue;
                row.Picks = row.Picks ?? new List<PickEntry>();
                row.Spells = row.Spells ?? new List<string>();
            }
            foreach (var row in build.Mythic) {
                if (row != null) row.Picks = row.Picks ?? new List<PickEntry>();
            }
        }

        // LLM output arrives with a BOM, inside a Markdown code fence, often with a sentence before and after.
        // A fenced block anywhere in the text is taken as the build.
        static string Clean(string text) {
            if (text == null) return "";
            string s = text.Trim().TrimStart('\uFEFF').Trim();
            int fence = s.IndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) {
                int firstBreak = s.IndexOf('\n', fence);
                int lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
                if (firstBreak >= 0 && lastFence > firstBreak) s = s.Substring(firstBreak + 1, lastFence - firstBreak - 1).Trim();
            }
            return s;
        }

        // Newtonsoft appends "Path '…', line N, position M." and sometimes a multi-line explanation — keep the first sentence.
        static string FirstSentence(string message) {
            string line = message.Split('\n')[0].Trim();
            int cut = line.IndexOf(" Path '", StringComparison.Ordinal);
            return cut > 0 ? line.Substring(0, cut) : line;
        }

        static string WithLocation(string text, string rawMessage) {
            var match = Location.Match(rawMessage);
            return match.Success ? Messages.Get("import.at", text, match.Groups[1].Value, match.Groups[2].Value) : text;
        }
    }
}
