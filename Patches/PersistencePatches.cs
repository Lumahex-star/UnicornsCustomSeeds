using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.TemplateUtils;
using System.Collections.Generic;
using System;
using System.IO;



#if IL2CPP
using Il2CppFishNet.Connection;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI.MainMenu;
using GenericCol = Il2CppSystem.Collections.Generic;
#elif MONO
using FishNet.Connection;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Growing;
using ScheduleOne.ItemFramework;
using ScheduleOne.Management;
using ScheduleOne.Persistence;
using ScheduleOne.Product;
using ScheduleOne.UI.MainMenu;
using GenericCol = System.Collections.Generic;
#endif

namespace UnicornsCustomSeeds.Patches
{
    [HarmonyPatch(typeof(LoadManager), nameof(LoadManager.ExitToMenu))]
    public static class LoadManager_ExitToMenu_Patch
    {
        public static void Postfix(LoadManager __instance, SaveInfo autoLoadSave, MainMenuPopup.Data mainMenuPopup, bool preventLeaveLobby)
        {
            var seedList = Singleton<ManagementUtilities>.Instance.Seeds;
            var seedListClean = new GenericCol.List<SeedDefinition>();
            foreach (var seed in seedList)
            {
                if (seed != null && !seed.ID.Contains("customseeddefinition"))
                    seedListClean.Add(seed);
            }
            Singleton<ManagementUtilities>.Instance.Seeds = seedListClean;
        }
    }

    [HarmonyPatch(typeof(LoadManager), nameof(LoadManager.StartGame))]
    public static class LoadManager_StartGame_Patch
    {
        public static void Postfix(LoadManager __instance)
        {
            if (__instance == null || string.IsNullOrEmpty(__instance.LoadedGameFolderPath)) return;

            try
            {
                string saveFolder = __instance.LoadedGameFolderPath;

                // ── DiscoveredCustomSeeds.json ────────────────────────────────────
                LoadDiscoveredSeeds(saveFolder);

                // ── UnicornsActiveCooking.json ────────────────────────────────────
                LoadActiveCooking(saveFolder);

                // ── UnicornsWelcomedSuppliers.json ────────────────────────────────
                LoadWelcomedSuppliers(saveFolder);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"PersistencePatches.StartGame: {ex}");
            }
        }

        private static void LoadDiscoveredSeeds(string saveFolder)
        {
            string filePath = Path.Combine(saveFolder, "DiscoveredCustomSeeds.json");
            List<UnicornSeedData> seeds = new List<UnicornSeedData>();

            if (File.Exists(filePath))
            {
                CustomSeedsManager.FirstLoad = false;
                string json = File.ReadAllText(filePath);

                try
                {
                    JArray jArray = JArray.Parse(json);

                    if (jArray.Count > 0 && jArray[0] is JObject first && !first.ContainsKey("variants"))
                    {
                        Utility.Success($"Preparing to migrate {jArray.Count} legacy seed(s).");
                        seeds = MigrateLegacySeedData(json, first);
                    }
                    else
                    {
                        CustomSeedsManager.letsMigrate = false;
                        seeds = JsonConvert.DeserializeObject<List<UnicornSeedData>>(json) ?? new List<UnicornSeedData>();
                    }

                    Utility.Success($"Loaded {seeds.Count} custom seeds.");
                }
                catch (Exception ex)
                {
                    Utility.PrintException(ex);
                    CustomSeedsManager.FirstLoad = true;
                    seeds = new List<UnicornSeedData>();
                }
            }
            else
            {
                CustomSeedsManager.FirstLoad = true;
            }

            // Dispatch each seed to the correct manager by drug type
            foreach (UnicornSeedData data in seeds)
            {
                if (data == null) continue;

                switch (data.drugType)
                {
                    case EDrugType.Marijuana:
                        if (!CustomSeedsManager.DiscoveredSeeds.ContainsKey(data.mixId))
                            CustomSeedsManager.DiscoveredSeeds.Add(data.mixId, data);
                        break;

                    case EDrugType.Shrooms:
                        if (!CustomShroomsManager.DiscoveredShrooms.ContainsKey(data.mixId))
                            CustomShroomsManager.DiscoveredShrooms.Add(data.mixId, data);
                        break;

                    case EDrugType.Cocaine:
                        if (!CustomCocaSeedsManager.DiscoveredCocaSeeds.ContainsKey(data.mixId))
                            CustomCocaSeedsManager.DiscoveredCocaSeeds.Add(data.mixId, data);
                        break;

                    case EDrugType.Methamphetamine:
                        if (!CustomPseudoManager.DiscoveredPseudoSeeds.ContainsKey(data.mixId))
                            CustomPseudoManager.DiscoveredPseudoSeeds.Add(data.mixId, data);
                        break;

                    default:
                        Utility.Error($"PersistencePatches: Unknown EDrugType '{data.drugType}' for seed '{data.seedId}' — skipped.");
                        break;
                }
            }
        }

