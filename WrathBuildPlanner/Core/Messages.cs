using System;
using System.Collections.Generic;

namespace WrathBuildPlanner.Core {
    /// <summary>
    /// Every text the mod produces about a build: import problems and the lines of an apply report.
    /// English lives here so Core stays free of game types; the mod plugs in a translator that returns
    /// the template of the player's language (keys are prefixed "msg." in the language files).
    /// </summary>
    public static class Messages {
        /// <summary>Returns the translated template for a key, or null to use English.</summary>
        public static Func<string, string> Translate;

        static readonly Dictionary<string, string> English = new Dictionary<string, string> {
            { "import.empty", "The text is empty." },
            { "import.not_object", "The text is not a build object." },
            { "import.bad_json", "Not valid JSON at line {0}, position {1}: {2}" },
            { "import.bad_structure", "The build's structure is wrong: {0}" },
            { "import.at", "{0} (line {1}, position {2})" },
            { "import.unreadable", "The file could not be read: {0}" },
            { "import.not_stored", "The build could not be stored: {0}" },
            { "pick.bad_token", "A pick must be a name or an object, found {0}." },
            { "pick.unknown_field", "Unknown field '{0}' in a pick (allowed: in, pick)." },
            { "pick.bad_value", "'{0}' in a pick must be text, found {1}." },
            { "validate.format", "This build uses format {0}; this version of the mod reads format {1}." },
            { "validate.no_name", "The build has no name." },
            { "validate.nothing", "The build has nothing to apply: no start block, no levels, no mythic ranks." },
            { "validate.start_without_level1", "The start block is only used together with an entry for level 1." },
            { "validate.race", "Unknown race '{0}'." },
            { "validate.not_attribute", "'{0}' is not an attribute." },
            { "validate.alignment", "Unknown alignment '{0}'." },
            { "validate.score_range", "{0} is {1}; starting scores go from 7 to 18 (before the racial bonus)." },
            { "validate.skill", "Unknown skill '{0}'." },
            { "validate.empty_row", "An entry in '{0}' is empty." },
            { "validate.level_range", "Level {0} is out of range; character levels go from 1 to 20." },
            { "validate.level_twice", "The build lists level {0} twice." },
            { "validate.level_order", "Levels must be in ascending order; level {0} comes after level {1}." },
            { "validate.no_class", "The level has no class." },
            { "validate.class", "Unknown class '{0}'." },
            { "validate.point_level", "An attribute point is only granted every fourth level; this entry will be ignored." },
            { "validate.empty_spell", "A spell name is empty." },
            { "validate.rank_range", "Mythic rank {0} is out of range; ranks go from 1 to 10." },
            { "validate.rank_twice", "The build lists mythic rank {0} twice." },
            { "validate.rank_order", "Mythic ranks must be in ascending order; rank {0} comes after rank {1}." },
            { "validate.empty_pick", "A pick is empty or has an empty name in its chain." },
            { "step.custom", "Custom character" },
            { "step.race", "Race: {0}" },
            { "step.race_bonus", "Racial bonus: {0}" },
            { "step.scores", "Ability scores: {0}" },
            { "step.scores_missed", "{0} {1} instead of {2}" },
            { "step.alignment", "Alignment: {0}" },
            { "step.alignment_class", "not allowed for this class" },
            { "step.class", "Class: {0}" },
            { "step.class_denied", "the game does not allow this class now" },
            { "step.rest_skipped", "Skills, picks and spells were not applied because the class could not be set." },
            { "step.archetype", "Archetype: {0}" },
            { "step.no_class", "no class selected" },
            { "step.archetype_first", "only possible on the first level of the class" },
            { "step.point", "Attribute point: {0}" },
            { "step.skills", "Skills: {0} point(s) spent" },
            { "step.skills_left", "{0} point(s) left" },
            { "step.spell", "Spell: {0}" },
            { "step.known", "already known" },
            { "step.path", "Mythic path: {0}" },
            { "step.path_open", "Mythic path" },
            { "step.path_locked", "the game does not offer this path now" },
            { "step.no_further", "'{0}' offers no further choice" },
            { "step.failed", "{0}" },
        };

        public static IEnumerable<string> Keys => English.Keys;

        public static string Get(string key, params object[] args) {
            string template = null;
            try {
                template = Translate?.Invoke(key);
            } catch (Exception) {
                template = null;
            }
            if (template == null && !English.TryGetValue(key, out template)) return key;
            if (args == null || args.Length == 0) return template;
            try {
                return string.Format(template, args);
            } catch (FormatException) {
                return template;
            }
        }
    }
}
