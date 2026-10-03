using System.Globalization;
using System.Text;

namespace WrathBuildPlanner.Core {
    public static partial class NameMatcher {
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
    }
}
