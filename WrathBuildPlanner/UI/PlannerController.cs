using System;
using System.Linq;
using Kingmaker;
using Kingmaker.UI.MVVM._PCView.CharGen;
using Kingmaker.UI.MVVM._VM.CharGen;
using UnityEngine;
using UnityEngine.UI;
using WrathBuildPlanner.Engine;
using WrathBuildPlanner.Logging;

namespace WrathBuildPlanner.UI {
    /// <summary>
    /// One always-alive controller object. Creates the bar once the open window's view exists
    /// (the view binds a few frames after the view model), removes it when the window closes.
    /// </summary>
    public class PlannerController : MonoBehaviour {
        public static PlannerController Instance { get; private set; }

        BuildBar bar;
        CharGenVM barWindow;
        bool libraryLoaded;
        GameObject hudButton;
        int jumpInFrames;

        // The window's pages refresh from the controller on LateUpdate and the page list can be rebuilt then.
        // Switching pages in the same frame as the picks left the view on a blank page (observed in-game),
        // so the jump waits a few frames.
        const int JumpDelayFrames = 10;

        public BuildBar Bar => bar;

        public static void Install() {
            if (Instance != null) return;
            var go = new GameObject("WrathBuildPlannerController");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<PlannerController>();
        }

        public static void Uninstall() {
            if (Instance == null) return;
            Instance.bar?.Destroy();
            BuildsWindow.Close();
            if (Instance.hudButton != null) Destroy(Instance.hudButton);
            Destroy(Instance.gameObject);
            Instance = null;
        }

        void Update() {
            try {
                SyncBar();
                SyncHudButton();
                HandleKeys();
                if (jumpInFrames > 0 && --jumpInFrames == 0) JumpNow();
            } catch (Exception e) {
                Log.UI.Error(e, "build bar sync failed");
            }
        }

        void SyncBar() {
            var window = WindowTracker.Window;
            if (window == null) {
                if (bar != null) {
                    bar.Destroy();
                    bar = null;
                    barWindow = null;
                }
                return;
            }
            if (bar != null && barWindow == window) return;

            var view = UiRoot.OpenWindowView();
            if (view == null) return;
            if (!libraryLoaded) {
                Main.Library.Reload();
                libraryLoaded = true;
            }
            bar?.Destroy();
            bar = BuildBar.Create(view);
            barWindow = window;
            bar.ChangeRequested += OnChangeRequested;
            Log.UI.Info("build bar created");
        }

        void OnChangeRequested() {
            BuildsWindow.Toggle();
        }

        void HandleKeys() {
            if (BuildsWindow.IsOpen && Input.GetKeyDown(KeyCode.Escape)) {
                BuildsWindow.Close();
                Input.ResetInputAxes();
                return;
            }
            if (!Enum.TryParse(Persistence.ModSettings.Current.Hotkey, true, out KeyCode key)) return;
            if (Input.GetKeyDown(key) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                BuildsWindow.Toggle();
        }

        // In-game only. A floating button bottom-left, above the spot Wrath Tactics uses for its own.
        void SyncHudButton() {
            var canvas = Game.Instance?.UI?.Canvas;
            if (canvas == null || Game.Instance.CurrentlyLoadedArea == null) {
                if (hudButton != null) {
                    Destroy(hudButton);
                    hudButton = null;
                }
                return;
            }
            if (hudButton != null) return;

            var (button, rect) = UIHelpers.Create("WrathBuildPlannerHudBtn", canvas.transform);
            hudButton = button;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(20f, 176f);
            rect.sizeDelta = new Vector2(48f, 48f);
            var image = button.AddComponent<Image>();
            if (ThemeProvider.HudButton != null) {
                image.sprite = ThemeProvider.HudButton;
                image.preserveAspect = true;
            } else {
                image.color = Theme.TitleFallback;
            }
            var click = button.AddComponent<Button>();
            click.targetGraphic = image;
            click.onClick.AddListener(BuildsWindow.Toggle);
        }


        /// <summary>
        /// Moves the window to the first page that still needs the player, or to the last page (summary)
        /// when nothing is open. Pages unlock in order, so "first not completed" is always reachable.
        /// </summary>
        public void JumpToFirstOpenPage() {
            jumpInFrames = JumpDelayFrames;
        }

        void JumpNow() {
            var window = WindowTracker.Window;
            if (window == null) return;
            var group = window.PhasesSelectionGroupRadioVM;
            var pages = group.EntitiesCollection.ToList();
            var target = pages.FirstOrDefault(p => !p.IsCompleted.Value) ?? pages.LastOrDefault();
            if (target != null) group.TrySelectEntity(target);
        }
    }
}
