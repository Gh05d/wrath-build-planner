using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using MenuLabels = Kingmaker.UI.MVVM._VM.CharGen.Phases.FeatureSelector.FeatureSelectionExtensions;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Newtonsoft.Json;

namespace WrathBuildPlanner.Engine {
    /// <summary>
    /// Writes the names a player meets in character creation and level-up to JSON for the authoring page
    /// (site/data/names.json). Starts at BlueprintRoot.Progression — classes and their archetypes, mythic classes,
    /// races, the feat progression, the basic-feat and deity selections — and follows every selection inside
    /// their progressions. Runs in an English game only: display names are taken as the game shows them.
    /// </summary>
    class NameExport {
        class Feature {
            [JsonProperty("n")] public List<string> Names;
            [JsonProperty("sub", NullValueHandling = NullValueHandling.Ignore)] public List<string> Sub;
            [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)] public List<List<string>> Params;
        }

        class Page {
            [JsonProperty("n")] public List<string> Names;
            [JsonProperty("items", NullValueHandling = NullValueHandling.Ignore)] public List<string> Items;
            [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)] public List<List<string>> Params;
            [JsonProperty("nested", NullValueHandling = NullValueHandling.Ignore)] public bool? Nested;
            // Set on pages reached from a race's features (heritages): the page only exists for that race.
            [JsonProperty("race", NullValueHandling = NullValueHandling.Ignore)] public string Race;
        }

        class ClassEntry {
            [JsonProperty("n")] public List<string> Names;
            [JsonProperty("archetypes")] public List<List<string>> Archetypes;
            [JsonProperty("spells")] public List<string> Spells;
        }

        class Output {
            [JsonProperty("meta")] public Dictionary<string, string> Meta = new Dictionary<string, string>();
            [JsonProperty("races")] public List<List<string>> Races = new List<List<string>>();
            [JsonProperty("classes")] public List<ClassEntry> Classes = new List<ClassEntry>();
            [JsonProperty("mythicPaths")] public List<List<string>> MythicPaths = new List<List<string>>();
            [JsonProperty("features")] public SortedDictionary<string, Feature> Features = new SortedDictionary<string, Feature>();
            [JsonProperty("spells")] public SortedDictionary<string, List<string>> Spells = new SortedDictionary<string, List<string>>();
            [JsonProperty("pages")] public List<Page> Pages = new List<Page>();
        }

        readonly Output output = new Output();
        readonly HashSet<BlueprintFeatureBase> visited = new HashSet<BlueprintFeatureBase>();
        int paramFailures;

        public static string Write(string path) {
            if (LocalizationManager.CurrentLocale != Locale.enGB) return "NOT ENGLISH — switch the game to English before exporting";
            var export = new NameExport();
            export.Collect();
            string json = JsonConvert.SerializeObject(export.output, Formatting.None);
            File.WriteAllText(path, json);
            var o = export.output;
            return $"pages={o.Pages.Count} features={o.Features.Count} classes={o.Classes.Count} races={o.Races.Count} " +
                   $"mythicPaths={o.MythicPaths.Count} spells={o.Spells.Count} paramFailures={export.paramFailures} bytes={json.Length}";
        }

        static string Id(SimpleBlueprint blueprint) => blueprint.AssetGuid.ToString();

        // BlueprintUnitFact.Name runs the text template engine, which throws without a loaded game for names with
        // templates (Player.log 2026-10-06: NameTemplate.Generate NRE). Read the pack text directly instead and drop
        // encyclopedia link tags ({g|Encyclopedia:X}text{/g}) and rich-text tags.
        static readonly Regex Tags = new Regex(@"\{/?g(\|[^}]*)?\}|<[^>]+>");

        static string Raw(LocalizedString text) {
            if (text == null) return null;
            try {
                string value = text.LoadString(LocalizationManager.CurrentPack, LocalizationManager.CurrentLocale);
                return string.IsNullOrEmpty(value) ? null : Tags.Replace(value, "").Trim();
            } catch (Exception) {
                return null;
            }
        }

        static List<string> Identities(params string[] names) =>
            names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();

        void Collect() {
            var root = Game.Instance.BlueprintRoot.Progression;
            output.Meta["gameVersion"] = GameVersion.GetVersion();
            output.Meta["exported"] = DateTime.UtcNow.ToString("yyyy-MM-dd");
            output.Meta["modVersion"] = Main.ModEntry?.Info?.Version ?? "?";

            foreach (var race in root.CharacterRaces) {
                string raceName = Raw(race.m_DisplayName);
                output.Races.Add(Identities(raceName, race.name));
                foreach (var feature in race.Features) Walk(feature, false, raceName ?? race.name);
            }
            foreach (var cls in root.CharacterClasses) {
                var archetypes = cls.Archetypes.Where(a => a != null).ToList();
                var books = new[] { cls.Spellbook }.Concat(archetypes.Select(a => a.m_ReplaceSpellbook?.Get()));
                output.Classes.Add(new ClassEntry {
                    Names = Identities(Raw(cls.LocalizedName), cls.name),
                    Archetypes = archetypes.Select(a => Identities(Raw(a.LocalizedName), a.name)).ToList(),
                    Spells = books.SelectMany(Spells).Distinct().ToList(),
                });
                WalkEntries(cls.Progression?.LevelEntries);
                foreach (var archetype in archetypes) WalkEntries(archetype.AddFeatures);
            }
            var mythics = root.m_CharacterMythics.Select(r => r?.Get()).Where(c => c != null).ToList();
            foreach (var mythic in mythics) {
                output.MythicPaths.Add(Identities(Raw(mythic.LocalizedName), mythic.name));
                WalkEntries(mythic.Progression?.LevelEntries);
            }
            WalkEntries(root.m_MythicStartingClass?.Get()?.Progression?.LevelEntries);
            Walk(root.m_FeatsProgression?.Get());
            Walk(root.m_BasicFeatSelection?.Get());
            Walk(root.m_DeitySelection?.Get());
        }

        IEnumerable<string> Spells(BlueprintSpellbook book) {
            var list = book?.m_SpellList?.Get();
            if (list?.SpellsByLevel == null) yield break;
            foreach (var level in list.SpellsByLevel) {
                foreach (var spell in level.Spells) {
                    if (spell == null) continue;
                    output.Spells[Id(spell)] = Identities(Raw(spell.m_DisplayName), spell.name);
                    yield return Id(spell);
                }
            }
        }

        void WalkEntries(IEnumerable<LevelEntry> entries) {
            if (entries == null) return;
            foreach (var entry in entries)
                foreach (var feature in entry.Features) Walk(feature, false);
        }

        // nested: reached as an option of another selection, not granted by a progression directly.
        // A selection met both ways is recorded as a top-level page (progression entries are walked first per root).
        void Walk(BlueprintFeatureBase feature, bool nested = false, string race = null) {
            if (feature == null || !visited.Add(feature)) return;
            AddFeature(feature);
            if (feature is IFeatureSelection selection) AddPage(feature, selection, nested, race);
            if (feature is BlueprintFeatureSelection group)
                foreach (var item in group.AllFeatures) Walk(item, true, race);
            if (feature is BlueprintProgression progression) WalkEntries(progression.LevelEntries);
        }

        void AddFeature(BlueprintFeatureBase feature) {
            var entry = new Feature { Names = Identities(Raw(feature.m_DisplayName), feature.name) };
            if (feature is BlueprintFeatureSelection group)
                entry.Sub = group.AllFeatures.Where(f => f != null).Select(Id).Distinct().ToList();
            if (feature is BlueprintParametrizedFeature parametrized) entry.Params = Params(parametrized);
            output.Features[Id(feature)] = entry;
        }

        // Parameter values without a unit. Spell-based kinds may need one; those count as failures and stay empty.
        List<List<string>> Params(BlueprintParametrizedFeature feature) {
            try {
                return feature.GetFullSelectionItems().Where(i => i != null && !string.IsNullOrEmpty(i.Name))
                    .Select(i => Identities(i.Name)).ToList();
            } catch (Exception e) {
                paramFailures++;
                Logging.Log.Engine.Warn($"NameExport: no parameters for {feature.name}: {e.Message}");
                return new List<List<string>>();
            }
        }

        void AddPage(BlueprintFeatureBase feature, IFeatureSelection selection, bool nested, string race) {
            var state = new FeatureSelectionState(null, default(FeatureSource), selection, 0, 0);
            string display = Raw(feature.m_DisplayName);
            string title;
            try {
                title = GameNames.EnglishPageTitle(state, MenuLabels.GetMenuLabel(state));
            } catch (Exception) {
                title = display;   // the default label is the selection's own name, which can throw the same way
            }
            var page = new Page { Names = Identities(title, display, feature.name), Nested = nested ? true : (bool?)null, Race = race };
            if (feature is BlueprintFeatureSelection group) page.Items = group.AllFeatures.Where(f => f != null).Select(Id).Distinct().ToList();
            if (feature is BlueprintParametrizedFeature parametrized) page.Params = output.Features[Id(feature)].Params;
            output.Pages.Add(page);
        }
    }
}
