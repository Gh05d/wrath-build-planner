using System.Collections.Generic;

namespace WrathBuildPlanner.Core {
    /// <summary>Fixed game vocabulary and the spellings guides use. Canonical values are the game's enum member names.</summary>
    public static class Vocabulary {
        public static readonly string[] Attributes = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };

        static readonly List<KeyValuePair<string, string[]>> skillList = new List<KeyValuePair<string, string[]>>();
        static readonly List<KeyValuePair<string, string[]>> alignmentList = new List<KeyValuePair<string, string[]>>();

        /// <summary>Canonical skill → accepted spellings, in registration order (exported to site/data/vocabulary.json).</summary>
        public static IReadOnlyList<KeyValuePair<string, string[]>> SkillList => skillList;
        public static IReadOnlyList<KeyValuePair<string, string[]>> AlignmentList => alignmentList;

        static readonly Dictionary<string, string> AttributeNames = new Dictionary<string, string>();
        static readonly Dictionary<string, string> SkillNames = new Dictionary<string, string>();
        static readonly Dictionary<string, string> AlignmentNames = new Dictionary<string, string>();

        static Vocabulary() {
            foreach (string attribute in Attributes) {
                AttributeNames[NameMatcher.Normalize(attribute)] = attribute;
                AttributeNames[NameMatcher.Normalize(attribute.Substring(0, 3))] = attribute;
            }

            AddSkill("SkillAthletics", "Athletics");
            AddSkill("SkillMobility", "Mobility");
            AddSkill("SkillThievery", "Trickery", "Thievery");
            AddSkill("SkillStealth", "Stealth");
            AddSkill("SkillKnowledgeArcana", "Knowledge (Arcana)", "Knowledge Arcana", "Arcana");
            AddSkill("SkillKnowledgeWorld", "Knowledge (World)", "Knowledge World", "World");
            AddSkill("SkillLoreNature", "Lore (Nature)", "Lore Nature", "Nature");
            AddSkill("SkillLoreReligion", "Lore (Religion)", "Lore Religion", "Religion");
            AddSkill("SkillPerception", "Perception");
            AddSkill("SkillPersuasion", "Persuasion");
            AddSkill("SkillUseMagicDevice", "Use Magic Device", "UMD");

            AddAlignment("LawfulGood", "Lawful Good", "LG");
            AddAlignment("NeutralGood", "Neutral Good", "NG");
            AddAlignment("ChaoticGood", "Chaotic Good", "CG");
            AddAlignment("LawfulNeutral", "Lawful Neutral", "LN");
            AddAlignment("TrueNeutral", "True Neutral", "Neutral", "TN", "N");
            AddAlignment("ChaoticNeutral", "Chaotic Neutral", "CN");
            AddAlignment("LawfulEvil", "Lawful Evil", "LE");
            AddAlignment("NeutralEvil", "Neutral Evil", "NE");
            AddAlignment("ChaoticEvil", "Chaotic Evil", "CE");
        }

        static void AddSkill(string canonical, params string[] spellings) {
            skillList.Add(new KeyValuePair<string, string[]>(canonical, spellings));
            SkillNames[NameMatcher.Normalize(canonical)] = canonical;
            foreach (string spelling in spellings) SkillNames[NameMatcher.Normalize(spelling)] = canonical;
        }

        static void AddAlignment(string canonical, params string[] spellings) {
            alignmentList.Add(new KeyValuePair<string, string[]>(canonical, spellings));
            AlignmentNames[NameMatcher.Normalize(canonical)] = canonical;
            foreach (string spelling in spellings) AlignmentNames[NameMatcher.Normalize(spelling)] = canonical;
        }

        /// <summary>
        /// English names for a game enum member that names a feat's further choice (WeaponCategory, SpellSchool,
        /// StatType): the member itself, a skill's names as the game shows them ("SkillThievery" is "Trickery"),
        /// and the member without its "Skill" or "Weapon" prefix ("WeaponHeavyShield" is "Heavy Shield").
        /// </summary>
        public static string[] EnumMemberNames(string member) {
            if (string.IsNullOrEmpty(member)) return new string[0];
            var names = new List<string> { member };
            foreach (var skill in skillList) {
                if (skill.Key == member) names.AddRange(skill.Value);
            }
            foreach (string prefix in new[] { "Skill", "Weapon" }) {
                if (member.Length > prefix.Length && member.StartsWith(prefix)) names.Add(member.Substring(prefix.Length));
            }
            return names.ToArray();
        }

        public static bool TryAttribute(string text, out string canonical) =>
            AttributeNames.TryGetValue(NameMatcher.Normalize(text), out canonical);

        public static bool TrySkill(string text, out string canonical) =>
            SkillNames.TryGetValue(NameMatcher.Normalize(text), out canonical);

        public static bool TryAlignment(string text, out string canonical) =>
            AlignmentNames.TryGetValue(NameMatcher.Normalize(text), out canonical);
    }
}
