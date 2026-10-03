using System.Collections.Generic;
using System.Linq;
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
        public List<ImportIssue> Issues = new List<ImportIssue>();
        public bool Ok => Build != null && !Issues.Any(i => i.IsError);
    }

    /// <summary>Text to BuildFile. Checks form only; meaning is BuildValidator's job.</summary>
    public static class BuildParser {
        public static ParseResult Parse(string text) {
            var result = new ParseResult();
            string json = Clean(text);
            if (json.Length == 0) {
                result.Issues.Add(ImportIssue.Error("file", "The text is empty."));
                return result;
            }

            var settings = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };
            try {
                result.Build = JsonConvert.DeserializeObject<BuildFile>(json, settings);
                if (result.Build == null) result.Issues.Add(ImportIssue.Error("file", "The text is not a build object."));
            } catch (JsonReaderException e) {
                result.Issues.Add(ImportIssue.Error("file", $"Not valid JSON at line {e.LineNumber}, position {e.LinePosition}: {FirstSentence(e.Message)}"));
            } catch (JsonSerializationException e) {
                result.Issues.Add(ImportIssue.Error("file", FirstSentence(e.Message)));
            }
            return result;
        }

        // LLM output often arrives with a BOM or inside a Markdown code fence.
        static string Clean(string text) {
            if (text == null) return "";
            string s = text.Trim().TrimStart('﻿').Trim();
            if (s.StartsWith("```")) {
                int firstBreak = s.IndexOf('\n');
                int lastFence = s.LastIndexOf("```");
                if (firstBreak >= 0 && lastFence > firstBreak) s = s.Substring(firstBreak + 1, lastFence - firstBreak - 1).Trim();
            }
            return s;
        }

        // Newtonsoft appends "Path '…', line N, position M." — keep the human part.
        static string FirstSentence(string message) {
            int cut = message.IndexOf(" Path '");
            return cut > 0 ? message.Substring(0, cut) : message;
        }
    }
}
