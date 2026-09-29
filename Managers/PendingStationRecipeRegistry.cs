using System;
using System.Collections.Generic;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.UI.Stations;
#elif MONO
using ScheduleOne.DevUtilities;
using ScheduleOne.Management;
using ScheduleOne.StationFramework;
using ScheduleOne.UI.Stations;
#endif

namespace UnicornsCustomSeeds.Managers
{
    /// <summary>
    /// StationRecipeField.Load does a one-shot lookup of the saved RecipeID in Options
    /// (ChemistryStationInterface.Recipes) and silently leaves SelectedRecipe null if it
    /// isn't there yet — the saved ID isn't kept anywhere. Custom pseudo recipes are only
    /// injected once PseudoFactory is ready, which on a cold boot is after stations have
    /// already loaded. This registry remembers the failed lookups so they can be applied
    /// once the recipe exists, regardless of timing.
    /// </summary>
    public static class PendingStationRecipeRegistry
    {
        // StationRecipe.RecipeID is "{Product.Quantity}x{Product.Item.ID}"; our custom recipes
        // always produce a "{mixId}_customliquidmeth" item, so this identifies them.
        public const string CustomRecipeIdMarker = "_customliquidmeth";

        private class PendingEntry
        {
            public StationRecipeField Field;
            public string RecipeId;
        }

        private static readonly List<PendingEntry> Pending = new List<PendingEntry>();

        public static void Register(StationRecipeField field, string recipeId)
        {
            foreach (PendingEntry existing in Pending)
            {
                if (existing.Field == field)
                {
                    existing.RecipeId = recipeId;
                    return;
                }
            }
            Pending.Add(new PendingEntry { Field = field, RecipeId = recipeId });
            Utility.Log($"PendingStationRecipeRegistry: '{recipeId}' not in ChemistryStationInterface.Recipes at load — queued for retry.");
        }

        public static void ResolveAll()
        {
            if (Pending.Count == 0) return;

            ChemistryStationInterface canvas = Singleton<ChemistryStationInterface>.Instance;
            if (canvas == null || canvas.Recipes == null) return;

            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                PendingEntry entry = Pending[i];
                try
                {
                    StationRecipe match = FindRecipe(canvas, entry.RecipeId);
                    if (match == null) continue;

                    if (entry.Field.SelectedRecipe == null)
                        entry.Field.SetRecipe(match, false);

                    Pending.RemoveAt(i);
                    Utility.Log($"PendingStationRecipeRegistry: Restored saved recipe '{entry.RecipeId}'.");
                }
                catch (Exception e)
                {
                    Utility.PrintException(e);
                    Pending.RemoveAt(i);
                }
            }
        }

        public static string IdOf(StationRecipe recipe)
        {
            if (recipe == null || recipe.Product == null || recipe.Product.Item == null) return "";
            return $"{recipe.Product.Quantity}x{recipe.Product.Item.ID}";
        }

        public static StationRecipe FindRecipe(ChemistryStationInterface canvas, string recipeId)
        {
            foreach (StationRecipe recipe in canvas.Recipes)
            {
                if (recipe != null && IdOf(recipe) == recipeId)
                    return recipe;
            }
            return null;
        }

        public static void Clear() => Pending.Clear();
    }
}
