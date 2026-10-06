using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Kingmaker.UI;
using MenuLabels = Kingmaker.UI.MVVM._VM.CharGen.Phases.FeatureSelector.FeatureSelectionExtensions;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using WrathBuildPlanner.Core;

namespace WrathBuildPlanner.Engine {
    /// <summary>Turns game objects into NameCandidates: display name, English name, internal name, GUID.</summary>
    public static class GameNames {
        static LocalizationPack englishPack;
        static bool englishTried;

        /// <summary>
        /// English text of a localized string while the game runs in another language.
        /// LocalizationManager.LoadPack(Locale) reads the pack from StreamingAssets without switching the game's language.
        /// Returns null when the game already runs in English or the pack cannot be loaded.
        /// </summary>
        public static string English(LocalizedString text) {
            if (text == null) return null;
            try {
                if (LocalizationManager.CurrentLocale == Locale.enGB) return null;
                if (!englishTried) {
                    englishTried = true;
                    englishPack = LocalizationManager.LoadPack(Locale.enGB);
                    Logging.Log.Engine.Info($"English name pack loaded: {englishPack != null}");
                }
                if (englishPack == null) return null;
                string value = text.LoadString(englishPack, Locale.enGB);
                return string.IsNullOrEmpty(value) ? null : value;
            } catch (Exception e) {
                if (englishPack != null) Logging.Log.Engine.Error(e, "English name lookup failed; English names are off for this session");
                englishPack = null;
                return null;
            }
        }

        static NameCandidate Make(object tag, string display, params string[] more) {
            var names = new List<string>();
            if (!string.IsNullOrEmpty(display)) names.Add(display);
            names.AddRange(more.Where(n => !string.IsNullOrEmpty(n)));
            return new NameCandidate { Tag = tag, Display = names.Count > 0 ? names[0] : "?", Names = names.ToArray() };
        }

        public static NameCandidate Of(BlueprintCharacterClass cls) =>
            Make(cls, cls.Name, English(cls.LocalizedName), cls.name, cls.AssetGuid.ToString());

        public static NameCandidate Of(BlueprintArchetype archetype) =>
            Make(archetype, archetype.Name, English(archetype.LocalizedName), archetype.name, archetype.AssetGuid.ToString());

        public static NameCandidate Of(BlueprintRace race) =>
            Make(race, race.Name, English(race.m_DisplayName), race.name, race.AssetGuid.ToString());

        public static NameCandidate OfSpell(BlueprintAbility spell) =>
            Make(spell, spell.Name, English(spell.m_DisplayName), spell.name, spell.AssetGuid.ToString());

        /// <summary>
        /// A selectable item. Parametrized items (Weapon Focus -> Greatsword) all share the parent feature's blueprint,
        /// so for them only the display name is an identity.
        /// </summary>
        public static NameCandidate OfItem(IFeatureSelectionItem item) {
            string display = (item as IUIDataProvider)?.Name;
            if (item.Param != null) return Make(item, display, ParamNames(item.Param));
            if (item.Feature == null) return Make(item, display);
            return Make(item, string.IsNullOrEmpty(display) ? item.Feature.name : display,
                English(item.Feature.m_DisplayName), item.Feature.name, item.Feature.AssetGuid.ToString());
        }

        /// <summary>
        /// English identities of a parameter value (the weapon of Weapon Focus, a school, a skill, a spell): the display
        /// name is in the game's language, so "Greatsword" failed in a German game (Deck, 2026-10-06). The enum names
        /// are English ("Greatsword", "Evocation"; see Vocabulary.EnumMemberNames); a blueprint brings its English name.
        /// </summary>
        static string[] ParamNames(FeatureParam param) {
            var names = new List<string>();
            if (param.WeaponCategory.HasValue) names.AddRange(Vocabulary.EnumMemberNames(param.WeaponCategory.Value.ToString()));
            if (param.SpellSchool.HasValue) names.AddRange(Vocabulary.EnumMemberNames(param.SpellSchool.Value.ToString()));
            if (param.StatType.HasValue) names.AddRange(Vocabulary.EnumMemberNames(param.StatType.Value.ToString()));
            if (param.Blueprint is BlueprintUnitFact fact) names.Add(English(fact.m_DisplayName));
            if (param.Blueprint != null) names.Add(param.Blueprint.name);
            return names.ToArray();
        }

        /// <summary>
        /// A selection, named as its page is titled. The title is FeatureSelectionExtensions.GetMenuLabel
        /// (what CharGenFeatureSelectorPhaseVM passes to SetPhaseName) and can differ from the blueprint's own
        /// name: the wizard's school page is titled "School", its blueprint is named "Specialist School".
        /// </summary>
        public static NameCandidate OfSelection(FeatureSelectionState selection) {
            string title = null;
            try {
                title = MenuLabels.GetMenuLabel(selection);
            } catch (Exception e) {
                Logging.Log.Engine.Error(e, "page title lookup failed");
            }
            var blueprint = selection.Selection as BlueprintFeature;
            string english = EnglishPageTitle(selection, title);
            if (blueprint == null) return Make(selection, title, english);
            return Make(selection, title, english, blueprint.Name, English(blueprint.m_DisplayName), blueprint.name);
        }

        /// <summary>
        /// The English text of a page title. GetMenuLabel returns one of the UICharGen strings or the selection's
        /// own name, both in the game's language (IL: FeatureSelectionExtensions.GetMenuLabel / GetFeatureSelectionName);
        /// find which one it was and read that string from the English pack. In an English game: the title itself.
        /// </summary>
        public static string EnglishPageTitle(FeatureSelectionState state, string title) {
            if (string.IsNullOrEmpty(title)) return title;
            try {
                var c = MenuLabels.Texts?.CharGen;
                if (c != null) {
                    foreach (var text in new[] { c.ChannelEnergy, c.Deity, c.Heritage, c.Bond, c.Animal, c.Discovery,
                                                 c.Bloodline, c.Domain, c.School, c.Spellbook, c.Blast, c.ChooseAbilities }) {
                        if (text != null && (string)text == title) return English(text) ?? title;
                    }
                }
                if (state?.Selection is BlueprintUnitFact fact && fact.Name == title) return English(fact.m_DisplayName) ?? title;
            } catch (Exception e) {
                Logging.Log.Engine.Error(e, "English page title lookup failed");
            }
            return title;
        }

        public static List<NameCandidate> Classes() =>
            Game.Instance.BlueprintRoot.Progression.CharacterClasses.Select(Of).ToList();

        public static List<NameCandidate> Races() =>
            Game.Instance.BlueprintRoot.Progression.CharacterRaces.Select(Of).ToList();

        class KnownNames : IKnownNames {
            public bool ClassExists(string name) => NameMatcher.Match(name, Classes()).Kind == MatchKind.Unique;
            public bool RaceExists(string name) => NameMatcher.Match(name, Races()).Kind == MatchKind.Unique;
        }

        public static readonly IKnownNames Known = new KnownNames();
    }
}
