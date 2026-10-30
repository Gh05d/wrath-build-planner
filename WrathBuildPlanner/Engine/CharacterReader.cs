using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Localization;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.FactLogic;
using WrathBuildPlanner.Core;
using WrathBuildPlanner.Models;
using MenuLabels = Kingmaker.UI.MVVM._VM.CharGen.Phases.FeatureSelector.FeatureSelectionExtensions;

namespace WrathBuildPlanner.Engine {
    /// <summary>
    /// Reads what the game recorded about a unit's levels into a CharacterRecord. Read-only on the unit.
    /// Engine facts this relies on: CLAUDE.md "Export facts (2026-10-30)".
    /// </summary>
    static class CharacterReader {
        /// <summary>Where a level of the history went: a character level or a mythic rank.</summary>
        struct Slot {
            public bool Mythic;
            public int Number;
        }

        public static CharacterRecord Read(UnitEntityData unit) {
            var progression = unit.Progression;
            var order = progression.m_ClassesOrder.ToList();
            var record = new CharacterRecord {
                Name = unit.CharacterName,
                CharacterLevel = progression.CharacterLevel,
                Race = RaceName(progression.Race),
            };

            // History position of every class level: the n-th entry of a class in m_ClassesOrder is its level n.
            var slots = new List<Slot>();
            int levels = 0, ranks = 0;
            foreach (var cls in order) slots.Add(cls.IsMythic ? new Slot { Mythic = true, Number = ++ranks } : new Slot { Number = ++levels });

            var seenClasses = new HashSet<BlueprintCharacterClass>();
            BlueprintCharacterClass firstMythic = null;
            var paths = Game.Instance.BlueprintRoot.Progression.CharacterMythics.ToList();
            for (int i = 0; i < order.Count; i++) {
                var cls = order[i];
                if (cls.IsMythic) {
                    if (firstMythic == null) firstMythic = cls;
                    var rank = new RecordRank { Rank = slots[i].Number };
                    if (cls != firstMythic && paths.Contains(cls) && seenClasses.Add(cls)) rank.Path = En(cls.LocalizedName, cls.Name);
                    record.Mythic.Add(rank);
                    continue;
                }
                var row = new RecordLevel { Level = slots[i].Number, Class = En(cls.LocalizedName, cls.Name) };
                if (seenClasses.Add(cls)) {
                    var archetype = progression.Classes.Where(c => c.CharacterClass == cls).SelectMany(c => c.Archetypes).FirstOrDefault();
                    if (archetype != null) row.Archetype = En(archetype.LocalizedName, archetype.Name);
                }
                record.Levels.Add(row);
            }

            ReadPicks(unit, order, slots, record);
            foreach (var skill in Vocabulary.SkillList) {
                if (Enum.TryParse(skill.Key, out StatType stat)) record.SkillRanks[skill.Key] = unit.Stats.GetStat(stat).BaseValue;
            }
            ReadSpells(unit, order, record);
            return record;
        }

        static void ReadPicks(UnitEntityData unit, List<BlueprintCharacterClass> order, List<Slot> slots, CharacterRecord record) {
            var progression = unit.Progression;
            var feats = Game.Instance.BlueprintRoot.Progression.FeatsProgression;
            // A selection whose item is itself a selection (Background > Scholar > Arcane) is one chain, read from the outer one.
            var nested = new HashSet<BlueprintFeatureSelection>(progression.Selections
                .SelectMany(s => s.Value.SelectionsByLevel.Values.SelectMany(l => l))
                .OfType<BlueprintFeatureSelection>().Where(progression.Selections.ContainsKey));
            var used = new HashSet<(Feature, int)>();

            foreach (var entry in progression.Selections) {
                var selection = entry.Key;
                if (nested.Contains(selection)) continue;
                foreach (var byLevel in entry.Value.SelectionsByLevel) {
                    foreach (var feature in byLevel.Value) {
                        try {
                            var source = FindSource(unit, feature, byLevel.Key, used);
                            if (source == null) continue;
                            var slot = SlotOf(source.Value.Blueprint, source.Value.Level, feats, order, slots);
                            if (slot == null) continue;   // not a level-up page: dialog, story event, another mod's script
                            var pick = new PickEntry { In = PageTitle(selection), Pick = Chain(unit, feature, source.Value.Fact) };
                            if (slot.Value.Mythic) record.Mythic.FirstOrDefault(r => r.Rank == slot.Value.Number)?.Picks.Add(pick);
                            else record.Levels.FirstOrDefault(r => r.Level == slot.Value.Number)?.Picks.Add(pick);
                        } catch (Exception e) {
                            record.SkippedPicks++;
                            Logging.Log.Engine.Warn($"Export: {unit.CharacterName}: pick '{feature?.name}' in '{selection?.name}' skipped: {e.Message}");
                        }
                    }
                }
            }
        }

