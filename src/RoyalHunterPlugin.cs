using System;
using BepInEx;
using BepInEx.Logging;

namespace RoyalHunter
{
    [BepInPlugin(ModId, "Royal Hunter", "1.0.0")]
    public class RoyalHunterPlugin : BaseUnityPlugin
    {
        public const string ModId = "royalhunter";

        internal static ManualLogSource Log;

        private bool initialized;
        private static int errorCount;

        private void OnEnable()
        {
            Log = Logger;
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            if (initialized)
                return;
            initialized = true;

            try
            {
                RoyalWingHooks.Apply();
                Log.LogInfo("Royal Hunter: wing hooks applied");
            }
            catch (Exception e)
            {
                Log.LogError(e);
            }
        }

        /// <summary>Пишет первые 10 ошибок в лог, чтобы не засорять его каждый кадр.</summary>
        internal static void Report(string where, Exception e)
        {
            if (errorCount++ < 10)
                Log.LogError("[" + where + "] " + e);
        }
    }
}
