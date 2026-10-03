using Kingmaker;
using Kingmaker.UI.MVVM._PCView.CharGen;
using UnityEngine;

namespace WrathBuildPlanner.UI {
    /// <summary>
    /// Which canvas to draw on. Character creation of a new game runs in the main menu, where the in-game
    /// canvas does not exist, so the creation window's own root canvas is preferred whenever it is open.
    /// </summary>
    public static class UiRoot {
        public static CharGenPCView OpenWindowView() => Object.FindObjectOfType<CharGenPCView>();

        public static Transform Canvas() {
            var view = OpenWindowView();
            if (view != null) {
                var canvas = view.GetComponentInParent<Canvas>();
                if (canvas != null) return canvas.rootCanvas.transform;
            }
            var ingame = Game.Instance?.UI?.Canvas;
            return ingame != null ? ingame.transform : null;
        }
    }
}