        struct Source {
            public Feature Fact;
            public BlueprintScriptableObject Blueprint;
            public int Level;
        }

        // The selection's own Source is only its first one; the Feature fact knows where each rank came from.
        static Source? FindSource(UnitEntityData unit, BlueprintFeature feature, int key, HashSet<(Feature, int)> used) {
            foreach (var fact in unit.Progression.Features.Enumerable.Where(f => f.Blueprint == feature)) {
                var sources = fact.m_RankToSource != null && fact.m_RankToSource.Count > 0
                    ? fact.m_RankToSource.Select(s => (s.Blueprint, s.Level)).ToList()
                    : new List<(BlueprintScriptableObject, int)> { (fact.m_Source, fact.SourceLevel) };
                for (int i = 0; i < sources.Count; i++) {
                    if (sources[i].Item2 != key || used.Contains((fact, i))) continue;
                    used.Add((fact, i));
                    return new Source { Fact = fact, Blueprint = sources[i].Item1, Level = sources[i].Item2 };
                }
            }
            return null;
        }

        static Slot? SlotOf(BlueprintScriptableObject source, int level, BlueprintProgression feats,
            List<BlueprintCharacterClass> order, List<Slot> slots) {
            if (source is BlueprintRace) return new Slot { Number = 1 };
            if (source == feats) return level >= 1 ? new Slot { Number = level } : (Slot?)null;
            if (!(source is BlueprintProgression progression)) return null;

            var owners = progression.m_Classes.Where(c => c.Class != null).Select(c => (c.Class, c.AdditionalLevel))
                .Concat(progression.m_Archetypes.Where(a => a.Archetype != null).Select(a => (a.Archetype.GetParentClass(), a.AdditionalLevel)));
            foreach (var (cls, additional) in owners) {
                int classLevel = level - additional;
                int seen = 0;
                for (int i = 0; i < order.Count; i++) {
                    if (order[i] == cls && ++seen == classLevel) return slots[i];
                }
            }
            return null;
        }

        static List<string> Chain(UnitEntityData unit, BlueprintFeature feature, Feature fact) {
            var chain = new List<string> { En(feature.m_DisplayName, feature.Name, feature.name) };
            if (fact?.Param != null) {
                var param = fact.Param;
                string name = null;
                if (param.WeaponCategory.HasValue) name = Vocabulary.EnumMemberNames(param.WeaponCategory.Value.ToString()).Last();
                else if (param.SpellSchool.HasValue) name = param.SpellSchool.Value.ToString();
                else if (param.StatType.HasValue) name = Vocabulary.EnumMemberNames(param.StatType.Value.ToString()).Skip(1).FirstOrDefault() ?? param.StatType.Value.ToString();
                else if (param.Blueprint is Kingmaker.Blueprints.Facts.BlueprintUnitFact paramFact) name = En(paramFact.m_DisplayName, paramFact.Name, paramFact.name);
                if (name != null) chain.Add(name);
            }
            if (feature is BlueprintFeatureSelection inner && unit.Progression.Selections.TryGetValue(inner, out var data)) {
                var chosen = data.SelectionsByLevel.Values.SelectMany(l => l).FirstOrDefault();
                if (chosen != null) chain.AddRange(Chain(unit, chosen, unit.Progression.Features.Enumerable.FirstOrDefault(f => f.Blueprint == chosen)));
            }
            return chain;
        }

