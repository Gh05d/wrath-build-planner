using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrathBuildPlanner.Core;
using WrathBuildPlanner.Engine;
using WrathBuildPlanner.Localization;
using WrathBuildPlanner.Persistence;

namespace WrathBuildPlanner.UI {
    public static class BuildsWindow {
        // Canvas units: the game's canvases are laid out for a 1920-wide screen and scaled down
        // (two thirds on the Deck), so the sheet is 1000x667 px on a 1280x800 screen.
        const float Width = 1500f;
        const float Height = 1000f;
        const float RowHeight = 52f;
        const float TitleFont = 30f;
        const float HeaderFont = 24f;
        const float RowFont = 21f;
        const float ButtonFont = 19f;
        const float NoteFont = 17f;

        static GameObject overlay;
        static GameObject partyList;
        static GameObject libraryList;
        static TextMeshProUGUI message;

        public static bool IsOpen => overlay != null;

        public static void Toggle() {
            if (IsOpen) Close();
            else Open();
        }

        public static void Close() {
            if (overlay != null) UnityEngine.Object.Destroy(overlay);
            overlay = null;
        }

        static void Open() {
            var canvas = UiRoot.Canvas();
            if (canvas == null) return;
            Main.Library.Reload();

            var (root, rootRect) = UIHelpers.Create("WrathBuildPlannerWindow", canvas);
            overlay = root;
            rootRect.FillParent();
            UIHelpers.AddBackground(root, Theme.DimBackdrop);
            root.AddComponent<Button>().onClick.AddListener(Close);

            var (sheet, sheetRect) = UIHelpers.Create("Sheet", root.transform);
            sheetRect.anchorMin = sheetRect.anchorMax = sheetRect.pivot = new Vector2(0.5f, 0.5f);
            sheetRect.sizeDelta = new Vector2(Width, Height);
            var paper = sheet.AddComponent<Image>();
            if (ThemeProvider.PopupPaper != null) {
                paper.sprite = ThemeProvider.PopupPaper;
                paper.type = Image.Type.Sliced;
                paper.pixelsPerUnitMultiplier = 2f;
            } else {
                paper.color = Theme.PaperFallback;
            }
            // Swallow clicks so only the dim area closes the window.
            var swallow = sheet.AddComponent<Button>();
            swallow.targetGraphic = paper;
            swallow.transition = Selectable.Transition.None;

            var title = Widgets.InkLabel(Place(sheet, "Title", 60f, -40f, Width - 120f, 56f), "window.title".i18n(), TitleFont);
            title.fontStyle = FontStyles.Bold;
            var close = Widgets.IconButton(sheet.transform, "Close", Icon.X, 36f, Close);
            var closeRect = close.Rect();
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-56f, -50f);
            closeRect.sizeDelta = new Vector2(36f, 36f);

            Widgets.InkLabel(Place(sheet, "PartyHeader", 60f, -112f, 640f, 40f), "window.party".i18n(), HeaderFont).fontStyle = FontStyles.Bold;
            partyList = Column(Place(sheet, "Party", 60f, -160f, 640f, 620f));

            Widgets.InkLabel(Place(sheet, "LibraryHeader", 750f, -112f, 690f, 40f), "window.library".i18n(), HeaderFont).fontStyle = FontStyles.Bold;
            libraryList = Column(Place(sheet, "Library", 750f, -160f, 690f, 490f));
            // Opening a URL under Proton may not show a browser: the address also goes to the clipboard.
            var link = Widgets.InlineLink(Place(sheet, "AiLink", 750f, -660f, 690f, 40f).transform, "Link", "window.ai".i18n(), () => {
                GUIUtility.systemCopyBuffer = Main.AuthoringUrl;
                Application.OpenURL(Main.AuthoringUrl);
                Say(Strings.Format("window.ai_opened", Main.AuthoringUrl));
            }, fontSize: RowFont);
            // The link sizes itself through its LayoutElement; this box has no layout group, so pin it left.
            var linkRect = link.Rect();
            linkRect.anchorMin = new Vector2(0f, 0f);
            linkRect.anchorMax = new Vector2(0f, 1f);
            linkRect.pivot = new Vector2(0f, 0.5f);
            linkRect.anchoredPosition = Vector2.zero;
            linkRect.sizeDelta = new Vector2(link.GetComponent<LayoutElement>().preferredWidth, 0f);

