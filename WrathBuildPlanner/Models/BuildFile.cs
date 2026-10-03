using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WrathBuildPlanner.Core;

namespace WrathBuildPlanner.Models {
    public class BuildFile {
        [JsonProperty("format")] public int Format;
        [JsonProperty("name")] public string Name;
        [JsonProperty("author")] public string Author;
        [JsonProperty("source")] public string Source;
        [JsonProperty("for")] public string For;
        [JsonProperty("start")] public StartBlock Start;
        [JsonProperty("skills")] public List<string> Skills;
        [JsonProperty("levels")] public List<LevelRow> Levels = new List<LevelRow>();
        [JsonProperty("mythic")] public List<MythicRow> Mythic = new List<MythicRow>();
    }

    public class StartBlock {
        [JsonProperty("race")] public string Race;
        [JsonProperty("raceBonus")] public string RaceBonus;
        [JsonProperty("abilityScores")] public Dictionary<string, int> AbilityScores;
        [JsonProperty("alignment")] public string Alignment;
    }

    public class LevelRow {
        [JsonProperty("level")] public int Level;
        [JsonProperty("class")] public string Class;
        [JsonProperty("archetype")] public string Archetype;
        [JsonProperty("abilityPoint")] public string AbilityPoint;
        [JsonProperty("skills")] public List<string> Skills;
        [JsonProperty("picks")] public List<PickEntry> Picks = new List<PickEntry>();
        [JsonProperty("spells")] public List<string> Spells = new List<string>();
    }

    public class MythicRow {
        [JsonProperty("rank")] public int Rank;
        [JsonProperty("path")] public string Path;
        [JsonProperty("picks")] public List<PickEntry> Picks = new List<PickEntry>();
    }

    /// <summary>One pick: optional selection scope plus a chain of names (parent first).</summary>
    [JsonConverter(typeof(PickEntryConverter))]
    public class PickEntry {
        public string In;
        public List<string> Pick = new List<string>();

        public string Label => (In != null ? In + ": " : "") + string.Join(" > ", Pick);
    }

    /// <summary>Accepts "Name", { "in": .., "pick": "Name" } and { "in": .., "pick": ["A", "B"] }.</summary>
    public class PickEntryConverter : JsonConverter {
        public override bool CanConvert(Type objectType) => objectType == typeof(PickEntry);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            if (reader.TokenType == JsonToken.Null) return null;
            var entry = new PickEntry();
            if (reader.TokenType == JsonToken.String) {
                entry.Pick.Add((string)reader.Value);
                return entry;
            }
            if (reader.TokenType != JsonToken.StartObject)
                throw new JsonSerializationException(Messages.Get("pick.bad_token", reader.TokenType));

            var obj = JObject.Load(reader);
            foreach (var property in obj.Properties()) {
                switch (property.Name) {
                    case "in":
                        if (property.Value.Type == JTokenType.Null) break;
                        entry.In = Text(property.Value, "in");
                        break;
                    case "pick":
                        if (property.Value.Type == JTokenType.Array) {
                            foreach (var item in (JArray)property.Value) entry.Pick.Add(Text(item, "pick"));
                        } else {
                            entry.Pick.Add(Text(property.Value, "pick"));
                        }
                        break;
                    default:
                        throw new JsonSerializationException(Messages.Get("pick.unknown_field", property.Name));
                }
            }
            return entry;
        }

        // Only text is a name; a nested object or list must not surface as a cast exception.
        static string Text(JToken token, string field) {
            if (token.Type != JTokenType.String) throw new JsonSerializationException(Messages.Get("pick.bad_value", field, token.Type));
            return (string)token;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            var entry = (PickEntry)value;
            writer.WriteStartObject();
            if (entry.In != null) {
                writer.WritePropertyName("in");
                writer.WriteValue(entry.In);
            }
            writer.WritePropertyName("pick");
            if (entry.Pick.Count == 1) {
                writer.WriteValue(entry.Pick[0]);
            } else {
                writer.WriteStartArray();
                foreach (string name in entry.Pick) writer.WriteValue(name);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
    }
}