        // The page title as the level-up shows it, in English (NameExport.AddPage does the same).
        static string PageTitle(BlueprintFeatureSelection selection) {
            var state = new FeatureSelectionState(null, default(FeatureSource), selection, 0, 0);
            try {
                string title = GameNames.EnglishPageTitle(state, MenuLabels.GetMenuLabel(state));
                if (!string.IsNullOrEmpty(title)) return title;
            } catch (Exception) {
                // the default label is the selection's own name, which is the fallback below
            }
            return En(selection.m_DisplayName, selection.Name, selection.name);
        }

        static void ReadSpells(UnitEntityData unit, List<BlueprintCharacterClass> order, CharacterRecord record) {
            var classOfLevel = order.Where(c => !c.IsMythic).Select(c => c.AssetGuid.ToString()).ToList();
            // Bloodline, curse and expanded-list spells come from AddKnownSpell, not from a level-up pick.
            var grantedSpells = new HashSet<Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility>(unit.Facts.List
                .SelectMany(f => f.Blueprint.ComponentsArray.OfType<AddKnownSpell>()).Select(c => c.Spell).Where(s => s != null));

            foreach (var book in unit.Descriptor.Spellbooks) {
                var bp = book.Blueprint;
                var cls = bp.CharacterClass;
                bool learns = !bp.AllSpellsKnown && !bp.IsMythic && ((bp.Spontaneous && bp.SpellsKnown != null) || (!bp.Spontaneous && bp.SpellsPerLevel > 0));
                if (!learns || cls == null) continue;

                var known = new Dictionary<int, List<string>>();
                for (int level = 1; level <= 9; level++) {
                    // A level-up offers only the book's own list: spells known from elsewhere (an item, a mythic ability) stay out.
                    var names = book.GetKnownSpells(level)
                        .Where(s => !s.CopiedFromScroll && !grantedSpells.Contains(s.Blueprint) && bp.SpellList != null && bp.SpellList.Contains(s.Blueprint))
                        .Select(s => En(s.Blueprint.m_DisplayName, s.Blueprint.Name, s.Blueprint.name)).Distinct().ToList();
                    if (names.Count > 0) known[level] = names;
                }
                if (known.Count == 0) continue;

                var table = bp.SpellsKnown;
                var perDay = bp.SpellsPerDay;
                var result = SpellDistributor.Distribute(classOfLevel, cls.AssetGuid.ToString(), known,
                    (c, s) => bp.Spontaneous && table != null ? Math.Max(0, table.GetCount(c, s) - table.GetCount(c - 1, s)) : 0,
                    c => bp.Spontaneous ? 0 : bp.SpellsPerLevel,
                    c => Enumerable.Range(1, 9).LastOrDefault(s => perDay != null && perDay.GetCount(c, s) > 0));
                foreach (var placed in result.ByLevel) record.Levels.FirstOrDefault(r => r.Level == placed.Key)?.Spells.AddRange(placed.Value);
                if (result.Dropped > 0) {
                    record.SpellsIncomplete = true;
                    Logging.Log.Engine.Info($"Export: {unit.CharacterName}: {result.Dropped} spell(s) of {bp.name} found no level");
                }
            }
        }

        static string RaceName(BlueprintRace race) {
            if (race == null) return null;
            string name = En(race.m_DisplayName, race.Name, race.name);
            return GameNames.Known.RaceExists(name) ? name : null;
        }

        /// <summary>English name, else the game's display name, else the blueprint name.</summary>
        static string En(LocalizedString text, string display, string blueprintName = null) {
            string english = GameNames.English(text);
            if (!string.IsNullOrEmpty(english)) return english;
            return !string.IsNullOrEmpty(display) ? display : blueprintName;
        }
    }
}
