using System;
using HarmonyLib;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.Persistence.Datas;
#elif MONO
using ScheduleOne.Management;
using ScheduleOne.Persistence.Datas;
#endif

namespace UnicornsCustomSeeds.Patches
{
    // StationRecipeField.Load(data) resolves the saved recipe with a single
    // Options.Find(RecipeID) and drops the ID if it isn't found, so a saved custom pseudo
    // recipe is lost whenever the station loads before PseudoFactory has injected it (cold
    // boot). Remember those misses; PseudoFactory.InjectRecipeForMix applies them later.
    // Parameter is bound positionally (__0) so this doesn't depend on the IL2CPP wrapper's
    // parameter name — a mismatch would abort patching for the whole assembly.
    [HarmonyPatch(typeof(StationRecipeField), "Load", new Type[] { typeof(StationRecipeFieldData) })]
    public static class StationRecipeField_Load_Patch
    {
        public static void Postfix(StationRecipeField __instance, StationRecipeFieldData __0)
        {
            try
            {
                if (__0 == null || string.IsNullOrEmpty(__0.RecipeID)) return;
                if (__instance.SelectedRecipe != null) return;
                if (!__0.RecipeID.Contains(PendingStationRecipeRegistry.CustomRecipeIdMarker)) return;

                PendingStationRecipeRegistry.Register(__instance, __0.RecipeID);
            }
            catch (Exception e) { Utility.PrintException(e); }
        }
    }
}
