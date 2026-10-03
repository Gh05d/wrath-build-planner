using System;
using System.Linq;
using Kingmaker.UI.MVVM._PCView.CharGen;
using Kingmaker.UI.MVVM._VM.CharGen;
using UnityEngine;
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
            Destroy(Instance.gameObject);
            Instance = null;
        }

        void Update() {
            try {
                SyncBar();
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
            Log.UI.Info("change build requested");
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