        private static List<UnicornSeedData> MigrateLegacySeedData(string json, JObject first)
        {
            var seeds = new List<UnicornSeedData>();

            if (first.ContainsKey("weedId"))
            {
                CustomSeedsManager.letsMigrate = true;
                var legacy = JsonConvert.DeserializeObject<List<LegacySeedData>>(json) ?? new List<LegacySeedData>();
                foreach (var l in legacy)
                {
                    var data = new UnicornSeedData
                    {
                        mixId = l.weedId,
                        drugType = EDrugType.Marijuana,
                    };
                    data.SetSingleVariant(CustomSeedsManager.BASE_SEED_ID, l.seedId, l.price);
                    seeds.Add(data);
                }

                return seeds;
            }

            // This path is part of migration, therefore letsMigrate needs to be true
            CustomSeedsManager.letsMigrate = true;
            var legacyCurrent = JsonConvert.DeserializeObject<List<LegacyUnicornSeedData>>(json) ?? new List<LegacyUnicornSeedData>();
            foreach (var l in legacyCurrent)
            {
                var data = new UnicornSeedData
                {
                    mixId = l.mixId,
                    drugType = l.drugType,
                };

                string baseItemId = ResolveLegacyBaseItemId(l);
                data.SetSingleVariant(baseItemId, l.seedId, l.price);
                seeds.Add(data);
            }

            return seeds;
        }

        private static string ResolveLegacyBaseItemId(LegacyUnicornSeedData data)
        {
            switch (data.drugType)
            {
                case EDrugType.Marijuana:
                    return CustomSeedsManager.BASE_SEED_ID;
                case EDrugType.Shrooms:
                    return CustomShroomsManager.BASE_SYRINGE_ID;
                case EDrugType.Cocaine:
                    return CustomCocaSeedsManager.BASE_SEED_ID;
                case EDrugType.Methamphetamine:
                    return CustomPseudoManager.InferPseudoBaseIdFromSeedId(data.seedId);
                default:
                    return string.Empty;
            }
        }

