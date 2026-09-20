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
    // Pot/Cauldron, which override a virtual Start() from a shared base class. Patching
    // "Start" here throws HarmonyException: Undefined target method at PatchAll time,
    // which aborts patching for the WHOLE assembly (every other Harmony patch in the mod
    // silently never applies). Awake() was tried next, but that's wrong too: per the
    // decompiled source, MushroomBed.Configuration and MushroomSpawnStation.SyringeSlot
    // (with its HardFilters) are both null until InitializeGridItem(...) runs — a
    // grid-placement/load callback, not a Unity message, that fires after Awake() and
    // isn't guaranteed before Start() either. AddKnownSpawnsToBed/
    // AddKnownSyringesToSpawnStation dereference both, so patching Awake() threw
    // NullReferenceException on every bed/station in the scene. InitializeGridItem is the
    // actual method that assigns them (see its body in MushroomBed.cs/
    // MushroomSpawnStation.cs), so patch that instead — a postfix here is guaranteed to
    // run after both are set.
    [HarmonyPatch(typeof(MushroomBed), "InitializeGridItem")]
    public class MushroomBedStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MushroomBed __instance)
        {
            try { CustomShroomsManager.AddKnownSpawnsToBed(__instance); }
            catch (Exception e) { Utility.PrintException(e); }
        }
    }

    [HarmonyPatch(typeof(MushroomSpawnStation), "InitializeGridItem")]
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
