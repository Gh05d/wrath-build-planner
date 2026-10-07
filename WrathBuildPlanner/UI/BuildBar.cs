using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kingmaker.UI.MVVM._PCView.CharGen;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Pregen;
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
    /// field (preview before applying, result list afterwards, plus a note on the pages the build leaves
    /// to the player). Parented to the window's own view so it lives and dies with it and works in the
    /// main menu too.
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
        const float DetailsMinHeight = 80f;
        const float DetailsPadding = 20f;
        const float DetailsPaddingX = 26f;
        const float NameFont = 20f;
        const float ButtonFont = 19f;
        const float DetailsFont = 20f;

        GameObject root;
        GameObject details;
        TextMeshProUGUI nameLabel;
        TextMeshProUGUI changeLabel;
        RectTransform detailsChevron;
        TextMeshProUGUI detailsText;
        string detailsBody = "";
        bool hasBuild;
        bool onPlayersPage;

        public ApplyReport LastReport { get; private set; }
        public event Action ChangeRequested;

        public static BuildBar Create(CharGenPCView view) {
            var bar = new BuildBar();
            var (root, rect) = UIHelpers.Create(BarName, view.transform);
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
            // The name field opens the Builds window as well: it is where players look for the choice.
            var nameButton = nameBox.AddComponent<Button>();
            nameButton.targetGraphic = Framed(nameBox, ThemeProvider.HintAnnotation, Theme.HintBacking, 4f);
            Widgets.ApplyColorTint(nameButton);
            nameButton.onClick.AddListener(() => bar.ChangeRequested?.Invoke());
            bar.nameLabel = UIHelpers.AddLabel(nameBox, "", NameFont, TextAlignmentOptions.Midline, Theme.HintText);
            bar.nameLabel.margin = new Vector4(14f, 0f, 14f, 0f);
            bar.nameLabel.enableWordWrapping = false;
            bar.nameLabel.overflowMode = TextOverflowModes.Ellipsis;

            Widgets.ActionButton(root.transform, "Apply", "bar.apply".i18n(), ButtonFont, bar.Apply, 180f);
            // Sized for the longer of its two labels; Refresh picks the one that fits the state.
            string set = "bar.set".i18n(), change = "bar.change".i18n();
            var changeButton = Widgets.ActionButton(root.transform, "Change", set.Length >= change.Length ? set : change,
                ButtonFont, () => bar.ChangeRequested?.Invoke(), 130f);
            bar.changeLabel = changeButton.GetComponentInChildren<TextMeshProUGUI>();
            bar.detailsChevron = DetailsToggle(root.transform, bar.ToggleDetails);

            bar.BuildDetails(view.transform);
            bar.Refresh();
            return bar;
        }

        void BuildDetails(Transform parent) {
            var (box, rect) = UIHelpers.Create(DetailsName, parent);
            details = box;
            // Bottom right, over the character model / progression chart: the left side holds the lists
            // the player needs to see while checking the picks.
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-DetailsRight, DetailsBottom);
            rect.sizeDelta = new Vector2(DetailsWidth, DetailsHeight);
            // The game's own tooltips are torn parchment with ink: the panel matches them.
            Framed(box, ThemeProvider.PopupPaper, Theme.PaperFallback, 3f);

            var scroll = box.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;

            var (viewport, viewportRect) = UIHelpers.Create("Viewport", box.transform);
            viewportRect.FillParent();
            viewportRect.offsetMin = new Vector2(DetailsPaddingX, DetailsPadding);
            viewportRect.offsetMax = new Vector2(-DetailsPaddingX, -DetailsPadding);
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
            var probe = UIHelpers.AddLabel(box, "", DetailsFont, TextAlignmentOptions.TopLeft, Theme.Ink);
            detailsText = content.AddComponent<TextMeshProUGUI>();
            detailsText.font = probe.font;
            detailsText.fontSize = probe.fontSize;
            detailsText.color = Theme.Ink;
            detailsText.alignment = TextAlignmentOptions.TopLeft;
            detailsText.enableWordWrapping = true;
            detailsText.raycastTarget = false;
            UnityEngine.Object.Destroy(probe.gameObject);

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            ShowDetails(false);
        }

        // A square button with a chevron instead of a "Details" label: the name field needs the width.
        // The chevron points up while the panel is closed and down while it is open.
        static RectTransform DetailsToggle(Transform parent, UnityEngine.Events.UnityAction onClick) {
            var (button, _) = UIHelpers.Create("Details", parent);
            Widgets.InRow(button, BarHeight, 0f);
            if (ThemeProvider.ActionButtonNormal != null) {
                ThemeProvider.ApplyActionButton(button);
            } else {
                var fallback = button.AddComponent<Button>();
                fallback.targetGraphic = UIHelpers.AddBackground(button, Theme.BandFallbackMauve);
                Widgets.ApplyColorTint(fallback);
            }
            button.GetComponent<Button>().onClick.AddListener(onClick);
            var icon = Widgets.IconImage(button.transform, "Chevron", Icon.Chevron, BarHeight * 0.45f, Theme.BandText);
            var rect = icon.Rect();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            icon.GetComponent<LayoutElement>().ignoreLayout = true;
            return rect;
        }

        void ShowDetails(bool open) {
            if (details == null) return;
            details.SetActive(open);
            if (open) details.transform.SetAsLastSibling();
            if (detailsChevron != null) detailsChevron.localRotation = Quaternion.Euler(0f, 0f, open ? 0f : 180f);
        }

        // Sliced theme sprite; a higher multiplier draws its border thinner. Flat colour if the sprite is missing.
        static Image Framed(GameObject obj, Sprite sprite, Color fallback, float multiplier) {
            var image = UIHelpers.AddBackground(obj, sprite == null ? fallback : Color.white);
            if (sprite == null) return image;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
            return image;
        }

        void SetDetails(string body) {
            detailsBody = body;
            RenderDetails();
        }

        // The panel is as tall as its text, up to DetailsHeight; longer text scrolls.
        void RenderDetails() {
            var lines = detailsBody.Length == 0 ? new List<string>() : detailsBody.Split('\n').Select(Ink).ToList();
            if (hasBuild && onPlayersPage) {
                if (lines.Count > 0) lines.Insert(0, "");
                lines.Insert(0, $"<i><color=#{Hex(Theme.InkMuted)}>{Plain("bar.players_page".i18n())}</color></i>");
            }
            string text = string.Join("\n", lines);
            detailsText.text = text;
            float width = DetailsWidth - 2f * DetailsPaddingX;
            float wanted = detailsText.GetPreferredValues(text, width, 0f).y + 2f * DetailsPadding;
            details.Rect().sizeDelta = new Vector2(DetailsWidth, Mathf.Clamp(wanted, DetailsMinHeight, DetailsHeight));
        }

        // Open items in red, done items in muted ink, everything else (summary, preview) in plain ink.
        static string Ink(string line) {
            if (line.StartsWith("! ")) return $"<color=#{Hex(Theme.StatusError)}>{Plain(line)}</color>";
            if (line.StartsWith("+ ")) return $"<color=#{Hex(Theme.InkMuted)}>{Plain(line)}</color>";
            if (line.StartsWith("    ")) return $"<color=#{Hex(Theme.StatusWarn)}>{Plain(line)}</color>";
            return Plain(line);
        }

        // Build and feature names come from files: never let them be read as markup.
        static string Plain(string text) => text.Length == 0 ? text : $"<noparse>{text}</noparse>";

        static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);

        const string BarName = "WrathBuildPlannerBar";
        const string DetailsName = "WrathBuildPlannerDetails";

        /// <summary>Removes bar objects a failed Create left behind under the view.</summary>
        public static void DestroyLeftovers(CharGenPCView view) {
            foreach (string name in new[] { BarName, DetailsName }) {
                var leftover = view.transform.Find(name);
                if (leftover != null) UnityEngine.Object.Destroy(leftover.gameObject);
            }
        }

        public void Destroy() {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (details != null) UnityEngine.Object.Destroy(details);
            root = null;
            details = null;
        }

        /// <summary>
        /// Called every frame: notes whether the window shows a page the build never fills
        /// (portrait, appearance, voice, name) and adds or drops the note in the details.
        /// </summary>
        public void SyncPage(bool playersPage) {
            if (root == null || playersPage == onPlayersPage) return;
            onPlayersPage = playersPage;
            RenderDetails();
        }

        public string DescribeForTests() =>
            $"name={nameLabel.text} | change={changeLabel.text} | details({details.activeSelf})={Regex.Replace(detailsText.text, "<[^>]+>", "")}";

        void ToggleDetails() {
            if (details == null) return;
            ShowDetails(!details.activeSelf);
        }

        string shownFile;

        LibraryEntry Assigned(out string fileName) {
            fileName = AssignmentStore.Get(WindowTracker.Controller?.Unit);
            // Another build was assigned: the old result no longer describes anything.
            if (fileName != shownFile) {
                shownFile = fileName;
                LastReport = null;
            }
            return Main.Library.Find(fileName);
        }

        /// <summary>Name label and preview text for the level the window is on.</summary>
        public void Refresh() {
            if (root == null) return;
            var entry = Assigned(out string fileName);
            hasBuild = entry != null && entry.Ok;
            changeLabel.text = (fileName == null ? "bar.set" : "bar.change").i18n();
            nameLabel.fontStyle = fileName == null ? FontStyles.Italic : FontStyles.Normal;
            if (fileName == null) {
                nameLabel.text = "bar.no_build".i18n();
                SetDetails("");
                return;
            }
            if (!hasBuild) {
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
            // A press while the deferred Apply below is waiting would run it twice, mid page switch.
            if (PlannerController.Instance?.HasPendingAction == true) return;
            // A second creation in one session opens on the portrait page while the premade character is still set.
            // Leaving the premade there rebuilds the page list under the shown page and leaves it blank (Deck,
            // 2026-10-06). Show the premade page first, as a player clicking "Custom character" would, then apply.
            var window = WindowTracker.Window;
            if (window != null && WindowTracker.Controller?.State?.IsPregen == true && PlannerController.Instance != null) {
                var pregen = window.m_PhasesList.OfType<CharGenPregenPhaseVM>().FirstOrDefault();
                if (pregen != null && window.CurrentPhaseVM.Value != pregen && window.PhasesSelectionGroupRadioVM.TrySelectEntity(pregen)) {
                    PlannerController.Instance.RunLater(ApplyNow, PregenPageFrames);
                    return;
                }
            }
            ApplyNow();
        }

        const int PregenPageFrames = 20;

        void ApplyNow() {
            if (root == null) return;   // the window closed in between
            // The usual loop is "fix the file, press Apply again": always work from what is on disk now.
            Main.Library.Reload();
            var entry = Assigned(out _);
            if (entry == null || !entry.Ok) {
                Refresh();
                ShowDetails(true);
                return;
            }
            LastReport = LevelApplier.Apply(WindowTracker.Controller, entry.Build);
            SetDetails(Render(LastReport));
            ShowDetails(true);
            // No row or no window means nothing was changed — that includes the page shown.
            if (!LastReport.NoRow && !LastReport.NoWindow) PlannerController.Instance?.JumpToFirstOpenPage();
        }

        public static string Render(ApplyReport report) {
            if (report.NoWindow) return "bar.no_window".i18n();
            if (report.NoRow) return Strings.Format(report.Mythic ? "bar.no_row_mythic" : "bar.no_row", report.Level);
            var lines = new List<string>();
            if (report.History != null && report.History.Comparable && !report.History.Matches)
                lines.Add(Strings.Format("bar.history", report.History.Expected, report.History.Actual));
            lines.Add(Strings.Format("bar.result", report.AppliedCount, report.OpenCount));
            foreach (var step in report.Steps.OrderBy(s => s.Status == StepStatus.Open ? 0 : 1)) {
                if (step.Status != StepStatus.Open) {
                    string line0 = $"+ {step.Label} — {(step.Status == StepStatus.Applied ? "status.applied" : "status.already").i18n()}";
                    lines.Add(string.IsNullOrEmpty(step.Detail) ? line0 : $"{line0} ({step.Detail})");
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
