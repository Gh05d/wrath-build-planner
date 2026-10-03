using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WrathBuildPlanner.Core {
    public class NameCandidate {
        public object Tag;
        public string[] Names;
        public string Display;
    }

    public enum MatchKind { Unique, None, Ambiguous }

    public class MatchOutcome {
        public MatchKind Kind;
        public NameCandidate Match;
        public List<NameCandidate> Tied = new List<NameCandidate>();
        public List<string> Suggestions = new List<string>();
    }

    /// <summary>
    /// Staged, exact matching of a guide's name against the candidates of one selection.
    /// Stage 1: any identity equals the wanted name after normalization.
    /// Stage 2: the identity with its category removed ("Specialist School — Evocation" -> "Evocation",
    ///          "Metamagic (Empower Spell)" -> "Empower Spell").
    /// The first stage with exactly one candidate decides; several in one stage is an ambiguity.
    /// Similarity only produces suggestions and never a match.
    /// </summary>
    public static partial class NameMatcher {
        const int MaxSuggestions = 3;
        static readonly string[] CategorySeparators = { " — ", " – ", " - " };

        /// <summary>Lower-case letters and digits only; accents removed. "Cat’s Grace" and "cats grace" compare equal.</summary>
        public static string Normalize(string text) {
            if (string.IsNullOrEmpty(text)) return "";
            var builder = new StringBuilder(text.Length);
            foreach (char c in text.Normalize(NormalizationForm.FormD)) {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
            }
            return builder.ToString();
        }

        /// <summary>The part after a category prefix, or null when the name has none.</summary>
        public static string StripCategory(string name) {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (string separator in CategorySeparators) {
                int at = name.IndexOf(separator, StringComparison.Ordinal);
                if (at > 0 && at + separator.Length < name.Length) return name.Substring(at + separator.Length);
            }
            if (TrySplitParenChain(name, out _, out string inner)) return inner;
            return null;
        }

        /// <summary>"Weapon Focus (Greatsword)" -> head "Weapon Focus", tail "Greatsword".</summary>
        public static bool TrySplitParenChain(string text, out string head, out string tail) {
            head = null;
            tail = null;
            if (string.IsNullOrEmpty(text)) return false;
            string trimmed = text.Trim();
            int open = trimmed.IndexOf('(');
            if (open <= 0 || !trimmed.EndsWith(")")) return false;
            head = trimmed.Substring(0, open).Trim();
            tail = trimmed.Substring(open + 1, trimmed.Length - open - 2).Trim();
            return head.Length > 0 && tail.Length > 0;
        }

        public static MatchOutcome Match(string wanted, IList<NameCandidate> candidates) {
            var outcome = new MatchOutcome { Kind = MatchKind.None };
            string key = Normalize(wanted);
            if (key.Length == 0 || candidates == null || candidates.Count == 0) return outcome;

            var exact = candidates.Where(c => c.Names.Any(n => Normalize(n) == key)).ToList();
            if (Decide(exact, outcome)) return outcome;

            var stripped = candidates.Where(c => c.Names.Any(n => Normalize(StripCategory(n)) == key)).ToList();
            if (Decide(stripped, outcome)) return outcome;

            outcome.Suggestions = Suggest(key, candidates);
            return outcome;
        }

        static bool Decide(List<NameCandidate> hits, MatchOutcome outcome) {
            if (hits.Count == 0) return false;
            if (hits.Count == 1) {
                outcome.Kind = MatchKind.Unique;
                outcome.Match = hits[0];
            } else {
                outcome.Kind = MatchKind.Ambiguous;
                outcome.Tied = hits;
            }
            return true;
        }

        // Hints for the human only: names that contain the wanted text (or vice versa), then near-typos.
        static List<string> Suggest(string key, IList<NameCandidate> candidates) {
            var scored = new List<KeyValuePair<int, string>>();
            foreach (var candidate in candidates) {
                int best = int.MaxValue;
                foreach (string name in candidate.Names) {
                    string n = Normalize(name);
                    if (n.Length == 0) continue;
                    if (n.Contains(key) || key.Contains(n)) {
                        best = 0;
                        break;
                    }
                    int distance = EditDistance(key, n);
                    if (distance < best) best = distance;
                }
                int allowed = Math.Max(2, key.Length / 4);
                if (best <= allowed) scored.Add(new KeyValuePair<int, string>(best, candidate.Display));
            }
            return scored.OrderBy(s => s.Key).Select(s => s.Value).Distinct().Take(MaxSuggestions).ToList();
        }

        static int EditDistance(string a, string b) {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previous[j] = j;
            for (int i = 1; i <= a.Length; i++) {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++) {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }
                var swap = previous;
                previous = current;
                current = swap;
            }
            return previous[b.Length];
        }
    }
}
