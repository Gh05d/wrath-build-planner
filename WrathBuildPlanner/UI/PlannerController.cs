using System;
using System.Linq;
using Kingmaker;
using Kingmaker.UI.MVVM._PCView.CharGen;
using Kingmaker.UI.MVVM._VM.CharGen;
using Kingmaker.UnitLogic.Class.LevelUp;
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
        CharGenVM searchWindow;
        int searchFrames;
        const int SearchEveryFrames = 5;
        const int MaxSearchFrames = 600;
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
                bar?.Destroy();
                bar = null;
                barWindow = null;
                searchWindow = null;
                return;
            }
            if (barWindow == window) return;

            // Respec is its own window mode and unverified: no bar there (spec: hidden if it does not hold).
            if (window.m_LevelUpController?.State?.Mode == LevelUpState.CharBuildMode.Respec) {
                barWindow = window;
                return;
            }

            // The view binds a few frames after the view model. In the gamepad UI it never appears,
            // so the (expensive) search stops after a while instead of running for the whole level-up.
            if (searchWindow != window) {
                searchWindow = window;
                searchFrames = 0;
            }
            if (searchFrames > MaxSearchFrames || ++searchFrames % SearchEveryFrames != 1) return;
            var view = UiRoot.OpenWindowView();
            if (view == null) return;

            barWindow = window;
            bar?.Destroy();
            bar = null;
            // Files may have been edited or removed since the last window: read the folder each time.
            Main.Library.Reload();
            try {
                bar = BuildBar.Create(view);
                bar.ChangeRequested += OnChangeRequested;
                Log.UI.Info("build bar created");
            } catch (Exception e) {
                // No retry for this window: a retry per frame would pile up half-built bars.
                Log.UI.Error(e, "build bar could not be created");
                BuildBar.DestroyLeftovers(view);
            }
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
