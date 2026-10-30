using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using WrathBuildPlanner.Models;

namespace WrathBuildPlanner.Core {
    /// <summary>
    /// A recorded character as a build file. What the game does not record (point-buy, ability increases)
    /// stays out of the file, so the planner leaves those pages to the player.
    /// </summary>
    public static class BuildExporter {
        public static BuildFile ToBuild(CharacterRecord record) {
            var build = new BuildFile {
                Format = BuildValidator.SupportedFormat,
                Name = record.Name + " (export)",
                For = record.Name,
                Start = record.Race != null ? new StartBlock { Race = record.Race } : null,
                Skills = Skills(record.SkillRanks),
            };
            foreach (var level in record.Levels) {
                build.Levels.Add(new LevelRow {
                    Level = level.Level, Class = level.Class, Archetype = level.Archetype,
                    Picks = level.Picks.ToList(), Spells = level.Spells.ToList(),
                });
            }
            foreach (var rank in record.Mythic) {
                build.Mythic.Add(new MythicRow { Rank = rank.Rank, Path = rank.Path, Picks = rank.Picks.ToList() });
            }
            return build;
        }

        // Highest rank first; ties keep the vocabulary's order. The format's skills are a priority list.
        static List<string> Skills(Dictionary<string, int> ranks) {
            if (ranks == null) return null;
            var skills = Vocabulary.SkillList
                .Select((skill, index) => new { Name = skill.Value[0], Ranks = ranks.TryGetValue(skill.Key, out int r) ? r : 0, index })
                .Where(s => s.Ranks > 0)
                .OrderByDescending(s => s.Ranks).ThenBy(s => s.index)
                .Select(s => s.Name).ToList();
            return skills.Count > 0 ? skills : null;
        }

        public static string ToJson(BuildFile build) =>
            JsonConvert.SerializeObject(build, new JsonSerializerSettings {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new SkipEmptyLists(),
            });

        /// <summary>Message keys for what the file leaves open, in the order the export window lists them.</summary>
        public static List<string> Gaps(CharacterRecord record, BuildFile build) {
            var gaps = new List<string>();
            if (record.Levels.Count < record.CharacterLevel) gaps.Add("export.gap.history");
            if (build.Levels.Any(r => r.Level == 1)) gaps.Add("export.gap.scores");
            if (build.Levels.Any(r => r.Level % 4 == 0)) gaps.Add("export.gap.ability_points");
            if (record.SpellsIncomplete) gaps.Add("export.gap.spells");
            return gaps;
        }

        class SkipEmptyLists : DefaultContractResolver {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization) {
                var property = base.CreateProperty(member, serialization);
                if (typeof(ICollection).IsAssignableFrom(property.PropertyType)) {
                    var getter = property.ValueProvider;
                    property.ShouldSerialize = owner => getter.GetValue(owner) is ICollection list && list.Count > 0;
                }
                return property;
            }
        }
    }
}
