using UnityEngine;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.PlayerTasks;
using Il2CppFluffyUnderware.DevTools.Extensions;
#elif MONO
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.ItemFramework;
using ScheduleOne.Growing;
using ScheduleOne.Product;
using ScheduleOne.PlayerTasks;
using FluffyUnderware.DevTools.Extensions;
#endif

namespace UnicornsCustomSeeds.Seeds
{
    public class SyringeFactory
    {
        private Transform rootGameObject;
        private SporeSyringeDefinition baseSyringeDef;
        private ShroomSpawnDefinition baseSpawnDefinition;
        private ShroomColony baseColonyPrefab;
        private ShroomDefinition baseShroomDef;
        private SpawnChunk baseChunkPrefab;

        public SyringeFactory(SporeSyringeDefinition baseSyringe)
        {
            baseSyringeDef = baseSyringe;
            baseSpawnDefinition = baseSyringe.SpawnDefinition;
            baseColonyPrefab = baseSpawnDefinition.ColonyPrefab;
            baseShroomDef = baseSpawnDefinition.Shroom;
            baseChunkPrefab = baseSpawnDefinition.ChunkPrefab;

            GameObject go = new GameObject($"{baseSyringeDef.ID}_CustomSyringes");
            go.SetActive(false);
            GameObject.DontDestroyOnLoad(go);
            rootGameObject = go.transform;
        }

        public SporeSyringeDefinition CreateSyringeDefinition(ShroomDefinition shroomDef)
        {
            if (baseSyringeDef == null) throw new InvalidOperationException("Base syringe definition not initialized.");

            SporeSyringeDefinition newSyringe = UnityEngine.Object.Instantiate(baseSyringeDef);
            newSyringe.ID = $"{shroomDef.ID}_customsyringedefinition";
            newSyringe.name = newSyringe.ID;
            newSyringe.Name = $"{shroomDef.name} Spore Syringe";

            // Use the original ShroomDefinition directly � it's already registered and initialized
            ShroomSpawnDefinition clonedSpawnDef = CloneSpawnDefinition(shroomDef.ID, shroomDef);
            newSyringe.SpawnDefinition = clonedSpawnDef;

            // Register the cloned ShroomSpawnDefinition so MushroomBed.CreateAndAssignColony_Server
            // can look it up by ID when the player applies the spawn to a bed
            Singleton<Registry>.Instance.AddToRegistry(clonedSpawnDef);
            Utility.Log($"Registered ShroomSpawnDefinition: {clonedSpawnDef.ID}");

            AssignShroomIcons(newSyringe, clonedSpawnDef, shroomDef);

            return newSyringe;
        }

        /// <summary>
        /// Generates per-mix coloured icons for both the syringe (player-carried item) and the
        /// spawn definition (used when applying to a MushroomBed) from the shroom mix's
        /// appearance settings. Mirrors CocaFactory's icon-assignment shape. Note
        /// ShroomAppearanceSettings uses PrimaryColor/SecondaryColor, not MainColor/SecondaryColor
        /// like weed/coca/pseudo.
        /// </summary>
        private void AssignShroomIcons(SporeSyringeDefinition syringe, ShroomSpawnDefinition spawnDef, ShroomDefinition shroomDef)
        {
            ShroomAppearanceSettings appearance = shroomDef.AppearanceSettings;
            if (appearance == null || appearance.IsUnintialized())
                appearance = ShroomDefinition.GetAppearanceSettings(shroomDef.Properties);

            if (appearance == null)
            {
                Utility.Error($"SyringeFactory: No appearance settings for '{shroomDef.ID}' — keeping base icons.");
                return;
            }

            (Color top, Color bottom) = PickMostDistinctPair(appearance);

            if (SeedVisualsManager.baseSyringeSprite != null)
            {
                try
                {
                    Sprite newIcon = SeedVisualsManager.GenerateIconWithKeyColorFill(
                        SeedVisualsManager.baseSyringeSprite, top, bottom, SeedVisualsManager.FillKeyColor);
                    if (newIcon != null)
                    {
                        newIcon.name = syringe.name + "_icon";
                        SeedVisualsManager.seedIcons[syringe.ID] = newIcon;
                        syringe.Icon = newIcon;
                    }
                }
                catch (Exception e)
                {
                    syringe.Icon = SeedVisualsManager.baseSyringeSprite;
                    Utility.PrintException(e);
                }
            }

            if (SeedVisualsManager.baseShroomSpawnSprite != null)
            {
                try
                {
                    Sprite newIcon = SeedVisualsManager.GenerateIconWithKeyColorFill(
                        SeedVisualsManager.baseShroomSpawnSprite, top, bottom, SeedVisualsManager.FillKeyColor);
                    if (newIcon != null)
                    {
                        newIcon.name = spawnDef.name + "_icon";
                        SeedVisualsManager.seedIcons[spawnDef.ID] = newIcon;
                        spawnDef.Icon = newIcon;
                    }
                }
                catch (Exception e)
                {
                    spawnDef.Icon = SeedVisualsManager.baseShroomSpawnSprite;
                    Utility.PrintException(e);
                }
            }
        }

        /// <summary>
        /// Shrooms have 3 candidate colors (Primary/Secondary always set, Spots only when
        /// HasSpots is true) but the icon gradient only uses 2. Primary+Secondary can end up
        /// nearly identical for some mixes, producing a flat, low-contrast gradient — so when
        /// spots are available, pick whichever of the 3 possible pairs has the most contrast
        /// (largest RGB distance) instead of always defaulting to Primary+Secondary.
        /// </summary>
        private (Color top, Color bottom) PickMostDistinctPair(ShroomAppearanceSettings appearance)
        {
            Color primary = appearance.PrimaryColor;
            Color secondary = appearance.SecondaryColor;

            if (!appearance.HasSpots)
                return (primary, secondary);

            Color spots = appearance.SpotsColor;
            float primarySecondary = SeedVisualsManager.ColorDistanceRgb(primary, secondary);
            float primarySpots = SeedVisualsManager.ColorDistanceRgb(primary, spots);
            float secondarySpots = SeedVisualsManager.ColorDistanceRgb(secondary, spots);

            float best = Mathf.Max(primarySecondary, Mathf.Max(primarySpots, secondarySpots));

            if (best == primarySpots) return (primary, spots);
            if (best == secondarySpots) return (secondary, spots);
            return (primary, secondary);
        }

        private ShroomSpawnDefinition CloneSpawnDefinition(string shroomId, ShroomDefinition shroomDef)
        {
            ShroomSpawnDefinition cloned = UnityEngine.Object.Instantiate(baseSpawnDefinition);
            cloned.ID = $"{shroomId}_customspawndefinition";
            cloned.name = cloned.ID;
            cloned.Name = $"{shroomDef.name} Spawn";
            cloned.Shroom = shroomDef;

            // Clone the colony prefab so its serialized _spawnDefinition field points to our
            // cloned spawn def. ShroomColony.GetHarvestedShroom() reads _spawnDefinition.Shroom
            // to determine what product to yield. Without this, the colony uses the vanilla def.
            ShroomColony clonedColony = UnityEngine.Object.Instantiate(baseColonyPrefab, rootGameObject);
            clonedColony.gameObject.name = $"{shroomId}_Colony";
            clonedColony._spawnDefinition = cloned;
            cloned.ColonyPrefab = clonedColony;

            return cloned;
        }

        public void DeleteChildren()
        {
            rootGameObject.DeleteChildren(true, false);
        }
    }
}
