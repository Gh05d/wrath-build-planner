using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;
using UnityEngine;
using WrathBuildPlanner.Engine;
using WrathBuildPlanner.Localization;

namespace WrathBuildPlanner.UI {
    /// <summary>The settings window's "Export" section: one button per recruited character. Drawn every frame by UMM.</summary>
    static class ExportSection {
        static readonly Dictionary<string, ExportOutcome> outcomes = new Dictionary<string, ExportOutcome>();
        static string lastError;
        // IMGUI lays out once per frame (Layout) and then handles the click: a label added during the click
        // would not match the layout. The export therefore runs at the next Layout pass.
        static UnitEntityData pending;

        public static void Draw() {
            if (pending != null && Event.current.type == EventType.Layout) {
                outcomes[pending.UniqueId] = CharacterExport.Export(pending);
                pending = null;
            }
            GUILayout.Space(12);
            GUILayout.Label("<b>" + "export.title".i18n() + "</b>");

            List<UnitEntityData> party, reserve;
            try {
                if (Game.Instance?.Player == null || LoadingProcess.Instance.IsLoadingInProcess) {
                    GUILayout.Label("export.none".i18n());
                    return;
                }
                party = CharacterExport.Party();
                reserve = CharacterExport.Reserve();
                if (party.Count == 0 && reserve.Count == 0) {
                    GUILayout.Label("export.none".i18n());
                    return;
                }
                Group("export.party", party);
                Group("export.reserve", reserve);
            } catch (Exception e) {
                // OnGUI runs every frame: log a failure once, not sixty times a second.
                if (e.Message != lastError) Logging.Log.UI.Error(e, "Export section failed");
                lastError = e.Message;
                GUILayout.Label("export.none".i18n());
            }
        }

        static void Group(string titleKey, List<UnitEntityData> units) {
            if (units.Count == 0) return;
            GUILayout.Label(titleKey.i18n());
            foreach (var unit in units) {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{unit.CharacterName} ({Strings.Format("export.level", unit.Progression.CharacterLevel)})", GUILayout.Width(320));
                if (GUILayout.Button("export.button".i18n(), GUILayout.Width(120))) pending = unit;
                GUILayout.EndHorizontal();
                if (outcomes.TryGetValue(unit.UniqueId, out var outcome)) Outcome(outcome);
            }
        }

        static void Outcome(ExportOutcome outcome) {
            var lines = new List<string>();
            if (outcome.Ok) {
                lines.Add(Strings.Format("export.saved", outcome.FileName));
                lines.AddRange(outcome.Gaps.Select(gap => gap.i18n()));
            } else {
                lines.Add(Strings.Format("export.failed", outcome.Error));
            }
            if (outcome.SkippedPicks > 0) lines.Add(Strings.Format("export.skipped", outcome.SkippedPicks));
            GUILayout.Label(string.Join("\n", lines.Select(l => "    " + l)));
        }
    }
}
