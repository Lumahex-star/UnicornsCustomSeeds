using System;
using System.Collections.Generic;
using UnityEngine;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.Storage;
using Il2CppFluffyUnderware.DevTools.Extensions;
#elif MONO
using ScheduleOne;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.DevUtilities;
using ScheduleOne.Equipping;
using ScheduleOne.Growing;
using ScheduleOne.ItemFramework;
using ScheduleOne.ObjectScripts;
using ScheduleOne.Product;
using ScheduleOne.StationFramework;
using ScheduleOne.Storage;
using FluffyUnderware.DevTools.Extensions;
#endif

namespace UnicornsCustomSeeds.Seeds
{
    public class CocaFactory
    {
        // Coca seeds reuse the weed seed vial art, turned 45 degrees so they read as a
        // distinct item at a glance in inventory/shop grids.
        private const float CocaIconRotation = 45f;

        // Static registries for type-safe identification in patches.
        // Key: cloned QualityItemDefinition ID
        // Value for leaves: the linked base definition ID
        // Value for bases: the target cocaine mix ID
        public static readonly Dictionary<string, string> CustomLeafIdToBaseId
            = new Dictionary<string, string>();
        public static readonly Dictionary<string, string> CustomBaseIdToMixId
      = new Dictionary<string, string>();

        private Transform rootGameObject;
        private SeedDefinition baseSeedDefinition;
        private CocaPlant baseCocaPlantPrefab;
        private PlantHarvestable baseHarvestable;
        private FunctionalSeed baseFunctionalSeedPrefab;
        private Equippable_Seed baseEquippableSeedPrefab;
        private AvatarEquippable baseAvatarEquippablePrefab;
        private StoredItem baseStoredItem;

        private QualityItemDefinition baseCocaLeafDefinition;
        private QualityItemDefinition baseCocaineBaseDefinition;

        public CocaFactory(SeedDefinition baseSeed, QualityItemDefinition baseCocaLeaf, QualityItemDefinition baseCocaineBase)
        {
            baseSeedDefinition = baseSeed;
            baseCocaLeafDefinition = baseCocaLeaf;
            baseCocaineBaseDefinition = baseCocaineBase;

#if IL2CPP
            baseCocaPlantPrefab = baseSeed.PlantPrefab.TryCast<CocaPlant>();
#elif MONO
          baseCocaPlantPrefab = (CocaPlant)baseSeed.PlantPrefab;
#endif
            if (baseCocaPlantPrefab == null)
                Utility.Error("CocaFactory: Failed to cast PlantPrefab to CocaPlant.");

            baseHarvestable = baseCocaPlantPrefab?.Harvestable;
            baseFunctionalSeedPrefab = baseSeed.FunctionSeedPrefab;
            baseStoredItem = baseSeed.StoredItem;

#if IL2CPP
            baseEquippableSeedPrefab = baseSeed.Equippable.TryCast<Equippable_Seed>();
#elif MONO
    baseEquippableSeedPrefab = (Equippable_Seed)baseSeed.Equippable;
#endif
            if (baseEquippableSeedPrefab == null)
                Utility.Error("CocaFactory: Failed to cast Equippable to Equippable_Seed.");

            baseAvatarEquippablePrefab = baseEquippableSeedPrefab?.AvatarEquippable;

            GameObject go = new GameObject($"{baseSeedDefinition.ID}_CustomCocaSeeds");
            go.SetActive(false);
            GameObject.DontDestroyOnLoad(go);
            rootGameObject = go.transform;
        }

        public void DeleteChildren()
        {
            rootGameObject.DeleteChildren(true, false);
            CustomLeafIdToBaseId.Clear();
            CustomBaseIdToMixId.Clear();
        }

