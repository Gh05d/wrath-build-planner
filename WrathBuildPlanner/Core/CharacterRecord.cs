using System.Collections.Generic;
using WrathBuildPlanner.Models;

namespace WrathBuildPlanner.Core {
    /// <summary>What the game recorded about a character's levels, with English names. Filled by Engine.CharacterReader.</summary>
    public class CharacterRecord {
        public string Name;
        /// <summary>English race name; null when the race cannot be picked in creation.</summary>
        public string Race;
        /// <summary>The character's level; more than Levels.Count when the game did not record every level (preset-levelled units).</summary>
        public int CharacterLevel;
        public List<RecordLevel> Levels = new List<RecordLevel>();
        public List<RecordRank> Mythic = new List<RecordRank>();
        /// <summary>Canonical skill (Vocabulary key) to ranks.</summary>
        public Dictionary<string, int> SkillRanks = new Dictionary<string, int>();
        /// <summary>Picks that could not be read (a feature from another mod that throws).</summary>
        public int SkippedPicks;
        /// <summary>Known spells that could not be placed on a level.</summary>
        public bool SpellsIncomplete;
        /// <summary>Legend levels above 20, which the build format cannot hold.</summary>
        public bool LegendLevelsLeftOut;
    }

    public class RecordLevel {
        public int Level;
        public string Class;
        public string Archetype;
        public List<PickEntry> Picks = new List<PickEntry>();
        public List<string> Spells = new List<string>();
    }

    public class RecordRank {
        public int Rank;
        public string Path;
        public List<PickEntry> Picks = new List<PickEntry>();
    }
}
