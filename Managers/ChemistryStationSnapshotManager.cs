using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;

#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.UI.Stations;
#elif MONO
using FishNet;
using ScheduleOne.DevUtilities;
using ScheduleOne.EntityFramework;
using ScheduleOne.Management;
using ScheduleOne.ObjectScripts;
using ScheduleOne.StationFramework;
using ScheduleOne.UI.Stations;
#endif

namespace UnicornsCustomSeeds.Managers
{
    [Serializable]
    public class ChemistryStationSnapshotEntry
    {
        public string stationGuid { get; set; }
        public string recipeId { get; set; }
        public string destinationGuid { get; set; }
    }

    /// <summary>
    /// Mod-owned record of each chemistry station's selected recipe and destination, written
    /// alongside the game's save (UnicornsChemistryStations.json) and re-applied after load.
    /// The game's own restore of a saved custom pseudo recipe is lost on a cold boot and the
    /// destination is reset with it; rather than depend on when the game runs its load, this
    /// re-applies whatever is still empty once the custom recipes exist.
    /// </summary>
    public static class ChemistryStationSnapshotManager
    {
        public static readonly Dictionary<string, ChemistryStationSnapshotEntry> Saved
            = new Dictionary<string, ChemistryStationSnapshotEntry>(StringComparer.OrdinalIgnoreCase);

        // Until the post-load restore has finished, a station that looks empty may just not
        // have been restored yet, so a save in that window must not erase its snapshot.
        public static bool RestoreDone = true;

        public static void Clear()
        {
            Saved.Clear();
            RestoreDone = true;
        }

        private static ChemistryStationConfiguration GetConfig(ChemistryStation station)
        {
            EntityConfiguration config = station.Configuration;
            if (config == null) return null;
#if IL2CPP
            return config.TryCast<ChemistryStationConfiguration>();
#elif MONO
            return config as ChemistryStationConfiguration;
#endif
        }

        private static string DestinationGuidOf(ChemistryStationConfiguration config)
        {
            BuildableItem dest = config.Destination?.SelectedObject;
            return dest != null ? dest.GUID.ToString() : "";
        }

        public static List<ChemistryStationSnapshotEntry> CaptureForSave()
        {
            var merged = new Dictionary<string, ChemistryStationSnapshotEntry>(Saved, StringComparer.OrdinalIgnoreCase);

            foreach (ChemistryStation station in UnityEngine.Object.FindObjectsOfType<ChemistryStation>())
            {
                if (station == null || station.isGhost) continue;
                ChemistryStationConfiguration config = GetConfig(station);
                if (config == null) continue;

                string guid = station.GUID.ToString();
                merged.TryGetValue(guid, out ChemistryStationSnapshotEntry old);

                string recipeId = config.Recipe?.SelectedRecipe != null
                    ? PendingStationRecipeRegistry.IdOf(config.Recipe.SelectedRecipe) : "";
                string destGuid = DestinationGuidOf(config);

                if (!RestoreDone && old != null)
                {
                    if (string.IsNullOrEmpty(recipeId)) recipeId = old.recipeId ?? "";
                    if (string.IsNullOrEmpty(destGuid)) destGuid = old.destinationGuid ?? "";
                }

                if (string.IsNullOrEmpty(recipeId) && string.IsNullOrEmpty(destGuid))
                {
                    merged.Remove(guid);
                    continue;
                }

                merged[guid] = new ChemistryStationSnapshotEntry
                {
                    stationGuid = guid,
                    recipeId = recipeId,
                    destinationGuid = destGuid,
                };
            }

            Saved.Clear();
            foreach (var kvp in merged) Saved[kvp.Key] = kvp.Value;
            return new List<ChemistryStationSnapshotEntry>(merged.Values);
        }

        public static void LogStates(string when)
        {
            try
            {
                var canvas = Singleton<ChemistryStationInterface>.Instance;
                Utility.Log($"ChemistryStations[{when}]: canvasRecipes={canvas?.Recipes?.Count}, snapshotEntries={Saved.Count}");

                foreach (ChemistryStation station in UnityEngine.Object.FindObjectsOfType<ChemistryStation>())
                {
                    if (station == null || station.isGhost) continue;
                    ChemistryStationConfiguration config = GetConfig(station);
                    if (config == null)
                    {
                        Utility.Log($"ChemistryStations[{when}]: {station.GUID} has no configuration.");
                        continue;
                    }

                    string recipe = config.Recipe?.SelectedRecipe != null
                        ? PendingStationRecipeRegistry.IdOf(config.Recipe.SelectedRecipe) : "<none>";
                    BuildableItem dest = config.Destination?.SelectedObject;
                    string destText = dest != null ? $"{dest.name} ({dest.GUID})" : "<none>";
                    Utility.Log($"ChemistryStations[{when}]: {station.GUID} recipe={recipe} destination={destText}");
                }
            }
            catch (Exception e) { Utility.PrintException(e); }
        }