        public SeedDefinition CreateCocaSeedDefinition(ProductDefinition cocaineDef)
        {
            if (baseSeedDefinition == null) throw new InvalidOperationException("Base seed definition not initialized.");

            // Leaf, base, and seed icons all derive from the same mix's appearance — resolve once.
            CocaineAppearanceSettings appearance = ResolveCocaAppearance(cocaineDef);

            // Step 1: Clone cocaine base � Object.Instantiate works for ScriptableObjects under IL2CPP
            QualityItemDefinition customBase = CloneCustomCocaineBase(cocaineDef, appearance);

            // Step 2: Clone coca leaf, link to base
            QualityItemDefinition customLeaf = CloneCustomCocaLeaf(customBase, cocaineDef, appearance);

            // Step 3: Clone plant, wire harvestable product to custom leaf
            CocaPlant newPlant = CloneCocaPlant(customLeaf);

            // Step 4: Clone seed definition ScriptableObject
            SeedDefinition newSeed = UnityEngine.Object.Instantiate(baseSeedDefinition);
            newSeed.ID = $"{cocaineDef.ID}_customcocaseed";
            newSeed.name = newSeed.ID;
            newSeed.Name = $"{cocaineDef.name} Coca Seed";
            newSeed.Description = $"A custom coca seed that produces {cocaineDef.name}.";
            newSeed.Icon = baseSeedDefinition.Icon;

            newSeed.PlantPrefab = newPlant;
            newSeed.PlantPrefab.SeedDefinition = newSeed;
            newSeed.FunctionSeedPrefab = CloneFunctionalSeedPrefab(newSeed.ID);
            newSeed.FunctionSeedPrefab.name = $"{cocaineDef.ID}CocaSeed_Functional";
            newSeed.Equippable = CloneEquippableSeedPrefab(newSeed);
            newSeed.StoredItem = CloneStoredItem(newSeed.ID);

            AssignCocaIcon(newSeed, appearance);

            // Register intermediates in the Registry so patches can resolve them by ID
            Singleton<Registry>.Instance.AddToRegistry(customLeaf);
            Singleton<Registry>.Instance.AddToRegistry(customBase);

            Utility.Log($"CocaFactory: {newSeed.ID} ? leaf:{customLeaf.ID} ? base:{customBase.ID} ? mix:{cocaineDef.ID}");
            return newSeed;
        }

        /// <summary>
        /// Resolves the cocaine mix's appearance settings once, shared by the seed, leaf, and
        /// base icons (they all derive from the same mix). Falls back to
        /// CocaineDefinition.GetAppearanceSettings(Properties) if uninitialized.
        /// </summary>
        private CocaineAppearanceSettings ResolveCocaAppearance(ProductDefinition cocaineDef)
        {
#if IL2CPP
            CocaineDefinition cocaDef = cocaineDef.TryCast<CocaineDefinition>();
#elif MONO
            CocaineDefinition cocaDef = cocaineDef as CocaineDefinition;
#endif
            if (cocaDef == null)
            {
                Utility.Error($"CocaFactory: '{cocaineDef.ID}' is not a CocaineDefinition — no appearance available.");
                return null;
            }

            CocaineAppearanceSettings appearance = cocaDef.AppearanceSettings;
            if (appearance == null || appearance.IsUnintialized())
                appearance = CocaineDefinition.GetAppearanceSettings(cocaDef.Properties);

            if (appearance == null)
                Utility.Error($"CocaFactory: No appearance settings for '{cocaineDef.ID}'.");

            return appearance;
        }

        /// <summary>
        /// Generates a per-mix coloured icon from the cocaine mix's appearance settings,
        /// rotated 45 degrees to set coca seeds apart from weed seeds. Mirrors the icon
        /// step in SeedFactory.CreateSeedDefinition. Falls back to the base vial sprite
        /// (and ultimately the base seed's own icon) if generation fails.
        /// </summary>
        private void AssignCocaIcon(SeedDefinition newSeed, CocaineAppearanceSettings appearance)
        {
            if (SeedVisualsManager.baseSeedSprite == null || appearance == null) return;

            try
            {
                Utility.Log($"[COKE] Main Color: {appearance.MainColor}, Secondary Color: {appearance.SecondaryColor}");
                (Color top, Color bottom) = SeedVisualsManager.BoostContrastIfSimilar(appearance.MainColor, appearance.SecondaryColor);
                Sprite newIcon = SeedVisualsManager.GenerateIconWithKeyColorFill(
                    SeedVisualsManager.baseSeedSprite, top, bottom,
                    SeedVisualsManager.FillKeyColor, rotationDegrees: CocaIconRotation);
                newIcon.name = newSeed.name + "_icon";
                SeedVisualsManager.seedIcons[newSeed.ID] = newIcon;
                newSeed.Icon = newIcon;
            }
            catch (Exception e)
            {
                newSeed.Icon = SeedVisualsManager.baseSeedSprite;
                Utility.PrintException(e);
            }
        }