            var buttons = Widgets.Row(Place(sheet, "Buttons", 750f, -710f, 690f, 60f).transform, "Row", 54f);
            Stretch(buttons.Rect());
            Widgets.ActionButton(buttons.transform, "Paste", "window.paste".i18n(), ButtonFont, PasteFromClipboard, 360f);
            Widgets.ActionButton(buttons.transform, "Reload", "window.reload".i18n(), ButtonFont, () => {
                Main.Library.Reload();
                Say("");
                Rebuild();
            }, 280f);

            message = Widgets.InkLabel(Place(sheet, "Message", 60f, -800f, Width - 120f, 150f), "", NoteFont + 2f, TextAlignmentOptions.TopLeft);
            message.enableWordWrapping = true;

            Rebuild();
        }

        // A box whose top-left corner sits at (x, y) from the sheet's top-left corner (y negative = down).
        // The pivot stays centred: the kit's helpers create children at the parent's pivot and only then
        // stretch them, so a corner pivot leaves every child shifted by half the box.
        static GameObject Place(GameObject parent, string name, float x, float y, float width, float height) {
            var (obj, rect) = UIHelpers.Create(name, parent.transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x + width / 2f, y - height / 2f);
            rect.sizeDelta = new Vector2(width, height);
            return obj;
        }

