using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.UI.MVVM._PCView.CharGen;
using Kingmaker.UnitLogic.Class.LevelUp;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrathBuildPlanner.Core;
using WrathBuildPlanner.Engine;
using WrathBuildPlanner.Engine.Steps;
using WrathBuildPlanner.Localization;
using WrathBuildPlanner.Models;
using WrathBuildPlanner.Persistence;

namespace WrathBuildPlanner.UI {
    /// <summary>
    /// The bar inside the level-up / creation window: assigned build, Apply, Change, and a collapsible
    /// field (preview before applying, result list afterwards). Parented to the window's own view so it
    /// lives and dies with it and works in the main menu too.
    /// </summary>
    public class BuildBar {
        // Sizes are in the window's own units: its canvas is laid out for 1920x1200 and scaled down
        // (to two thirds on the Deck's 1280x800), so these are 1.5x what ends up on a Deck screen.
        const float BarWidth = 610f;
        const float BarHeight = 46f;
        const float BarLeft = 24f;
        const float BarBottom = 14f;
        const float DetailsWidth = 560f;
        const float DetailsHeight = 380f;
        const float DetailsRight = 30f;
        const float DetailsBottom = 132f;
        const float DetailsMinHeight = 70f;
        const float DetailsPadding = 12f;
        const float NameFont = 20f;
        const float ButtonFont = 19f;
        const float DetailsFont = 20f;

        GameObject root;
        GameObject details;
        TextMeshProUGUI nameLabel;
        TextMeshProUGUI detailsText;

        public ApplyReport LastReport { get; private set; }
        public event Action ChangeRequested;

        public static BuildBar Create(CharGenPCView view) {
            var bar = new BuildBar();
            var (root, rect) = UIHelpers.Create("WrathBuildPlannerBar", view.transform);
            bar.root = root;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(BarLeft, BarBottom);
            rect.sizeDelta = new Vector2(BarWidth, BarHeight);

            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = Theme.RowGap;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var (nameBox, _) = UIHelpers.Create("Name", root.transform);
            Widgets.InRow(nameBox, 170f, 1f);
            UIHelpers.AddBackground(nameBox, Theme.HintBacking);
            bar.nameLabel = UIHelpers.AddLabel(nameBox, "", NameFont, TextAlignmentOptions.MidlineLeft, Theme.HintText);
            bar.nameLabel.margin = new Vector4(8f, 0f, 8f, 0f);
            bar.nameLabel.enableWordWrapping = false;
            bar.nameLabel.overflowMode = TextOverflowModes.Ellipsis;

            Widgets.ActionButton(root.transform, "Apply", "bar.apply".i18n(), ButtonFont, bar.Apply, 180f);
            Widgets.ActionButton(root.transform, "Change", "bar.change".i18n(), ButtonFont, () => bar.ChangeRequested?.Invoke(), 130f);
            Widgets.ActionButton(root.transform, "Details", "bar.details".i18n(), ButtonFont, bar.ToggleDetails, 110f);

            bar.BuildDetails(view.transform);
            bar.Refresh();
            return bar;
        }

        void BuildDetails(Transform parent) {
            var (box, rect) = UIHelpers.Create("WrathBuildPlannerDetails", parent);
            details = box;
            // Bottom right, over the character model / progression chart: the left side holds the lists
            // the player needs to see while checking the picks.
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-DetailsRight, DetailsBottom);
            rect.sizeDelta = new Vector2(DetailsWidth, DetailsHeight);
            UIHelpers.AddBackground(box, Theme.HintBacking);

            var scroll = box.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;

            var (viewport, viewportRect) = UIHelpers.Create("Viewport", box.transform);
            viewportRect.FillParent();
            viewportRect.offsetMin = new Vector2(14f, DetailsPadding);
            viewportRect.offsetMax = new Vector2(-14f, -DetailsPadding);
            viewport.AddComponent<RectMask2D>();

            var (content, contentRect) = UIHelpers.Create("Content", viewport.transform);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // The text sits on the content object itself so the ContentSizeFitter can size it; the kit's
            // AddLabel creates a stretched child, which a fitter without a layout group cannot measure.
            var probe = UIHelpers.AddLabel(box, "", DetailsFont, TextAlignmentOptions.TopLeft, Theme.HintText);
            detailsText = content.AddComponent<TextMeshProUGUI>();
            detailsText.font = probe.font;
            detailsText.fontSize = probe.fontSize;
            detailsText.color = Theme.HintText;
            detailsText.alignment = TextAlignmentOptions.TopLeft;
            detailsText.enableWordWrapping = true;
            detailsText.raycastTarget = false;
            UnityEngine.Object.Destroy(probe.gameObject);

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            details.SetActive(false);
        }