        /// <summary>
        /// Generates a per-mix coloured icon for a coca intermediate item (leaf or cocaine
        /// base) via key-color fill on its dedicated base sprite. Mirrors AssignCocaIcon's
        /// shape but generic over QualityItemDefinition since leaf/base share it, only the
        /// base sprite differs.
        /// </summary>
        private void AssignCocaIntermediateIcon(QualityItemDefinition clone, Sprite baseIcon, CocaineAppearanceSettings appearance)
        {
            if (baseIcon == null || appearance == null) return;

            try
            {
                (Color top, Color bottom) = SeedVisualsManager.BoostContrastIfSimilar(appearance.MainColor, appearance.SecondaryColor);
                Sprite newIcon = SeedVisualsManager.GenerateIconWithKeyColorFill(
                    baseIcon, top, bottom, SeedVisualsManager.FillKeyColor);
                if (newIcon == null) return;

                newIcon.name = clone.name + "_icon";
                SeedVisualsManager.seedIcons[clone.ID] = newIcon;
                clone.Icon = newIcon;
            }
            catch (Exception e)
            {
                clone.Icon = baseIcon;
                Utility.PrintException(e);
            }
        }

        private void GrowLabel(Transform root, string seedDefId)
        {
            Transform labelTransform = null;
            var items = root.GetComponentsInChildren<Transform>();
            foreach (var item in items)
            {
                //Utility.Log(item.name);
                if (item.name == "Label")
                {
                    labelTransform = item.transform;
                }
            }
            if (labelTransform != null)
            {
                labelTransform.localScale = new Vector3(1.05f, 1.05f, 2);
                labelTransform.position = new Vector3(0, -0.05f, 0);
                if (SeedVisualsManager.customMat == null)
                {
                    SeedVisualsManager.LoadSeedMaterial();
                }

                if (SeedVisualsManager.customMat != null)
                {
                    var rend = labelTransform.GetComponent<Renderer>();
                    // Create an instance of the material instead of using shared
                    rend.material = SeedVisualsManager.customMat;
                    labelTransform.gameObject.AddComponent<SeedVialLabel>();
                    labelTransform.name = labelTransform.name + ":" + seedDefId;
                }
                else
                {
                    Utility.Log("MATERIAL NOT LOADED!!!");
                }
            }
        }

        /// <summary>
        /// Clones baseCocaineBaseDefinition and patches its StationItem's CookableModule.Product
        /// to point at cocaineDef. This makes the LabOven output the right mix with zero oven patching.
        /// Also registers this base's ID in CustomBaseIdToMixId for CauldronPatches.
        /// </summary>
        private QualityItemDefinition CloneCustomCocaineBase(ProductDefinition cocaineDef, CocaineAppearanceSettings appearance)
        {
            QualityItemDefinition clone = UnityEngine.Object.Instantiate(baseCocaineBaseDefinition);
            clone.ID = $"{cocaineDef.ID}_customcocainebase";
            clone.name = clone.ID;
            clone.Name = $"{cocaineDef.name} Base";

            // Clone the StationItem so CookableModule.Product can be set per-mix
            // without affecting the vanilla cocainebase used by other cauldrons
            if (baseCocaineBaseDefinition.StationItem != null)
            {
                StationItem clonedStationItem = UnityEngine.Object.Instantiate(
         baseCocaineBaseDefinition.StationItem, rootGameObject);
                clonedStationItem.name = $"{cocaineDef.ID}_CocaineBaseStationItem";

                CookableModule cookable = clonedStationItem.GetModule<CookableModule>();
                if (cookable != null)
                {
                    cookable.Product = cocaineDef;
                    Utility.Log($"CocaFactory: Set CookableModule.Product = {cocaineDef.ID}");
                }
                else
                {
                    Utility.Error($"CocaFactory: No CookableModule on StationItem for {cocaineDef.ID}. LabOven will output vanilla product.");
                }
                clone.StationItem = clonedStationItem;
            }
            else
            {
                Utility.Error("CocaFactory: baseCocaineBaseDefinition.StationItem is null.");
            }

            AssignCocaIntermediateIcon(clone, SeedVisualsManager.baseCocaBaseSprite, appearance);

            // Register for CauldronPatches: base ID ? target mix ID
            CustomBaseIdToMixId[clone.ID] = cocaineDef.ID;
            return clone;
        }

