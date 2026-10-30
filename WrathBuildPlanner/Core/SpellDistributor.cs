using System;
using System.Collections.Generic;
using System.Linq;

namespace WrathBuildPlanner.Core {
    public class Distribution {
        /// <summary>Character level to the spells learned on it.</summary>
        public Dictionary<int, List<string>> ByLevel = new Dictionary<int, List<string>>();
        /// <summary>Known spells that found no slot.</summary>
        public int Dropped;
    }

    /// <summary>
    /// The game stores which spells a character knows, not the level each was learned on. This places them on
    /// the levels that offered a pick, in the game's own counts, so the planner can learn them again.
    /// </summary>
    public static class SpellDistributor {
        /// <param name="classOfLevel">class of character level i + 1</param>
        /// <param name="newKnown">(caster level, spell level) → new picks of exactly that spell level</param>
        /// <param name="freePicks">caster level → picks of any castable level (wizard), 0 for spontaneous books</param>
        /// <param name="maxSpellLevel">caster level → highest castable spell level</param>
        public static Distribution Distribute(IList<string> classOfLevel, string spellbookClass,
            IDictionary<int, List<string>> knownBySpellLevel,
            Func<int, int, int> newKnown, Func<int, int> freePicks, Func<int, int> maxSpellLevel) {
            var queues = knownBySpellLevel.ToDictionary(kv => kv.Key, kv => new Queue<string>(kv.Value));
            var result = new Distribution();
            int casterLevel = 0;
            for (int i = 0; i < classOfLevel.Count; i++) {
                if (classOfLevel[i] != spellbookClass) continue;
                casterLevel++;
                var learned = new List<string>();
                foreach (var queue in queues.OrderBy(q => q.Key)) {
                    for (int n = newKnown(casterLevel, queue.Key); n > 0 && queue.Value.Count > 0; n--) learned.Add(queue.Value.Dequeue());
                }
                int max = maxSpellLevel(casterLevel);
                for (int n = freePicks(casterLevel); n > 0; n--) {
                    var queue = queues.Where(q => q.Key <= max && q.Value.Count > 0).OrderByDescending(q => q.Key).Select(q => q.Value).FirstOrDefault();
                    if (queue == null) break;
                    learned.Add(queue.Dequeue());
                }
                if (learned.Count > 0) result.ByLevel[i + 1] = learned;
            }
            result.Dropped = queues.Values.Sum(q => q.Count);
            return result;
        }
    }
}
