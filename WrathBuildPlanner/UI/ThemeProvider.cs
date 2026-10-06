using UnityEngine;
using UnityEngine.UI;
using WrathBuildPlanner.Logging;

namespace WrathBuildPlanner.UI {
    public static class ThemeProvider {
        public static Sprite ActionButtonNormal { get; private set; }
        public static Sprite ActionButtonHover { get; private set; }
        public static Sprite ActionButtonPressed { get; private set; }
        public static Sprite HudButton { get; private set; }
        public static Sprite BandMauve { get; private set; }
        public static Sprite BandBlue { get; private set; }
        public static Sprite PopupPaper { get; private set; }
        public static Sprite HintAnnotation { get; private set; }
        public static Sprite IconAdd { get; private set; }
        public static Sprite IconDelete { get; private set; }
        public static Sprite IconX { get; private set; }
        public static Sprite IconXHover { get; private set; }
        public static Sprite IconCheck { get; private set; }
        public static Sprite IconCheckHover { get; private set; }
        public static Sprite IconArrow { get; private set; }
        public static Sprite IconArrowHover { get; private set; }
        public static Sprite ToggleOn { get; private set; }
        public static Sprite ToggleOff { get; private set; }
        public static Sprite DividerFlourish { get; private set; }
        public static Sprite DividerLine { get; private set; }

        public static void Init() {
            // 9-slice border values are read directly from the original Owlcat sprite metadata
            // (UnityPy `m_Border`, axis order = left, bottom, right, top in pixels).
            ActionButtonNormal = Load("action_button_normal.png", new Vector4( 62,  35,  62, 35));
            ActionButtonHover  = Load("action_button_hover.png",  new Vector4( 62,  35,  62, 35));
            ActionButtonPressed= Load("action_button_pressed.png",new Vector4( 62,  35,  62, 35));
            HudButton          = Load("hud_button.png",           Vector4.zero);

            // Ink-on-parchment set (spec 2026-09-26). Borders from tools/extract_sprites.py.
            BandMauve       = Load("band_mauve.png",       new Vector4(112,   0, 112,  0));
            BandBlue        = Load("band_blue.png",        new Vector4(112,   0, 112,  0));
            PopupPaper      = Load("popup_paper.png",      new Vector4(139, 101, 145, 89));
            HintAnnotation  = Load("hint_annotation.png",  new Vector4( 72,  58,  72, 67));
            IconAdd         = Load("icon_add.png",         Vector4.zero);
            IconDelete      = Load("icon_delete.png",      Vector4.zero);
            IconX           = Load("icon_x.png",           Vector4.zero);
            IconXHover      = Load("icon_x_hover.png",     Vector4.zero);
            IconCheck       = Load("icon_check.png",       Vector4.zero);
            IconCheckHover  = Load("icon_check_hover.png", Vector4.zero);
            IconArrow       = Load("icon_arrow.png",       Vector4.zero);
            IconArrowHover  = Load("icon_arrow_hover.png", Vector4.zero);
            ToggleOn        = Load("toggle_on.png",        Vector4.zero);
            ToggleOff       = Load("toggle_off.png",       Vector4.zero);
            DividerFlourish = Load("divider_flourish.png", new Vector4(114,   0,  94,  0));
            DividerLine     = Load("divider_line.png",     new Vector4( 16,   0,  16,  0));

            var all = new[] {
                ActionButtonNormal, ActionButtonHover, ActionButtonPressed, HudButton,
                BandMauve, BandBlue, PopupPaper, HintAnnotation,
                IconAdd, IconDelete, IconX, IconXHover, IconCheck, IconCheckHover,
                IconArrow, IconArrowHover, ToggleOn, ToggleOff,
                DividerFlourish, DividerLine };
            int loaded = 0;
            foreach (var s in all) {
                if (s != null) loaded++;
            }
            Log.UI.Info($"ThemeProvider initialised — {loaded}/{all.Length} sprites loaded.");
        }

        static Sprite Load(string file, Vector4 border) =>
            AssetLoader.Load("icons", file, border);

        /// <summary>
        /// Applies a 3-state button sprite set to obj. Adds Image+Button if missing.
        /// No-op if normal sprite is null.
        /// </summary>
        public static void ApplyButton(GameObject obj, Sprite normal, Sprite hover, Sprite pressed) {
            if (obj == null || normal == null) return;
            var img = obj.GetComponent<Image>() ?? obj.AddComponent<Image>();
            img.sprite = normal;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = true;

            var btn = obj.GetComponent<Button>() ?? obj.AddComponent<Button>();
            btn.transition = Selectable.Transition.SpriteSwap;
            btn.spriteState = new SpriteState {
                highlightedSprite = hover,
                pressedSprite     = pressed,
                selectedSprite    = hover,
                disabledSprite    = normal,
            };
            btn.targetGraphic = img;
        }

        public static void ApplyActionButton(GameObject obj) =>
            ApplyButton(obj, ActionButtonNormal, ActionButtonHover, ActionButtonPressed);
    }
}