        /// <summary>
        /// Clones baseCocaLeafDefinition. The StationItem is shared (not cloned) because
        /// CookableModule on the leaf drives Cauldron cook time, not output � output is
        /// intercepted by CauldronPatches via CustomLeafIdToBaseId.
        /// </summary>
        private QualityItemDefinition CloneCustomCocaLeaf(QualityItemDefinition customBase, ProductDefinition cocaineDef, CocaineAppearanceSettings appearance)
        {
            QualityItemDefinition clone = UnityEngine.Object.Instantiate(baseCocaLeafDefinition);
            clone.ID = $"{cocaineDef.ID}_customcocaleaf";
            clone.name = clone.ID;
            clone.Name = $"Coca Leaf ({cocaineDef.name})";
            // StationItem shared � controls Cauldron cook time; output overridden in patch
            clone.StationItem = baseCocaLeafDefinition.StationItem;

            AssignCocaIntermediateIcon(clone, SeedVisualsManager.baseCocaLeafSprite, appearance);

            // Register for CauldronPatches: leaf ID ? base ID
            CustomLeafIdToBaseId[clone.ID] = customBase.ID;
            return clone;
        }

        private CocaPlant CloneCocaPlant(QualityItemDefinition customLeaf)
        {
            CocaPlant newPlant = UnityEngine.Object.Instantiate(baseCocaPlantPrefab, rootGameObject);
            newPlant.gameObject.name = $"{customLeaf.ID}_CocaPlant";

            PlantHarvestable newHarvestable = UnityEngine.Object.Instantiate(baseHarvestable, rootGameObject);
            newHarvestable.gameObject.name = $"{customLeaf.ID}_Harvestable";
            newHarvestable.Product = customLeaf;
            newPlant.Harvestable = newHarvestable;

            foreach (PlantHarvestable pH in newPlant.GetComponentsInChildren<PlantHarvestable>())
                pH.Product = customLeaf;

            return newPlant;
        }

        private FunctionalSeed CloneFunctionalSeedPrefab(string seedDefId)
        {
            if (baseFunctionalSeedPrefab == null) throw new InvalidOperationException("Base functional seed prefab not initialized.");
            FunctionalSeed newSeed = UnityEngine.Object.Instantiate(baseFunctionalSeedPrefab, rootGameObject);
            newSeed.gameObject.name = $"{seedDefId}_Functional";
            GrowLabel(newSeed.transform, seedDefId);
            return newSeed;
        }

        private Equippable_Seed CloneEquippableSeedPrefab(SeedDefinition newDef)
        {
            if (baseEquippableSeedPrefab == null) throw new InvalidOperationException("Base equippable seed prefab not initialized.");
            Equippable_Seed newEquipSeed = UnityEngine.Object.Instantiate(baseEquippableSeedPrefab, rootGameObject);
            GrowLabel(newEquipSeed.transform, newDef.ID);
            newEquipSeed.gameObject.name = $"{newDef.ID}_Equippable";
            newEquipSeed.gameObject.layer = baseEquippableSeedPrefab.gameObject.layer;
            newEquipSeed.Seed = newDef;
            newEquipSeed.AvatarEquippable = CloneAvatarEquippablePrefab(newDef.ID);
            newEquipSeed.AvatarEquippable.name = $"{newDef.ID}_AvatarEquippable";
            return newEquipSeed;
        }

