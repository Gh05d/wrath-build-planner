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
            };
            try {
                record.Race = RaceName(progression.Race);
            } catch (Exception e) {
                Logging.Log.Engine.Warn($"Export: {unit.CharacterName}: race name unreadable: {e.Message}");
            }

            // A preset-levelled unit (hired pregen, DLC companion) has fake levels the history does not hold, and levels
            // taken after hiring are appended behind them: only the first entry is surely its own level 1.
            if (order.Count(c => !c.IsMythic) < progression.CharacterLevel) {
                int first = order.FindIndex(c => !c.IsMythic);
                order = order.Where((c, i) => c.IsMythic || i == first).ToList();
            }

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
                    if (cls != firstMythic && paths.Contains(cls) && seenClasses.Add(cls)) rank.Path = Safe(() => En(cls.LocalizedName, cls.Name), cls.name);
                    record.Mythic.Add(rank);
                    continue;
                }
                if (slots[i].Number > BuildExporter.MaxLevel) {
                    record.LegendLevelsLeftOut = true;
                    continue;
                }
                var row = new RecordLevel { Level = slots[i].Number, Class = Safe(() => En(cls.LocalizedName, cls.Name), cls.name) };
                if (seenClasses.Add(cls)) {
                    var archetype = progression.Classes.Where(c => c.CharacterClass == cls).SelectMany(c => c.Archetypes).FirstOrDefault();
                    if (archetype != null) row.Archetype = Safe(() => En(archetype.LocalizedName, archetype.Name), archetype.name);
                }
                record.Levels.Add(row);
            }

            ReadPicks(unit, order, slots, record);
            foreach (var skill in Vocabulary.SkillList) {
                try {
                    if (Enum.TryParse(skill.Key, out StatType stat)) record.SkillRanks[skill.Key] = unit.Stats.GetStat(stat).BaseValue;
                } catch (Exception e) {
                    Logging.Log.Engine.Warn($"Export: {unit.CharacterName}: skill {skill.Key} unreadable: {e.Message}");
                }
            }
            ReadSpells(unit, order, record);
            return record;
        }

        static void ReadPicks(UnitEntityData unit, List<BlueprintCharacterClass> order, List<Slot> slots, CharacterRecord record) {
            var progression = unit.Progression;
            var feats = Game.Instance.BlueprintRoot.Progression.FeatsProgression;
            // A selection whose item is itself a selection (Background > Scholar > Arcane) is one chain, read from the
            // outer pick — but only at the level it was picked as an item: the same selection can also be a page of its own
            // (Ranger combat style: a class page, and a Slayer talent item).
            var nested = new HashSet<(BlueprintFeatureSelection, int)>();
            foreach (var data in progression.Selections.Values) {
                foreach (var byLevel in data.SelectionsByLevel) {
                    foreach (var inner in byLevel.Value.OfType<BlueprintFeatureSelection>()) {
                        if (progression.Selections.ContainsKey(inner)) nested.Add((inner, byLevel.Key));
                    }
                }
            }
            var used = new HashSet<(Feature, int)>();
            var usedInner = new HashSet<(BlueprintFeatureSelection, int, int)>();

            foreach (var entry in progression.Selections) {
                var selection = entry.Key;
                foreach (var byLevel in entry.Value.SelectionsByLevel) {
                    if (nested.Contains((selection, byLevel.Key))) continue;
                    foreach (var feature in byLevel.Value) {
                        try {
                            // A feature with one rank keeps only its last source: fall back to the selection's own source and key.
                            var source = FindSource(unit, feature, byLevel.Key, used)
                                ?? new Source { Blueprint = entry.Value.Source.Blueprint, Level = byLevel.Key,
                                                Fact = progression.Features.Enumerable.FirstOrDefault(f => f.Blueprint == feature) };
                            var slot = SlotOf(source.Blueprint, source.Level, feats, order, slots);
                            if (slot == null) continue;   // not a level-up page: dialog, story event, another mod's script
                            var pick = new PickEntry { In = PageTitle(selection), Pick = Chain(unit, feature, source.Fact, byLevel.Key, usedInner) };
                            var target = slot.Value.Mythic
                                ? record.Mythic.FirstOrDefault(r => r.Rank == slot.Value.Number)?.Picks
                                : record.Levels.FirstOrDefault(r => r.Level == slot.Value.Number)?.Picks;
                            if (target != null) target.Add(pick);
                            else Logging.Log.Engine.Info($"Export: {unit.CharacterName}: pick '{feature.name}' has no row ({(slot.Value.Mythic ? "rank" : "level")} {slot.Value.Number})");
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
            // Race features sit at 0; feat selections whose first source was the race (human bonus feat) keep character levels.
            if (source is BlueprintRace) return new Slot { Number = Math.Max(1, level) };
            if (source == feats) return level >= 1 ? new Slot { Number = level } : (Slot?)null;
            if (!(source is BlueprintProgression progression)) return null;

            // BlueprintProgression.CalcLevel: the sum over every class and archetype it lists (class level + AdditionalLevel),
            // so a prestige class continuing a base progression (Dragon Disciple after Sorcerer) counts on.
            var owners = progression.m_Classes.Where(c => c.Class != null).Select(c => (Class: c.Class, c.AdditionalLevel))
                .Concat(progression.m_Archetypes.Where(a => a.Archetype != null).Select(a => (Class: a.Archetype.GetParentClass(), a.AdditionalLevel)))
                .GroupBy(o => o.Class).Select(g => g.First()).ToList();
            if (owners.Count == 0) return null;
            var ownerClasses = new HashSet<BlueprintCharacterClass>(owners.Select(o => o.Class));
            int additional = owners.Where(o => order.Contains(o.Class)).Sum(o => o.AdditionalLevel);
            int running = additional;
            for (int i = 0; i < order.Count; i++) {
                if (!ownerClasses.Contains(order[i])) continue;
                if (++running == level) return slots[i];
            }
            return null;
        }

        static List<string> Chain(UnitEntityData unit, BlueprintFeature feature, Feature fact, int key,
            HashSet<(BlueprintFeatureSelection, int, int)> usedInner) {
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
            // The inner selection's item at the outer pick's level; a second pick of the same selection takes the next one.
            if (feature is BlueprintFeatureSelection inner && unit.Progression.Selections.TryGetValue(inner, out var data)
                && data.SelectionsByLevel.TryGetValue(key, out var items)) {
                for (int i = 0; i < items.Count; i++) {
                    if (!usedInner.Add((inner, key, i))) continue;
                    var chosen = items[i];
                    chain.AddRange(Chain(unit, chosen, unit.Progression.Features.Enumerable.FirstOrDefault(f => f.Blueprint == chosen), key, usedInner));
                    break;
                }
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
            // Bloodline, curse and expanded-list spells come from AddKnownSpell, not from a level-up pick.
            HashSet<Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility> grantedSpells;
            try {
                grantedSpells = new HashSet<Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility>(unit.Facts.List
                    .SelectMany(f => f.Blueprint.ComponentsArray.OfType<AddKnownSpell>()).Select(c => c.Spell).Where(s => s != null));
            } catch (Exception e) {
                record.SpellsIncomplete = true;
                Logging.Log.Engine.Warn($"Export: {unit.CharacterName}: spells left out, granted spells unreadable: {e.Message}");
                return;
            }

            foreach (var book in unit.Descriptor.Spellbooks) {
                try {
                    ReadBook(unit, order, record, book, grantedSpells);
                } catch (Exception e) {
                    record.SpellsIncomplete = true;
                    Logging.Log.Engine.Warn($"Export: {unit.CharacterName}: spells of {book?.Blueprint?.name} left out: {e.Message}");
                }
            }
        }

        static void ReadBook(UnitEntityData unit, List<BlueprintCharacterClass> order, CharacterRecord record, Spellbook book,
            HashSet<Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility> grantedSpells) {
            var bp = book.Blueprint;
            var cls = bp.CharacterClass;
            bool learns = !bp.AllSpellsKnown && !bp.IsMythic && ((bp.Spontaneous && bp.SpellsKnown != null) || (!bp.Spontaneous && bp.SpellsPerLevel > 0));
            if (!learns || cls == null) return;

            var known = new Dictionary<int, List<string>>();
            for (int level = 1; level <= 9; level++) {
                // A level-up offers only the book's own list: spells known from elsewhere (an item, a mythic ability) stay out.
                var names = book.GetKnownSpells(level)
                    .Where(s => !s.CopiedFromScroll && !grantedSpells.Contains(s.Blueprint) && bp.SpellList != null && bp.SpellList.Contains(s.Blueprint))
                    .Select(s => En(s.Blueprint.m_DisplayName, s.Blueprint.Name, s.Blueprint.name)).Distinct().ToList();
                if (names.Count > 0) known[level] = names;
            }
            if (known.Count == 0) return;

            // Levels that advance this book: its class, and prestige classes whose ClassData carries it (Eldritch Knight, Loremaster).
            var advancing = new HashSet<BlueprintCharacterClass>(unit.Progression.Classes
                .Where(c => c.CharacterClass == cls || c.Spellbook == bp).Select(c => c.CharacterClass)) { cls };
            const string Book = "book";
            var classOfLevel = order.Where(c => !c.IsMythic).Select(c => advancing.Contains(c) ? Book : c.AssetGuid.ToString()).ToList();

            // A prepared caster's first level adds 3 + the casting ability's modifier (ApplySpellbook: SetExtraSpells with
            // ExtraByStat). The current score may be higher than at level 1; a pick too many only shows as "no free slot".
            int firstLevelPicks = 3 + Math.Max(0, unit.Stats.GetStat(bp.CastingAttribute).PermanentValue / 2 - 5);
            var table = bp.SpellsKnown;
            var perDay = bp.SpellsPerDay;
            var result = SpellDistributor.Distribute(classOfLevel, Book, known,
                (c, s) => bp.Spontaneous && table != null ? Math.Max(0, table.GetCount(c, s) - table.GetCount(c - 1, s)) : 0,
                c => bp.Spontaneous ? 0 : c == 1 ? firstLevelPicks : bp.SpellsPerLevel,
                c => Enumerable.Range(1, 9).LastOrDefault(s => perDay != null && perDay.GetCount(c, s) > 0));
            foreach (var placed in result.ByLevel) record.Levels.FirstOrDefault(r => r.Level == placed.Key)?.Spells.AddRange(placed.Value);
            if (result.Dropped > 0) {
                record.SpellsIncomplete = true;
                Logging.Log.Engine.Info($"Export: {unit.CharacterName}: {result.Dropped} spell(s) of {bp.name} found no level");
            }
        }

        static string RaceName(BlueprintRace race) {
            if (race == null) return null;
            string name = En(race.m_DisplayName, race.Name, race.name);
            return GameNames.Known.RaceExists(name) ? name : null;
        }

        static string Safe(Func<string> read, string fallback) {
            try {
                return read() ?? fallback;
            } catch (Exception) {
                return fallback;
            }
        }

        /// <summary>English name, else the game's display name, else the blueprint name.</summary>
        static string En(LocalizedString text, string display, string blueprintName = null) {
            string english = GameNames.English(text);
            if (!string.IsNullOrEmpty(english)) return english;
            return !string.IsNullOrEmpty(display) ? display : blueprintName;
        }
    }
}