        static void Stretch(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // A scrollable vertical list inside the given box; returns the content object rows are added to.
        static GameObject Column(GameObject box) {
            var scroll = box.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            box.AddComponent<RectMask2D>();
            var (content, contentRect) = UIHelpers.Create("Content", box.transform);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = box.Rect();
            scroll.content = contentRect;
            return content;
        }

        static void Clear(GameObject list) {
            for (int i = list.transform.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(list.transform.GetChild(i).gameObject);
        }

        static void Rebuild() {
            if (!IsOpen) return;
            Clear(partyList);
            Clear(libraryList);

            foreach (var unit in Characters()) {
                var captured = unit;
                string fileName = AssignmentStore.Get(unit);
                var entry = Main.Library.Find(fileName);
                string buildName = fileName == null ? "window.none".i18n()
                    : entry != null && entry.Ok ? DisplayName(entry) : Strings.Format("bar.missing", fileName);

                var row = Widgets.Row(partyList.transform, "Unit", RowHeight);
                Widgets.InRow(Widgets.InkLabel(row, unit.CharacterName, RowFont).gameObject, 220f, 0f);
                Widgets.ActionButton(row.transform, "Pick", buildName, ButtonFont, () => PickBuild(captured), 300f, 1f);

                string notice = Notice(unit, entry);
                if (notice != null) {
                    var note = Widgets.Row(partyList.transform, "Note", RowHeight);
                    var label = Widgets.InkLabel(note, notice, NoteFont, TextAlignmentOptions.MidlineLeft, Theme.InkMuted, true);
                    label.enableWordWrapping = true;
                    Widgets.InRow(label.gameObject, 100f, 1f);
                }
            }

            if (Main.Library.Entries.Count == 0) {
                var empty = Widgets.Row(libraryList.transform, "Empty", RowHeight * 2f);
                var label = Widgets.InkLabel(empty, "window.empty_library".i18n(), NoteFont + 2f, TextAlignmentOptions.TopLeft, Theme.InkMuted);
                label.enableWordWrapping = true;
                Widgets.InRow(label.gameObject, 100f, 1f);
            }
            foreach (var entry in Main.Library.Entries) {
                var captured = entry;
                var row = Widgets.Row(libraryList.transform, "Build", RowHeight);
                string name = DisplayName(entry);
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(entry.Build?.Author)) parts.Add(Strings.Format("window.by", entry.Build.Author));
                if (!string.IsNullOrWhiteSpace(entry.Build?.For)) parts.Add(Strings.Format("window.for", entry.Build.For));
                if (!string.IsNullOrWhiteSpace(entry.Build?.Source)) parts.Add(entry.Build.Source);
                string text = parts.Count > 0 ? $"{name}  ({string.Join(", ", parts)})" : name;
                var label = Widgets.InkLabel(row, text, RowFont - 1f);
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Ellipsis;
                Widgets.InRow(label.gameObject, 100f, 1f);

                int errors = entry.Issues.Count(i => i.IsError);
                string status = entry.Ok ? "window.valid".i18n() : Strings.Format("window.errors", errors);
                Widgets.ActionButton(row.transform, "Status", status, NoteFont, () => ShowIssues(captured), 170f);
            }
        }

        // In creation there is no party: the unit being created is the only row.
        static List<UnitEntityData> Characters() {
            var units = new List<UnitEntityData>();
            var creating = WindowTracker.Controller?.Unit;
            if (creating != null) units.Add(creating);
            var party = Game.Instance?.Player?.Party;
            if (party != null) units.AddRange(party.Where(u => u != creating));
            return units;
        }

        static string Notice(UnitEntityData unit, LibraryEntry entry) {
            if (entry == null || !entry.Ok) return null;
            int next = unit.Progression.CharacterLevel + 1;
            var classes = GameNames.Classes();
            Func<string, string> canonical = name => {
                var outcome = NameMatcher.Match(name, classes);
                return outcome.Kind == MatchKind.Unique ? outcome.Match.Display : name;
            };
            var expected = LevelPlanner.ExpectedClassLevels(entry.Build, next, canonical);
            var actual = unit.Progression.Classes.Where(c => !c.CharacterClass.IsMythic).ToDictionary(c => c.CharacterClass.Name, c => c.Level);
            var history = LevelPlanner.CompareHistory(expected, actual);
            if (history.Comparable && !history.Matches) return Strings.Format("bar.history", history.Expected, history.Actual);
            return LevelPlanner.RowFor(entry.Build, next) != null ? Strings.Format("window.next_level", next) : Strings.Format("bar.no_row", next);
        }

        // Two files can carry the same build name (a second paste becomes name-2.json): show the file then.
        static string DisplayName(LibraryEntry entry) {
            string name = entry.Build?.Name;
            if (string.IsNullOrWhiteSpace(name)) return entry.FileName;
            bool shared = Main.Library.Entries.Count(e => e.Build?.Name == name) > 1;
            return shared ? $"{name} [{entry.FileName}]" : name;
        }

        static void ShowIssues(LibraryEntry entry) {
            Say(entry.Issues.Count == 0 ? $"{entry.FileName}: {"window.valid".i18n()}"
                : entry.FileName + ":\n" + string.Join("\n", entry.Issues.Select(i => (i.IsError ? "! " : "- ") + i.Message)));
        }

        static void Say(string text) {
            if (message != null) message.text = text;
        }

        static void PasteFromClipboard() {
            ImportText(GUIUtility.systemCopyBuffer);
        }

        public static string ImportText(string text) {
            if (string.IsNullOrWhiteSpace(text)) {
                Say("window.clipboard_empty".i18n());
                return "EMPTY";
            }
            var result = Main.Library.ImportText(text);
            if (result.Ok) Say(Strings.Format("window.imported", result.FileName));
            else Say("window.import_failed".i18n() + "\n" + string.Join("\n", result.Issues.Where(i => i.IsError).Select(i => "! " + i.Message)));
            Rebuild();
            return result.Ok ? "OK " + result.FileName : "FAILED " + string.Join("; ", result.Issues.Where(i => i.IsError).Select(i => i.Message));
        }

        // Own picker instead of the kit's: the kit draws on the in-game canvas, which does not exist in the main menu.
        static void PickBuild(UnitEntityData unit) {
            var valid = Main.Library.Entries.Where(e => e.Ok).ToList();
            if (overlay == null) return;

            // A child of the window, so closing the window (Escape, outside click) takes the picker with it.
            var (dim, dimRect) = UIHelpers.Create("WrathBuildPlannerPicker", overlay.transform);
            dimRect.FillParent();
            UIHelpers.AddBackground(dim, Theme.DimPopup);
            dim.AddComponent<Button>().onClick.AddListener(() => UnityEngine.Object.Destroy(dim));

            float height = Mathf.Min(700f, (valid.Count + 1) * (RowHeight + 6f) + 40f);
            var (box, boxRect) = UIHelpers.Create("Box", dim.transform);
            boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(600f, height);
            UIHelpers.AddBackground(box, Theme.PaperFallback);
            var list = Column(box);
            list.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(18, 18, 18, 18);

            Action<string, string> add = (label, fileName) => {
                var row = Widgets.Row(list.transform, "Option", RowHeight);
                Widgets.ActionButton(row.transform, "Choose", label, ButtonFont + 1f, () => {
                    AssignmentStore.Set(unit, fileName);
                    UnityEngine.Object.Destroy(dim);
                    PlannerController.Instance?.Bar?.Refresh();
                    Rebuild();
                }, 520f, 1f);
            };
            add("window.none".i18n(), null);
            foreach (var entry in valid) add(DisplayName(entry), entry.FileName);
        }
    }
}