        private AvatarEquippable CloneAvatarEquippablePrefab(string newDefId)
        {
            if (baseAvatarEquippablePrefab == null) throw new InvalidOperationException("Base avatar equippable prefab not initialized.");
            AvatarEquippable newAvatarEquip = UnityEngine.Object.Instantiate(baseAvatarEquippablePrefab, rootGameObject);
            GrowLabel(newAvatarEquip.transform, newDefId);
            newAvatarEquip.gameObject.name = $"{newDefId}_AvatarEquippable";
            return newAvatarEquip;
        }

        private StoredItem CloneStoredItem(string newDefId)
        {
            if (baseStoredItem == null) throw new InvalidOperationException("Base stored item not initialized.");
            StoredItem newStoredItem = UnityEngine.Object.Instantiate(baseStoredItem, rootGameObject);
            GrowLabel(newStoredItem.transform, newDefId);
            newStoredItem.gameObject.name = $"{newDefId}_StoredItem";
            return newStoredItem;
        }

        /// <summary>
        /// Adds a custom leaf ID to a single Cauldron's IngredientSlot ItemFilter_ID
        /// whitelists. Shared by AddLeafToCauldrons (one-shot scene sweep) and
        /// AddKnownLeavesToCauldron (per-instance, called from Cauldron.Start).
        /// </summary>
        private static int AddLeafToCauldron(Cauldron cauldron, string customLeafId)
        {
            int patched = 0;
            foreach (ItemSlot slot in cauldron.IngredientSlots)
            {
                foreach (ItemFilter filter in slot.HardFilters)
                {
#if IL2CPP
                    ItemFilter_ID idFilter = filter.TryCast<ItemFilter_ID>();
#elif MONO
                    ItemFilter_ID idFilter = filter as ItemFilter_ID;
#endif
                    if (idFilter != null && !idFilter.IDs.Contains(customLeafId))
                    {
                        idFilter.IDs.Add(customLeafId);
                        patched++;
                    }
                }
            }
            return patched;
        }

        /// <summary>
        /// Adds a custom leaf ID to every Cauldron currently in the scene's IngredientSlot
        /// ItemFilter_ID whitelists. Call this after the leaf has been registered in the
        /// Registry. Mirrors CustomShroomsManager.AddSyringeToSpawnStations.
        ///
        /// This only reaches Cauldrons that already exist at call time — any Cauldron
        /// created afterwards (placed by the player, rebuilt on a network client, etc.)
        /// is caught by AddKnownLeavesToCauldron instead, via the Cauldron.Start patch.
        /// </summary>
        public static void AddLeafToCauldrons(QualityItemDefinition customLeaf)
        {
            Cauldron[] cauldrons = GameObject.FindObjectsOfType<Cauldron>();
            int patched = 0;
            foreach (Cauldron cauldron in cauldrons)
                patched += AddLeafToCauldron(cauldron, customLeaf.ID);
            Utility.Log($"CocaFactory: Added '{customLeaf.ID}' to {patched} cauldron slot filter(s).");
        }

        /// <summary>
        /// Adds every currently-known custom leaf ID to a single Cauldron's IngredientSlot
        /// filters. Called from CauldronStartPatch so a Cauldron always has the full,
        /// up-to-date whitelist the moment it exists — regardless of whether it was already
        /// in the scene when AddLeafToCauldrons last ran (see that method's remarks).
        /// </summary>
        public static void AddKnownLeavesToCauldron(Cauldron cauldron)
        {
            if (cauldron == null || CustomLeafIdToBaseId.Count == 0) return;

            int patched = 0;
            foreach (string customLeafId in CustomLeafIdToBaseId.Keys)
                patched += AddLeafToCauldron(cauldron, customLeafId);

            if (patched > 0)
                Utility.Log($"CocaFactory: Whitelisted {patched} custom leaf filter(s) on '{cauldron.name}' at Start.");
        }
    }
}