        private static void LoadActiveCooking(string saveFolder)
        {
            string filePath = Path.Combine(saveFolder, "UnicornsActiveCooking.json");
            if (!File.Exists(filePath)) return;

            try
            {
                string json = File.ReadAllText(filePath);
                var entries = JsonConvert.DeserializeObject<List<ActiveCookingEntry>>(json);
                if (entries == null) return;

                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry?.stationGuid) && !string.IsNullOrEmpty(entry.mixId))
                        ActiveCookingRegistry.Register(entry.stationGuid, entry.mixId);
                }

                Utility.Success($"Loaded {entries.Count} active cooking entries.");
            }
            catch (Exception ex)
            {
                Utility.PrintException(ex);
            }
        }

        private static void LoadWelcomedSuppliers(string saveFolder)
        {
            string filePath = Path.Combine(saveFolder, "UnicornsWelcomedSuppliers.json");
            if (!File.Exists(filePath)) return;

            try
            {
                string json = File.ReadAllText(filePath);
                var names = JsonConvert.DeserializeObject<List<string>>(json);
                if (names == null) return;

                foreach (string name in names)
                {
                    if (!string.IsNullOrEmpty(name))
                        WelcomedSuppliersRegistry.MarkWelcomed(name);
                }

                Utility.Success($"Loaded {names.Count} welcomed supplier(s).");
            }
            catch (Exception ex)
            {
                Utility.PrintException(ex);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch: ProductManager.CreateWeed — re-create weed seeds after load
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ProductManager), nameof(ProductManager.CreateWeed))]
    public static class Patch_ProductManager_CreateWeed
    {
        public static void Postfix(
            NetworkConnection conn, string name, string id,
     EDrugType type, List<string> properties, WeedAppearanceSettings appearance)
        {
            if (Registry.ItemExists(id + "_customseeddefinition")) return;

            if (CustomSeedsManager.DiscoveredSeeds.TryGetValue(id, out var parts))
            {
                SeedDefinition newSeed = CustomSeedsManager.SeedDefinitionLoader(parts);
                if (newSeed != null)
                {
                    try { Singleton<ManagementUtilities>.Instance.Seeds.Add(newSeed); }
                    catch (Exception ex) { Utility.PrintException(ex); }
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch: ProductManager.CreateCocaine — re-create coca seeds after load
    //
    // Fires whenever a cocaine mix is registered (on load for each existing mix).
    // If DiscoveredCocaSeeds has an entry for this mix ID and the seed is not
    // yet in the Registry, run CocaFactory to rebuild the full chain and
    // register it, then add the leaf to cauldron filters.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ProductManager), nameof(ProductManager.CreateCocaine))]
    public static class Patch_ProductManager_CreateCocaine
    {
        public static void Postfix(
         NetworkConnection conn, string name, string id,
  EDrugType type, List<string> properties, CocaineAppearanceSettings appearance)
        {
            if (Registry.ItemExists(id + "_customcocaseed")) return;
            if (!CustomCocaSeedsManager.DiscoveredCocaSeeds.TryGetValue(id, out var _)) return;

            if (CustomCocaSeedsManager.factory == null)
            {
                Utility.Error($"Patch_ProductManager_CreateCocaine: factory is null for '{id}'.");
                return;
            }

            try
            {
                var cocaineDef = Registry.GetItem<ProductDefinition>(id);
                if (cocaineDef == null)
                {
                    Utility.Error($"Patch_ProductManager_CreateCocaine: Could not resolve ProductDefinition '{id}'.");
                    return;
                }

                SeedDefinition newSeed = CustomCocaSeedsManager.factory.CreateCocaSeedDefinition(cocaineDef);
                if (newSeed == null) return;

                Singleton<Registry>.Instance.AddToRegistry(newSeed);

                try { Singleton<ManagementUtilities>.Instance.Seeds.Add(newSeed); }
                catch (Exception ex) { Utility.PrintException(ex); }

                // AddLeafToCauldrons must be called after the leaf is registered so all
                // cauldrons in the scene accept the custom leaf in their ingredient slot filters.
                // CreateCocaSeedDefinition registers the leaf internally, so it is available here.
                string leafId = $"{id}_customcocaleaf";
                var customLeaf = Registry.GetItem<QualityItemDefinition>(leafId);
                if (customLeaf != null)
                    CocaFactory.AddLeafToCauldrons(customLeaf);
                else
                    Utility.Error($"Patch_ProductManager_CreateCocaine: Could not resolve leaf '{leafId}' from Registry — cauldrons will not accept this leaf.");

                Utility.Log($"Patch_ProductManager_CreateCocaine: Reloaded coca seed '{newSeed.ID}'.");
            }
            catch (Exception ex) { Utility.PrintException(ex); }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch: ProductManager.CreateShroom_Server — re-create syringe chains after load
    //
    // Fires whenever a shroom mix is registered during the load replay.
    // DiscoveredShrooms is already populated by LoadDiscoveredSeeds at this
    // point. If the mix is in DiscoveredShrooms and the syringe is not yet in
    // the Registry, rebuild the full syringe + spawn + colony chain.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ProductManager), nameof(ProductManager.CreateShroom_Server))]
    public static class Patch_ProductManager_CreateShroom
    {
        public static void Postfix(
            string name, string id,
            EDrugType type, List<string> properties, ShroomAppearanceSettings appearance)
        {
            string syringeId = id + "_customsyringedefinition";
            if (Registry.ItemExists(syringeId)) return;
            if (!CustomShroomsManager.DiscoveredShrooms.TryGetValue(id, out var data)) return;

            if (CustomShroomsManager.factory == null)
            {
                Utility.Error($"Patch_ProductManager_CreateShroom: factory is null for '{id}'.");
                return;
            }

            try
            {
                SporeSyringeDefinition newSyringe = CustomShroomsManager.SyringeDefinitionLoader(data);
                if (newSyringe != null)
                    Utility.Log($"Patch_ProductManager_CreateShroom: Reloaded syringe '{newSyringe.ID}'.");
            }
            catch (Exception ex) { Utility.PrintException(ex); }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch: ProductManager.CreateMeth — re-create pseudo chains after load
    //
    // Fires when ProductManager registers a meth product during the load replay.
    // DiscoveredPseudoSeeds is already populated by LoadDiscoveredSeeds at this
    // point (StartGame Postfix runs before the product replay).
    // If the mix is in DiscoveredPseudoSeeds and the pseudo is not yet in the
    // Registry, rebuild the full chain via PseudoFactory.
    //
    // NOTE: Chemistry stations are not yet spawned when this fires, so filter
    // and recipe restoration is deferred to RestorePseudoFilters() which runs
    // in CustomPseudoManager.Initialize() on onLoadComplete.
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(ProductManager), "CreateMeth")]
    public static class Patch_ProductManager_CreateMeth
    {
        public static void Postfix(
            NetworkConnection conn, string name, string id,
            EDrugType type, List<string> properties, MethAppearanceSettings appearance)
        {
            if (!CustomPseudoManager.DiscoveredPseudoSeeds.TryGetValue(id, out var data)) return;

            if (CustomPseudoManager.factory == null)
            {
                // PseudoFactory is initialized slightly later from the main-scene coroutine.
                // RestorePseudoFilters() on onLoadComplete will rebuild the pseudo variants.
                return;
            }

            try
            {
#if IL2CPP
                MethDefinition methDef = Registry.GetItem<ProductDefinition>(id)?.TryCast<MethDefinition>();
#elif MONO
                MethDefinition methDef = Registry.GetItem<MethDefinition>(id);
#endif
                if (methDef == null)
                {
                    Utility.Error($"Patch_ProductManager_CreateMeth: Could not resolve MethDefinition '{id}'.");
                    return;
                }

                foreach (var variant in data.variants)
                {
                    if (Registry.ItemExists(variant.seedId))
                        continue;

                    CustomPseudoManager.factory.CreatePseudoChain(methDef, variant.baseItemId);
                }

                Utility.Log($"Patch_ProductManager_CreateMeth: Rebuilt pseudo chain for '{id}'.");
            }
            catch (Exception ex) { Utility.PrintException(ex); }
        }
    }
}
