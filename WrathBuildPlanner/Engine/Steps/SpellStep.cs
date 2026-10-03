using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using WrathBuildPlanner.Core;

namespace WrathBuildPlanner.Engine.Steps {
    /// <summary>
    /// Learns the row's spells in order. Runs interleaved with PickStep, because a pick (archetype, bloodline)
    /// can open spell slots and the spellbook can change.
    /// The game can drop spell picks again when a feature is selected afterwards (it replays all actions and
    /// logs "Invalid action: SelectSpell"), so nothing is remembered as done: every pass looks at what is
    /// really picked in the current state and selects what is missing.
    /// </summary>
    public class SpellStep {
        readonly List<string> wanted;
        readonly Dictionary<string, StepResult> failures = new Dictionary<string, StepResult>();
        readonly HashSet<string> selectedByThisRun = new HashSet<string>();

        public SpellStep(ApplyContext context) {
            wanted = (context.Row?.Spells ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        class Slot {
            public SpellSelectionData Data;
            public Spellbook Book;
            public int MinLevel;
            public int MaxLevel;
            public int Index;
        }

        /// <summary>Learns at most one spell. Returns true if it did.</summary>
        public bool Pass(ApplyContext context) {
            foreach (string name in wanted) {
                string label = "Spell: " + name;
                if (IsPickedNow(context, name)) continue;

                var slots = OpenSlots(context);
                if (slots.Count == 0) {
                    failures[name] = StepResult.Open(label, OpenReason.NoFreeSlot);
                    continue;
                }

                StepResult failure = null;
                foreach (var slot in slots) {
                    var allowed = Allowed(slot, out var candidates);
                    var outcome = NameMatcher.Match(name, candidates);
                    if (outcome.Kind != MatchKind.Unique) {
                        failure = failure ?? StepResult.Open(label,
                            outcome.Kind == MatchKind.Ambiguous ? OpenReason.Ambiguous : OpenReason.NotFound, null, outcome.Suggestions);
                        continue;
                    }
                    var chosen = (BlueprintAbility)outcome.Match.Tag;
                    int spellLevel = allowed[chosen];
                    if (slot.Book != null && slot.Data.SpellbookContainsSpell(slot.Book, spellLevel, chosen)) {
                        failure = StepResult.Open(label, OpenReason.NotSelectable, "already known");
                        continue;
                    }
                    if (context.Controller.SelectSpell(slot.Data.Spellbook, slot.Data.SpellList, spellLevel, chosen, slot.Index)) {
                        selectedByThisRun.Add(name);
                        failures.Remove(name);
                        return true;
                    }
                    failure = StepResult.Open(label, OpenReason.NotSelectable);
                }
                failures[name] = failure ?? StepResult.Open(label, OpenReason.NoFreeSlot);
            }
            return false;
        }

        /// <summary>Reports from the state as it is at the end, not from what was selected along the way.</summary>
        public void Report(ApplyContext context) {
            foreach (string name in wanted) {
                string label = "Spell: " + name;
                if (IsPickedNow(context, name)) {
                    context.Report.Steps.Add(selectedByThisRun.Contains(name) ? StepResult.Applied(label) : StepResult.Already(label));
                } else {
                    context.Report.Steps.Add(failures.TryGetValue(name, out var failure) ? failure : StepResult.Open(label, OpenReason.NoFreeSlot));
                }
            }
        }

        /// <summary>Names of the spells currently picked in this level, for callers that want to detect drops.</summary>
        public static List<string> PickedNow(ApplyContext context) {
            var picked = new List<string>();
            foreach (var data in context.State.SpellSelections) {
                if (data.ExtraSelected != null) picked.AddRange(data.ExtraSelected.Where(s => s != null).Select(s => s.name));
                foreach (var level in data.LevelCount) {
                    if (level?.SpellSelections != null) picked.AddRange(level.SpellSelections.Where(s => s != null).Select(s => s.name));
                }
            }
            return picked;
        }

        static bool IsPickedNow(ApplyContext context, string name) {
            foreach (var data in context.State.SpellSelections) {
                var picked = new List<NameCandidate>();
                if (data.ExtraSelected != null) picked.AddRange(data.ExtraSelected.Where(s => s != null).Select(GameNames.OfSpell));
                foreach (var level in data.LevelCount) {
                    if (level?.SpellSelections != null) picked.AddRange(level.SpellSelections.Where(s => s != null).Select(GameNames.OfSpell));
                }
                if (NameMatcher.Match(name, picked).Kind != MatchKind.None) return true;
            }
            return false;
        }

        // Spells the slot may take, highest level first, with the level each belongs to.
        static Dictionary<BlueprintAbility, int> Allowed(Slot slot, out List<NameCandidate> candidates) {
            candidates = new List<NameCandidate>();
            var allowed = new Dictionary<BlueprintAbility, int>();
            for (int level = slot.MaxLevel; level >= slot.MinLevel; level--) {
                foreach (var spell in slot.Data.SpellList.GetSpells(level)) {
                    if (allowed.ContainsKey(spell)) continue;
                    allowed[spell] = level;
                    candidates.Add(GameNames.OfSpell(spell));
                }
            }
            return allowed;
        }

        // First empty slot of every page: per spell level for spontaneous casters, the "extra" array for prepared ones.
        static List<Slot> OpenSlots(ApplyContext context) {
            var slots = new List<Slot>();
            foreach (var data in context.State.SpellSelections) {
                var book = context.Controller.Preview.Descriptor.Spellbooks.FirstOrDefault(b => b.Blueprint == data.Spellbook);
                if (book != null && book.Blueprint.AllSpellsKnown) continue;
                for (int level = 0; level < data.LevelCount.Length; level++) {
                    var perLevel = data.LevelCount[level]?.SpellSelections;
                    if (perLevel == null) continue;
                    int index = Array.FindIndex(perLevel, s => s == null);
                    if (index >= 0) slots.Add(new Slot { Data = data, Book = book, MinLevel = level, MaxLevel = level, Index = index });
                }
                if (data.ExtraSelected != null) {
                    int index = Array.FindIndex(data.ExtraSelected, s => s == null);
                    if (index >= 0) slots.Add(new Slot { Data = data, Book = book, MinLevel = 1, MaxLevel = data.ExtraMaxLevel, Index = index });
                }
            }
            return slots;
        }
    }
}