        public static IEnumerator RestoreWhenReady()
        {
            RestoreDone = false;
            if (!InstanceFinder.IsServer || Saved.Count == 0)
            {
                RestoreDone = true;
                yield break;
            }

            float[] waits = { 2f, 6f, 12f };
            for (int i = 0; i < waits.Length; i++)
            {
                yield return new WaitForSeconds(waits[i]);

                int unresolved = 0;
                try
                {
                    unresolved = ApplySaved();
                    LogStates($"after restore pass {i + 1}");
                }
                catch (Exception e) { Utility.PrintException(e); }

                if (unresolved == 0) break;
            }

            RestoreDone = true;
        }

        // Returns how many snapshot entries could not be fully applied yet.
        private static int ApplySaved()
        {
            int unresolved = 0;
            ChemistryStationInterface canvas = Singleton<ChemistryStationInterface>.Instance;
            IEnumerable<BuildableItem> buildables = null;

            foreach (ChemistryStation station in UnityEngine.Object.FindObjectsOfType<ChemistryStation>())
            {
                if (station == null || station.isGhost) continue;
                if (!Saved.TryGetValue(station.GUID.ToString(), out ChemistryStationSnapshotEntry entry)) continue;

                ChemistryStationConfiguration config = GetConfig(station);
                if (config == null) { unresolved++; continue; }

                bool complete = true;

                if (!string.IsNullOrEmpty(entry.recipeId) && config.Recipe.SelectedRecipe == null)
                {
                    StationRecipe recipe = canvas != null
                        ? PendingStationRecipeRegistry.FindRecipe(canvas, entry.recipeId) : null;
                    if (recipe != null)
                    {
                        config.Recipe.SetRecipe(recipe, false);
                        Utility.Log($"ChemistryStationSnapshotManager: Restored recipe '{entry.recipeId}' on {station.GUID}.");
                    }
                    else
                    {
                        Utility.Log($"ChemistryStationSnapshotManager: Recipe '{entry.recipeId}' for {station.GUID} not available yet.");
                        complete = false;
                    }
                }

                if (!string.IsNullOrEmpty(entry.destinationGuid) && config.Destination.SelectedObject == null)
                {
                    if (buildables == null) buildables = UnityEngine.Object.FindObjectsOfType<BuildableItem>();

                    BuildableItem target = null;
                    foreach (BuildableItem item in buildables)
                    {
                        if (item != null && string.Equals(item.GUID.ToString(), entry.destinationGuid, StringComparison.OrdinalIgnoreCase))
                        {
                            target = item;
                            break;
                        }
                    }

                    if (target != null && TrySetDestination(config.Destination, target))
                    {
                        Utility.Log($"ChemistryStationSnapshotManager: Restored destination {entry.destinationGuid} on {station.GUID}.");
                    }
                    else
                    {
                        Utility.Log($"ChemistryStationSnapshotManager: Destination {entry.destinationGuid} for {station.GUID} not restored (found={target != null}).");
                        complete = false;
                    }
                }

                if (!complete) unresolved++;
            }

            return unresolved;
        }

        // ObjectField's setter isn't in any source I have; look it up by name so a wrong guess
        // logs the real method names instead of failing the build.
        private static bool TrySetDestination(ObjectField field, BuildableItem target)
        {
            MethodInfo[] methods = field.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (MethodInfo m in methods)
            {
                if (m.Name != "SetObject" || m.GetParameters().Length != 2) continue;
                try
                {
                    m.Invoke(field, new object[] { target, false });
                    return true;
                }
                catch (Exception e)
                {
                    Utility.PrintException(e);
                    return false;
                }
            }

            var names = new List<string>();
            foreach (MethodInfo m in methods)
                if (m.DeclaringType == field.GetType()) names.Add(m.Name);
            Utility.Error($"ChemistryStationSnapshotManager: ObjectField has no SetObject(item, bool). Methods: {string.Join(", ", names)}");
            return false;
        }
    }
}
