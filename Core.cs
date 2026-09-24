using MelonLoader;
using System.Collections;
using UnityEngine.Events;
using UnicornsCustomSeeds.Seeds;
using Newtonsoft.Json;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.Patches;
using UnicornsCustomSeeds.TemplateUtils;


#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.UI.Stations;
#elif MONO
using FishNet;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Growing;
using ScheduleOne.ItemFramework;
using ScheduleOne.ObjectScripts;
using ScheduleOne.Persistence;
using ScheduleOne.ObjectScripts;
using ScheduleOne.Product;
using ScheduleOne.StationFramework;
using ScheduleOne.UI.Stations;
#endif

[assembly: MelonInfo(typeof(UnicornsCustomSeeds.Core), UnicornsCustomSeeds.BuildInfo.Name, UnicornsCustomSeeds.BuildInfo.Version, UnicornsCustomSeeds.BuildInfo.Author, UnicornsCustomSeeds.BuildInfo.DownloadLink)]
[assembly: MelonColor(255, 191, 0, 255)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UnicornsCustomSeeds
{
    public static class BuildInfo
    {
        public const string Name = "Unicorns Custom Seeds";
        public const string Description = "Your good buddy Unicorn can help you synthesize seeds";
        public const string Author = "OverweightUnicorn";
        public const string Company = "UnicornsCanMod";
        public const string Version = "1.1.2";
        public const string DownloadLink = null;
    }

    public class Core : MelonMod
    {
        /// <summary>
        /// True once StashManager.WaitForAllStashesAndSubscribe() has finished
        /// (either all four supplier stashes were found and subscribed, or it
        /// timed out waiting). Set from StashManager, not Core, since the
        /// coroutine is what actually knows when stash lookups are done.
        /// </summary>
        public static bool ModInitialized = false;

        public override void OnInitializeMelon()
        {
            // Must run before any JsonConvert call in the mod — see SafeJsonContractResolver.cs.
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new SafeContractResolver()
            };

            AssetBundleUtils.Initialize(this);
        }

        public override void OnLateInitializeMelon()
        {
            StashManager.InitializeConfig();
            SeedVisualsManager.LoadSeedMaterial();
            LoadManager.Instance.onLoadComplete.AddListener((UnityAction)InitMod);
            SaveManager.Instance.onSaveComplete.AddListener((UnityAction)SaveData);
        }

        public void SaveData()
        {
            try
            {
                string saveFolder = Singleton<LoadManager>.Instance.LoadedGameFolderPath;
                if (string.IsNullOrEmpty(saveFolder) || !Directory.Exists(saveFolder))
                    return;

                // LoadManager_StartGame_Patch.Postfix is what repopulates DiscoveredSeeds/
                // Shrooms/CocaSeeds/PseudoSeeds, ActiveCookingRegistry and
                // WelcomedSuppliersRegistry from disk. If it never ran this session (e.g. a
                // Harmony patch elsewhere in the mod failed at PatchAll time, which aborts
                // patching for the WHOLE assembly — this has happened), all of that state is
                // just empty in-memory defaults, not "nothing to save". Writing it out would
                // silently overwrite the real, previously-saved data with empty lists.
                if (!LoadManager_StartGame_Patch.HasLoadedThisSession)
                {
                    Utility.Error("Core.SaveData: skipped writing DiscoveredCustomSeeds.json / UnicornsActiveCooking.json / UnicornsWelcomedSuppliers.json — LoadManager.StartGame's postfix never ran this session (Harmony patching likely failed), so in-memory state was never loaded from disk. Existing files left untouched.");
                    return;
                }

                // ── DiscoveredCustomSeeds.json ────────────────────────────────────
                {
                    var all = new List<UnicornSeedData>();
                    all.AddRange(CustomSeedsManager.DiscoveredSeeds.Values);
                    all.AddRange(CustomShroomsManager.DiscoveredShrooms.Values);
                    all.AddRange(CustomCocaSeedsManager.DiscoveredCocaSeeds.Values);
                    all.AddRange(CustomPseudoManager.DiscoveredPseudoSeeds.Values);

                    string json = JsonConvert.SerializeObject(all, Formatting.Indented);
                    File.WriteAllText(Path.Combine(saveFolder, "DiscoveredCustomSeeds.json"), json);
                }

                // ── UnicornsActiveCooking.json ────────────────────────────────────
                {
                    var entries = new List<UnicornsCustomSeeds.Managers.ActiveCookingEntry>();
                    foreach (var kvp in UnicornsCustomSeeds.Managers.ActiveCookingRegistry.GuidToMixId)
                        entries.Add(new UnicornsCustomSeeds.Managers.ActiveCookingEntry { stationGuid = kvp.Key, mixId = kvp.Value });

                    string json = JsonConvert.SerializeObject(entries, Formatting.Indented);
                    File.WriteAllText(Path.Combine(saveFolder, "UnicornsActiveCooking.json"), json);
                }

                // ── UnicornsWelcomedSuppliers.json ────────────────────────────────
                {
                    var welcomed = new List<string>(UnicornsCustomSeeds.Managers.WelcomedSuppliersRegistry.Welcomed);
                    string json = JsonConvert.SerializeObject(welcomed, Formatting.Indented);
                    File.WriteAllText(Path.Combine(saveFolder, "UnicornsWelcomedSuppliers.json"), json);
                }
            }
            catch (Exception e) { Utility.PrintException(e); }
        }

        public void InitMod()
        {
            // Each Initialize() contains its own try/catch — see the remarks on
            // CustomSeedsManager.Initialize. This method must never throw: it is a
            // LoadManager.onLoadComplete listener, and UnityEvent.Invoke does not isolate
            // listeners, so throwing here would abort the game's own deferred loaders and
            // hang a client on the loading screen.
            Utility.Log($"InitMod: start (IsServer={InstanceFinder.IsServer}, IsClientOnly={InstanceFinder.IsClientOnly}).");

            // Must happen before CustomPseudoManager.Initialize() (which calls
            // RestorePseudoFilters() to re-inject saved custom recipes into
            // ChemistryStationInterface.Recipes). InitMod runs on LoadManager.onLoadComplete,
            // a UnityEvent also used by the game's own ConfigurationReplicator to apply
            // deferred station field values (selected recipe, Destination) queued during load.
            // Our listener was registered once at mod boot (OnLateInitializeMelon), before any
            // scene loads, so it fires before per-load ConfigurationReplicator listeners on the
            // same event — but only if PseudoFactory (and therefore the custom recipe) already
            // exists by the time this runs. Previously PseudoFactory was built exclusively by
            // InitPseudoFactoryWhenReady(), a coroutine kicked off from OnSceneWasLoaded that
            // can still be polling when onLoadComplete fires; RestorePseudoFilters() would then
            // silently no-op (factory == null), the custom recipe would still be missing from
            // ChemistryStationInterface.Recipes when ConfigurationReplicator tried to resolve
            // the station's saved selection, and that failed lookup reset both the recipe
            // selection and the Destination field together. By main-scene time the recipe list
            // itself is normally already populated (CustomSeedsManager.Initialize() above
            // already finds scene objects like the Albert shop successfully), so this attempt
            // usually succeeds immediately; the coroutine remains as a fallback for the rare
            // case where it isn't ready yet.
            TryBuildPseudoFactory();

            CustomSeedsManager.Initialize();
            CustomShroomsManager.Initialize();
            CustomCocaSeedsManager.Initialize();
            CustomPseudoManager.Initialize();

            StashManager.GetAlbertsStash();

            if (CustomSeedsManager.letsMigrate)
            {
                bool hasData = CustomSeedsManager.DiscoveredSeeds.Count > 0
                            || CustomShroomsManager.DiscoveredShrooms.Count > 0
                            || CustomCocaSeedsManager.DiscoveredCocaSeeds.Count > 0;

                if (hasData)
                    SaveData();
                else
                    Utility.Error("Core: migration was flagged but nothing loaded — skipping the save so the existing file isn't overwritten with an empty list.");
                Utility.Success($"Successfully migrated {CustomSeedsManager.DiscoveredSeeds.Count} seed(s)");
                CustomSeedsManager.letsMigrate = false;
            }

            MelonCoroutines.Start(StashManager.WaitForAllStashesAndSubscribe());
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            var baseSeed = Registry.GetItem<SeedDefinition>("ogkushseed");
            if (CustomSeedsManager.factory == null && baseSeed != null)
            {
                CustomSeedsManager.factory = new SeedFactory(baseSeed);
            }

            var baseSyringe = Registry.GetItem<SporeSyringeDefinition>(CustomShroomsManager.BASE_SYRINGE_ID);
            if (CustomShroomsManager.factory == null && baseSyringe != null)
            {
                CustomShroomsManager.factory = new SyringeFactory(baseSyringe);
            }

            var temp = Singleton<Registry>.Instance.ItemDictionary;

            // CocaFactory — requires three base definitions to be present in the Registry
            var baseCocaSeed = Registry.GetItem<SeedDefinition>(CustomCocaSeedsManager.BASE_SEED_ID);
            var baseCocaLeaf = Registry.GetItem<QualityItemDefinition>(CustomCocaSeedsManager.BASE_LEAF_ID);
            var baseCocaBase = Registry.GetItem<QualityItemDefinition>(CustomCocaSeedsManager.BASE_BASE_ID);
            if (CustomCocaSeedsManager.factory == null && baseCocaSeed != null && baseCocaLeaf != null && baseCocaBase != null)
            {
                CustomCocaSeedsManager.factory = new CocaFactory(baseCocaSeed, baseCocaLeaf, baseCocaBase);
            }
            else if (CustomCocaSeedsManager.factory == null)
            {
                Utility.Error($"Core: CocaFactory init failed — cocaseed={baseCocaSeed != null}, cocaleaf={baseCocaLeaf != null}, cocainebase={baseCocaBase != null}");
            }

            // When returning to the main scene clear all data structures to prevent overlap with other saves
            if (sceneName.ToLower() != "main")
            {
                CustomSeedsManager.ClearAll();
                CustomShroomsManager.ClearAll();
                CustomCocaSeedsManager.ClearAll();
                CustomPseudoManager.ClearAll();
                UnicornsCustomSeeds.Managers.ActiveCookingRegistry.Clear();
                UnicornsCustomSeeds.Managers.WelcomedSuppliersRegistry.Clear();
                ProductManagerAppPatches.ClearPendingIndicators();
                StashManager.ClearCaches();
                LoadManager_StartGame_Patch.HasLoadedThisSession = false;
                ModInitialized = false;
            }
            else
            {
                // Reload assets when entering main scene to prevent garbage collection issues
                if (SeedVisualsManager.baseQuestIconSprite == null || SeedVisualsManager.baseSeedSprite == null)
                {
                    SeedVisualsManager.LoadSeedMaterial();
                }

                // PseudoFactory needs ChemistryStationCanvas.Recipes which is not populated
                // at OnSceneWasLoaded time. Poll until it is ready, then initialize. Usually
                // superseded by the synchronous TryBuildPseudoFactory() call in InitMod() (see
                // its comment), which runs later (on onLoadComplete) and typically wins the
                // race; this coroutine is the fallback for when even that is too early.
                if (CustomPseudoManager.factory == null)
                    MelonCoroutines.Start(InitPseudoFactoryWhenReady());
            }
        }

        /// <summary>
        /// Builds CustomPseudoManager.factory if it doesn't exist yet and
        /// ChemistryStationInterface.Recipes is already populated. No-op (returns false) if
        /// either the factory already exists or the recipes aren't ready — callers decide
        /// whether to retry. Shared by InitMod() (synchronous attempt on onLoadComplete) and
        /// InitPseudoFactoryWhenReady() (polling fallback from OnSceneWasLoaded).
        /// </summary>
        private bool TryBuildPseudoFactory()
        {
            if (CustomPseudoManager.factory != null) return true;

            try
            {
                bool ready = Singleton<ChemistryStationInterface>.Instance?.Recipes?.Count > 0;
                if (!ready) return false;

                var basePseudo     = Registry.GetItem<QualityItemDefinition>(CustomPseudoManager.PSEUDO_BASE_ID);
                var lowPseudo      = Registry.GetItem<QualityItemDefinition>(CustomPseudoManager.PSEUDO_LO_ID);
                var highPseudo     = Registry.GetItem<QualityItemDefinition>(CustomPseudoManager.PSEUDO_HI_ID);
                var rawLiquidMeth  = Registry.GetItem(CustomPseudoManager.BASE_LIQUIDMETH_ID);
#if IL2CPP
                LiquidMethDefinition baseLiquidMeth = rawLiquidMeth?.TryCast<LiquidMethDefinition>();
#elif MONO
                LiquidMethDefinition baseLiquidMeth = rawLiquidMeth as LiquidMethDefinition;
#endif
                StationRecipe baseRecipe = null;
                foreach (StationRecipe r in Singleton<ChemistryStationInterface>.Instance.Recipes)
                {
                    if (r.Product?.Item?.ID == CustomPseudoManager.BASE_LIQUIDMETH_ID)
                    {
                        baseRecipe = r;
                        break;
                    }
                }

                if (basePseudo != null && lowPseudo != null && highPseudo != null && baseLiquidMeth != null && baseRecipe != null)
                {
                    CustomPseudoManager.factory = new PseudoFactory(basePseudo, lowPseudo, highPseudo, baseLiquidMeth, baseRecipe);
                    Utility.Log("Core: PseudoFactory initialized.");
                    return true;
                }

                Utility.Error($"Core: PseudoFactory init failed — pseudo={basePseudo != null}, lowPseudo={lowPseudo != null}, highPseudo={highPseudo != null}, liquidmeth={baseLiquidMeth != null}, recipe={baseRecipe != null}");
                return false;
            }
            catch (Exception ex)
            {
                Utility.PrintException(ex);
                return false;
            }
        }

        private IEnumerator InitPseudoFactoryWhenReady()
        {
            // Poll once per frame until ChemistryStationCanvas has at least one recipe loaded.
            int timeoutFrames = 1800; // ~30 seconds at 60 fps — hard bail-out
            while (timeoutFrames-- > 0)
            {
                bool ready = false;
                try
                {
                    ready = Singleton<ChemistryStationInterface>.Instance?.Recipes?.Count > 0;
                }
                catch { /* singleton not initialised yet */ }

                if (ready) break;
                yield return null;
            }

            if (timeoutFrames <= 0)
            {
                Utility.Error("Core: Timed out waiting for ChemistryStationInterface.Recipes — PseudoFactory not initialized.");
                yield break;
            }

            bool built = TryBuildPseudoFactory();

            // If InitMod()/onLoadComplete already ran before the poll above finished,
            // CustomPseudoManager.RestorePseudoFilters() would have silently no-opped
            // (factory was still null) instead of re-injecting a loaded save's custom
            // recipes into ChemistryStationInterface.Recipes. Run it now as a catch-up — it's
            // a no-op if DiscoveredPseudoSeeds is empty or everything is already in place.
            // This can no longer fix a station's own saved recipe/Destination selection (the
            // game's ConfigurationReplicator already tried and failed to resolve it against
            // the still-missing recipe by the time onLoadComplete ran), but it does restore
            // the recipe's availability going forward and the station ingredient filters.
            if (built)
                CustomPseudoManager.RestorePseudoFilters();
        }
    }
}