using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Newtonsoft.Json;
using WrathBuildPlanner.Persistence;

namespace WrathBuildPlanner.Localization {
    public static class Strings {
        static readonly Dictionary<Locale, Dictionary<string, string>> Packs = new Dictionary<Locale, Dictionary<string, string>>();
        static readonly Dictionary<string, Locale> Codes = new Dictionary<string, Locale> {
            { "en", Locale.enGB }, { "de", Locale.deDE }, { "fr", Locale.frFR }, { "ru", Locale.ruRU }, { "zh", Locale.zhCN },
        };
        static bool initialised;

        public static readonly string[] LanguageChoices = { "auto", "en", "de", "fr", "ru", "zh" };

        public static void Initialise() {
            if (initialised) return;
            initialised = true;
            Add(Locale.enGB, "en_GB.json");
            Add(Locale.deDE, "de_DE.json");
            Add(Locale.frFR, "fr_FR.json");
            Add(Locale.ruRU, "ru_RU.json");
            Add(Locale.zhCN, "zh_CN.json");
            Core.Messages.Translate = key => Packs.TryGetValue(Current, out var pack) && pack.TryGetValue("msg." + key, out string value) ? value : null;
        }

        // The settings override wins; "auto" follows the game. LocalizationManager.CurrentLocale throws
        // before the game's settings have loaded, so fall back to English there.
        public static Locale Current {
            get {
                if (Codes.TryGetValue(ModSettings.Current.Language ?? "auto", out var chosen)) return chosen;
                try {
                    return LocalizationManager.CurrentLocale;
                } catch (NullReferenceException) {
                    return Locale.enGB;
                }
            }
        }

        public static string i18n(this string key) {
            if (Packs.TryGetValue(Current, out var pack) && pack.TryGetValue(key, out string value)) return value;
            if (Packs.TryGetValue(Locale.enGB, out var english) && english.TryGetValue(key, out string fallback)) return fallback;
            return key;
        }

        public static string Format(string key, params object[] args) {
            string template = key.i18n();
            try {
                return string.Format(template, args);
            } catch (FormatException) {
                return template;
            }
        }

        static void Add(Locale locale, string fileName) {
            try {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WrathBuildPlanner.Localization." + fileName)) {
                    if (stream == null) {
                        Logging.Log.Engine.Warn("localization resource missing: " + fileName);
                        return;
                    }
                    using (var reader = new StreamReader(stream)) {
                        Packs[locale] = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd()) ?? new Dictionary<string, string>();
                    }
                }
            } catch (JsonException e) {
                Logging.Log.Engine.Error(e, "malformed localization file " + fileName);
            }
        }
    }
}
