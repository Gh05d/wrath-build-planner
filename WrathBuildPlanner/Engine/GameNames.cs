using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Kingmaker.UI;
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
            if (item.Param != null || item.Feature == null) return Make(item, display);
            return Make(item, string.IsNullOrEmpty(display) ? item.Feature.name : display,
                English(item.Feature.m_DisplayName), item.Feature.name, item.Feature.AssetGuid.ToString());
        }

        /// <summary>
        /// A selection, named as its page is titled. The title is FeatureSelectionExtensions.GetMenuLabel
        /// (what CharGenFeatureSelectorPhaseVM passes to SetPhaseName) and can differ from the blueprint's own
        /// name: the wizard's school page is titled "School", its blueprint is named "Specialist School".
        /// </summary>
        public static NameCandidate OfSelection(FeatureSelectionState selection) {
            string title = null;
            try {
                title = Kingmaker.UI.MVVM._VM.CharGen.Phases.FeatureSelector.FeatureSelectionExtensions.GetMenuLabel(selection);
            } catch (Exception e) {
                Logging.Log.Engine.Error(e, "page title lookup failed");
            }
            var blueprint = selection.Selection as BlueprintFeature;
            string group = selection.Selection.GetGroup().ToString();
            if (blueprint == null) return Make(selection, title, group);
            return Make(selection, title, blueprint.Name, English(blueprint.m_DisplayName), blueprint.name, group);
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
