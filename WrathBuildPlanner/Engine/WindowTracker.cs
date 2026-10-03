using System;
using HarmonyLib;
using Kingmaker.UI.MVVM._VM.CharGen;
using Kingmaker.UnitLogic.Class.LevelUp;

namespace WrathBuildPlanner.Engine {
    /// <summary>
    /// The level-up and character-creation windows share CharGenVM. There is no static accessor for the
    /// open one, so the constructor and DisposeImplementation are patched to track it.
    /// </summary>
    public static class WindowTracker {
        public static CharGenVM Window { get; private set; }
        public static LevelUpController Controller => Window?.m_LevelUpController;
        public static event Action Changed;

        [HarmonyPatch(typeof(CharGenVM), MethodType.Constructor,
            typeof(LevelUpController), typeof(Action), typeof(Action), typeof(LevelUpConfig))]
        static class Opened {
            static void Postfix(CharGenVM __instance) {
                Window = __instance;
                Logging.Log.Game.Info("level-up/creation window opened");
                Changed?.Invoke();
            }
        }

        [HarmonyPatch(typeof(CharGenVM), "DisposeImplementation")]
        static class Closed {
            static void Postfix(CharGenVM __instance) {
                if (Window != __instance) return;
                Window = null;
                Logging.Log.Game.Info("level-up/creation window closed");
                Changed?.Invoke();
            }
        }
    }
}
