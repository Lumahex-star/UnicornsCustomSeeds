using System;
using HarmonyLib;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne.ObjectScripts;
#elif MONO
using ScheduleOne.ObjectScripts;
#endif

namespace UnicornsCustomSeeds.Patches
{
    // ─────────────────────────────────────────────────────────────────────────
    // PotStartPatch
    //
    // CustomSeedsManager.AddSeedToPots (called at synthesis time, and from the
    // ProductManager.CreateWeed/CreateCocaine load-reconstruction patches) is a
    // one-shot GameObject.FindObjectsOfType<Pot>() sweep — it only reaches Pots
    // that already exist in the scene when it runs. Any Pot created afterwards
    // (placed by the player, rebuilt on a network client, or simply not yet
    // spawned when the sweep ran) never gets the custom seed added to its
    // Configuration.Seed.Options, so botanists treat it as having no valid seed
    // configured and never fetch matching items from storage — appearing to
    // "not recognize" custom seeds until the game is restarted.
    //
    // Patching Pot.Start guarantees every Pot instance is self-healing: the
    // moment it exists, it gets the current, full list of known custom seeds
    // (weed + coca), regardless of scene-sweep timing.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Pot), "Start")]
    public class PotStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Pot __instance)
        {
            try
            {
                CustomSeedsManager.AddKnownSeedsToPot(__instance);
            }
            catch (Exception e) { Utility.PrintException(e); }
        }
    }
}
