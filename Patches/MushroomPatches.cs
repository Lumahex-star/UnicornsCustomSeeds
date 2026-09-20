using System;
using HarmonyLib;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.StationFramework;
#elif MONO
using ScheduleOne.ObjectScripts;
using ScheduleOne.StationFramework;
#endif

namespace UnicornsCustomSeeds.Patches
{
    // ─────────────────────────────────────────────────────────────────────────
    // MushroomBedStartPatch / MushroomSpawnStationStartPatch
    //
    // Mirrors PotStartPatch and CauldronStartPatch. Both
    // MushroomBed.Configuration.Spawn.Options and MushroomSpawnStation's
    // SyringeSlot ItemFilter_ID whitelist are per-instance runtime state, only
    // ever patched by one-shot FindObjectsOfType sweeps (AddSpawnToMushroomBeds /
    // AddSyringeToSpawnStations) — called at synthesis time and, for stations
    // only, from the syringe load-reconstruction path. Any bed or station that
    // didn't exist yet when the relevant sweep ran never gets the custom syringe,
    // so employees treat it as having nothing valid to work with — appearing to
    // "not recognize" custom syringes until a restart happens to reorder things
    // favorably.
    // ─────────────────────────────────────────────────────────────────────────
    // Neither MushroomBed nor MushroomSpawnStation declares its own Start() — unlike
    // Pot/Cauldron, which override a virtual Start() from a shared base class, these two
    // only declare Awake(). Patching "Start" here throws HarmonyException: Undefined
    // target method at PatchAll time, which aborts patching for the WHOLE assembly (every
    // other Harmony patch in the mod silently never applies). Awake() is the actual
    // per-instance lifecycle hook that exists on both types, so it's the closest analog.
    [HarmonyPatch(typeof(MushroomBed), "Awake")]
    public class MushroomBedStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MushroomBed __instance)
        {
            try { CustomShroomsManager.AddKnownSpawnsToBed(__instance); }
            catch (Exception e) { Utility.PrintException(e); }
        }
    }

    [HarmonyPatch(typeof(MushroomSpawnStation), "Awake")]
    public class MushroomSpawnStationStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MushroomSpawnStation __instance)
        {
            try { CustomShroomsManager.AddKnownSyringesToSpawnStation(__instance); }
            catch (Exception e) { Utility.PrintException(e); }
        }
    }
}
