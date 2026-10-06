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
                string message = Messages.Get("import.bad_json", e.LineNumber, e.LinePosition, FirstSentence(e.Message));
                if (json.IndexOfAny(TypographicQuotes) >= 0) message += " " + Messages.Get("import.smart_quotes");
                result.Issues.Add(ImportIssue.Error("file", message));
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

        static readonly char[] TypographicQuotes = { '\u201C', '\u201D', '\u201E' };
        static readonly Regex Fence = new Regex("```([^\\n`]*)\\n(.*?)```", RegexOptions.Singleline);

        // LLM output arrives with a BOM, inside Markdown code fences, with sentences or a second block ("Left out")
        // before and after; players paste the whole answer. Same order as the build page (site/js/extract.js):
        // the first ```json block, else the first fenced block holding an object, else the first {...} in the text.
        static string Clean(string text) {
            if (text == null) return "";
            string s = text.Trim().TrimStart('\uFEFF').Trim();
            return FromFence(s) ?? FromBraces(s) ?? s;
        }

        static string FromFence(string s) {
            string firstObject = null;
            foreach (Match block in Fence.Matches(s)) {
                string language = block.Groups[1].Value.Trim().ToLowerInvariant();
                string body = block.Groups[2].Value.Trim();
                if (language == "json" || language == "jsonc") return body;
                if (firstObject == null && body.StartsWith("{")) firstObject = body;
            }
            return firstObject;
        }

        // From the first "{" to its matching "}", skipping braces inside strings; unclosed: to the end.
        static string FromBraces(string s) {
            int start = s.IndexOf('{');
            if (start < 0) return null;
            int depth = 0;
            bool inString = false;
            for (int i = start; i < s.Length; i++) {
                char c = s[i];
                if (inString) {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0) return s.Substring(start, i - start + 1);
            }
            return s.Substring(start);
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