        // The panel is as tall as its text, up to DetailsHeight; longer text scrolls.
        void SetDetails(string text) {
            detailsText.text = text;
            float width = DetailsWidth - 28f;
            float wanted = detailsText.GetPreferredValues(text, width, 0f).y + 2f * DetailsPadding;
            details.Rect().sizeDelta = new Vector2(DetailsWidth, Mathf.Clamp(wanted, DetailsMinHeight, DetailsHeight));
        }

        public void Destroy() {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (details != null) UnityEngine.Object.Destroy(details);
            root = null;
            details = null;
        }

        void ToggleDetails() {
            if (details == null) return;
            details.SetActive(!details.activeSelf);
            if (details.activeSelf) details.transform.SetAsLastSibling();
        }

        LibraryEntry Assigned(out string fileName) {
            fileName = AssignmentStore.Get(WindowTracker.Controller?.Unit);
            return Main.Library.Find(fileName);
        }

        /// <summary>Name label and preview text for the level the window is on.</summary>
        public void Refresh() {
            if (root == null) return;
            var entry = Assigned(out string fileName);
            if (fileName == null) {
                nameLabel.text = "bar.no_build".i18n();
                SetDetails("");
                return;
            }
            if (entry == null || !entry.Ok) {
                nameLabel.text = Strings.Format("bar.missing", fileName);
                SetDetails(entry == null ? "" : string.Join("\n", entry.Issues.Select(i => "! " + i.Message)));
                return;
            }
            nameLabel.text = entry.Build.Name;
            if (LastReport == null) SetDetails(Preview(entry.Build));
        }

        string Preview(BuildFile build) {
            var state = WindowTracker.Controller?.State;
            if (state == null) return "";
            if (state.Mode == LevelUpState.CharBuildMode.Mythic) {
                var mythic = LevelPlanner.MythicRowFor(build, state.NextMythicLevel);
                if (mythic == null) return Strings.Format("bar.no_row_mythic", state.NextMythicLevel);
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(mythic.Path)) parts.Add(mythic.Path);
                parts.AddRange(mythic.Picks.Select(p => p.Label));
                return Strings.Format("bar.preview", string.Join(", ", parts));
            }
            var row = LevelPlanner.RowFor(build, state.NextCharacterLevel);
            if (row == null) return Strings.Format("bar.no_row", state.NextCharacterLevel);
            var items = new List<string> { row.Class };
            if (!string.IsNullOrWhiteSpace(row.Archetype)) items.Add(row.Archetype);
            if (!string.IsNullOrWhiteSpace(row.AbilityPoint)) items.Add("+1 " + row.AbilityPoint);
            items.AddRange(row.Picks.Select(p => p.Label));
            items.AddRange(row.Spells);
            return Strings.Format("bar.preview", string.Join(", ", items));
        }

        public void Apply() {
            var entry = Assigned(out _);
            if (entry == null || !entry.Ok) {
                Refresh();
                if (details != null) details.SetActive(true);
                return;
            }
            LastReport = LevelApplier.Apply(WindowTracker.Controller, entry.Build);
            SetDetails(Render(LastReport));
            if (details != null) {
                details.SetActive(true);
                details.transform.SetAsLastSibling();
            }
            PlannerController.Instance?.JumpToFirstOpenPage();
        }

        public static string Render(ApplyReport report) {
            if (report.NoWindow) return "";
            if (report.NoRow) return Strings.Format(report.Mythic ? "bar.no_row_mythic" : "bar.no_row", report.Level);
            var lines = new List<string>();
            if (report.History != null && report.History.Comparable && !report.History.Matches)
                lines.Add(Strings.Format("bar.history", report.History.Expected, report.History.Actual));
            lines.Add(Strings.Format("bar.result", report.AppliedCount, report.OpenCount));
            foreach (var step in report.Steps.OrderBy(s => s.Status == StepStatus.Open ? 0 : 1)) {
                if (step.Status != StepStatus.Open) {
                    lines.Add($"+ {step.Label} — {(step.Status == StepStatus.Applied ? "status.applied" : "status.already").i18n()}");
                    continue;
                }
                string line = $"! {step.Label} — {("reason." + step.Reason).i18n()}";
                if (!string.IsNullOrEmpty(step.Detail)) line += $" ({step.Detail})";
                lines.Add(line);
                if (step.Suggestions.Count > 0) lines.Add("    " + Strings.Format("reason.similar", string.Join(", ", step.Suggestions)));
            }
            return string.Join("\n", lines);
        }
    }
}
