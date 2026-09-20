using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.TemplateUtils;


#if IL2CPP
using Il2Cpp;
using Il2CppFishNet;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.UI.Shop;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.UI.Phone.Delivery;
#elif MONO
using FishNet;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Management;
using ScheduleOne.Messaging;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.ObjectScripts;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Product;
using ScheduleOne.Quests;
using ScheduleOne.StationFramework;
using ScheduleOne.UI.Shop;
using ScheduleOne.UI.Phone;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.UI.Phone.Delivery;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class CustomShroomsManager
    {
        public const string BASE_SYRINGE_ID = "sporesyringe";

        public static SyringeFactory factory;
        public static Dictionary<string, UnicornSeedData> DiscoveredShrooms = new();

        public static ShopInterface PhilShop = null;
        public static GameObject PhilShopGo = null;
        public static Phil phil = null;

        /// <summary>
        /// See CustomSeedsManager.Initialize — this runs on the shared
        /// LoadManager.onLoadComplete UnityEvent, where an escaping exception would abort
        /// the remaining listeners and hang a client's load.
        /// </summary>
        public static void Initialize()
        {
            try { InitializeInternal(); }
            catch (Exception e)
            {
                Utility.Error("CustomShroomsManager.Initialize failed — continuing so other listeners still run.");
                Utility.PrintException(e);
            }
        }

        private static void InitializeInternal()
        {
            phil = GameObject.FindObjectOfType<Phil>();
            if (phil != null)
            {
                PhilShop = phil.Shop;
                PhilShopGo = PhilShop?.gameObject;

                if (PhilShop == null)
                    Utility.Error("CustomShroomsManager: Phil's shop is null!");

                // Register Phil's conversation
                if (phil.MSGConversation != null)
                    ConversationManager.RegisterConversation("Phil", phil.MSGConversation);

                ConversationManager.InitSupplierWelcome("Phil", phil.RelationData, phil.DialogueHandler, ConversationManager.PhilWelcomeMessage, DiscoveredShrooms.Count > 0);

                ShroomQuestManager.Init();

                // phil.MSGConversation can still be null at this point — NPC message
                // conversations aren't always created yet when LoadManager.onLoadComplete
                // fires. When that happens, ShroomQuestManager.Init() silently skips
                // creating the "Synthesize Shrooms" sendable (ConversationManager has no
                // "Phil" entry to attach it to) and Phil never offers custom syringe
                // synthesis for the rest of the session. Retry until his conversation
                // shows up.
                if (ConversationManager.GetConversation("Phil") == null)
                    MelonCoroutines.Start(WaitForPhilConversation());

                // Reload any syringes that were discovered in a previous session
                foreach (var kvp in DiscoveredShrooms)
                {
                    if (!Registry.ItemExists(kvp.Value.seedId))
                        SyringeDefinitionLoader(kvp.Value);
                    else
                    {
                        var existing = Registry.GetItem<SporeSyringeDefinition>(kvp.Value.seedId);
                        AddSyringeToSpawnStations(existing);
                        CreateShopListing(existing, kvp.Value.price);
                    }
                }
            }
            else
            {
                Utility.Error("CustomShroomsManager: Could not find Phil.");
            }
        }

        private static IEnumerator WaitForPhilConversation()
        {
            int timeoutFrames = 1800; // ~30 seconds at 60 fps — hard bail-out
            while (timeoutFrames-- > 0)
            {
                if (phil == null) yield break; // scene changed / mod cleared before Phil showed up
                if (phil.MSGConversation != null)
                {
                    ConversationManager.RegisterConversation("Phil", phil.MSGConversation);
                    ShroomQuestManager.Init(); // idempotent — re-checks quest state, creates the sendable now that convo exists
                    yield break;
                }
                yield return null;
            }
            Utility.Error("CustomShroomsManager: Timed out waiting for Phil's MSGConversation — 'Synthesize Shrooms' will not be offered this session.");
        }

				/// <summary>
        /// Rebuilds a SporeSyringeDefinition from a saved UnicornSeedData record.
        /// Called by the persistence patch after the game replays CreateShroom on load.
        /// </summary>
        public static SporeSyringeDefinition SyringeDefinitionLoader(UnicornSeedData data)
        {
            if (factory == null)
            {
                Utility.Error($"SyringeDefinitionLoader: factory is null for '{data.mixId}'.");
                return null;
            }

            ShroomDefinition shroomDef = Registry.GetItem<ShroomDefinition>(data.mixId);
            if (shroomDef == null)
            {
                Utility.Error($"SyringeDefinitionLoader: Could not resolve ShroomDefinition '{data.mixId}'.");
                return null;
            }

            SporeSyringeDefinition newSyringe = factory.CreateSyringeDefinition(shroomDef);
            if (newSyringe == null) return null;

            Singleton<Registry>.Instance.AddToRegistry(newSyringe);

            try { Singleton<ManagementUtilities>.Instance.MushroomSpawns.Add(newSyringe.SpawnDefinition); }
            catch (Exception ex) { Utility.PrintException(ex); }

            AddSyringeToSpawnStations(newSyringe);
            CreateShopListing(newSyringe, data.price);
            Utility.Log($"SyringeDefinitionLoader: Reloaded syringe '{newSyringe.ID}'.");
            return newSyringe;
        }

        public static void StartSyringeCreation(ShroomDefinition shroomDef)
        {
            MelonCoroutines.Start(CreateSyringe(shroomDef));
        }

        public static IEnumerator CreateSyringe(ShroomDefinition shroomDef)
        {
            yield return new WaitForSeconds(5f);

            if (factory == null)
            {
                Utility.Error("SyringeFactory is null!");
                yield break;
            }

            SporeSyringeDefinition newSyringe = factory.CreateSyringeDefinition(shroomDef);
            if (newSyringe == null)
            {
                Utility.Error("Failed to create custom syringe definition.");
                yield break;
            }
            Utility.Log($"Created new syringe definition: {newSyringe.ID} for shroom: {shroomDef.ID}");
            Singleton<Registry>.Instance.AddToRegistry(newSyringe);

            Singleton<ManagementUtilities>.Instance.MushroomSpawns.Add(newSyringe.SpawnDefinition);
            AddSpawnToMushroomBeds(newSyringe.SpawnDefinition);

            // Add custom syringe to all spawn stations so they accept it
            AddSyringeToSpawnStations(newSyringe);
            newSyringe.BasePurchasePrice += StashManager.GetIngredientCost(shroomDef);
            UnicornSeedData newData = new UnicornSeedData
            {
                mixId = shroomDef.ID,
                drugType = EDrugType.Shrooms,
            };
            newData.SetSingleVariant(BASE_SYRINGE_ID, newSyringe.ID, newSyringe.BasePurchasePrice);
            DiscoveredShrooms.Add(newData.mixId, newData);
            CreateShopListing(newSyringe, newData.price);

            DeadDrop randomDrop = DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position);
            if (randomDrop != null && InstanceFinder.IsServer)
            {
                ItemInstance defaultInstance = newSyringe.GetDefaultInstance();
                defaultInstance.SetQuantity(3);
                randomDrop.Storage.InsertItem(defaultInstance, true);
                string guidString = GUIDManager.GenerateUniqueGUID().ToString();
                NetworkSingleton<QuestManager>.Instance.CreateDeaddropCollectionQuest(null, randomDrop.GUID.ToString(), guidString);
                ConversationManager.SendMessage("Phil", $"{shroomDef.name} syringe synthesized and placed in a dead drop.");
            }
            else
            {
                Utility.Error("No available dead drop for syringe placement.");
            }

            NetworkSyncManager.Broadcast(newData);
        }

        /// <summary>
        /// Adds a ShroomSpawnDefinition to a single MushroomBed's Configuration.Spawn.Options,
        /// if not already present. Shared by AddSpawnToMushroomBeds (one-shot scene sweep) and
        /// AddKnownSpawnsToBed (per-instance, called from MushroomBed.Awake).
        /// </summary>
        private static bool AddSpawnToBed(MushroomBed bed, ShroomSpawnDefinition spawn)
        {
#if IL2CPP
            if (!(bed.Configuration.TryCast<MushroomBedConfiguration>() is MushroomBedConfiguration config))
                return false;
#elif MONO
            if (!(bed.Configuration is MushroomBedConfiguration config))
                return false;
#endif
            if (config.Spawn.Options.Contains(spawn)) return false;

            config.Spawn.Options.Add(spawn);
            return true;
        }

        /// <summary>
        /// Pushes a new ShroomSpawnDefinition into every MushroomBed already present in the
        /// scene, mirroring CustomSeedsManager.AddSeedToPots. MushroomBedConfiguration.Spawn.Options
        /// is only populated from ManagementUtilities.MushroomSpawns when a bed's config first
        /// initializes, so beds already spawned need the new spawn definition pushed directly.
        ///
        /// This only reaches beds that already exist at call time — any bed created afterwards
        /// (placed by the player, rebuilt on a network client, recreated when a save is loaded,
        /// etc.) is caught by AddKnownSpawnsToBed instead, via the MushroomBed.Awake patch.
        /// </summary>
        public static void AddSpawnToMushroomBeds(ShroomSpawnDefinition newSpawn)
        {
            var beds = GameObject.FindObjectsOfType<MushroomBed>();
            foreach (MushroomBed bed in beds)
                AddSpawnToBed(bed, newSpawn);
        }

        /// <summary>
        /// Adds every currently-known custom mushroom spawn to a single MushroomBed's
        /// Configuration.Spawn.Options. Called from MushroomBedStartPatch so a bed always has
        /// the full, up-to-date option list the moment it exists — without this, a bed that
        /// only came into existence after the last sweep has no valid spawn configured, and
        /// employees never treat it as ready to work.
        /// </summary>
        public static void AddKnownSpawnsToBed(MushroomBed bed)
        {
            if (bed == null) return;

            int patched = 0;
            foreach (UnicornSeedData data in DiscoveredShrooms.Values)
            {
                string syringeId = data?.seedId;
                if (string.IsNullOrEmpty(syringeId)) continue;

                SporeSyringeDefinition syringe = Registry.GetItem<SporeSyringeDefinition>(syringeId);
                if (syringe?.SpawnDefinition == null) continue;

                if (AddSpawnToBed(bed, syringe.SpawnDefinition)) patched++;
            }

            if (patched > 0)
                Utility.Log($"CustomShroomsManager: Whitelisted {patched} custom spawn(s) on bed '{bed.name}' at Awake.");
        }

        /// <summary>
        /// Adds a custom syringe ID to a single MushroomSpawnStation's SyringeSlot
        /// ItemFilter_ID whitelist, if not already present. Shared by AddSyringeToSpawnStations
        /// (one-shot scene sweep) and AddKnownSyringesToSpawnStation (per-instance, called from
        /// MushroomSpawnStation.Awake).
        /// </summary>
        private static bool AddSyringeIdToStation(MushroomSpawnStation station, string syringeId)
        {
            if (station.SyringeSlot == null) return false;

            bool patched = false;
            foreach (var filter in station.SyringeSlot.HardFilters)
            {
#if IL2CPP
                ItemFilter_ID idFilter = filter.TryCast<ItemFilter_ID>();
#elif MONO
                ItemFilter_ID idFilter = filter as ItemFilter_ID;
#endif
                if (idFilter != null && !idFilter.IDs.Contains(syringeId))
                {
                    idFilter.IDs.Add(syringeId);
                    patched = true;
                }
            }
            return patched;
        }

        /// <summary>
        /// This only reaches stations that already exist at call time — any station created
        /// afterwards is caught by AddKnownSyringesToSpawnStation instead, via the
        /// MushroomSpawnStation.Awake patch.
        /// </summary>
        public static void AddSyringeToSpawnStations(SporeSyringeDefinition newSyringe)
        {
            var stations = GameObject.FindObjectsOfType<MushroomSpawnStation>();
            foreach (MushroomSpawnStation station in stations)
                AddSyringeIdToStation(station, newSyringe.ID);
        }

        /// <summary>
        /// Adds every currently-known custom syringe ID to a single MushroomSpawnStation's
        /// SyringeSlot filters. Called from MushroomSpawnStationStartPatch so a station always
        /// has the full, up-to-date whitelist the moment it exists, mirroring
        /// CocaFactory.AddKnownLeavesToCauldron / CustomSeedsManager.AddKnownSeedsToPot.
        /// </summary>
        public static void AddKnownSyringesToSpawnStation(MushroomSpawnStation station)
        {
            if (station == null) return;

            int patched = 0;
            foreach (UnicornSeedData data in DiscoveredShrooms.Values)
            {
                string syringeId = data?.seedId;
                if (string.IsNullOrEmpty(syringeId)) continue;

                if (AddSyringeIdToStation(station, syringeId)) patched++;
            }

            if (patched > 0)
                Utility.Log($"CustomShroomsManager: Whitelisted {patched} custom syringe(s) on spawn station '{station.name}' at Awake.");
        }

        public static void CreateShopListing(SporeSyringeDefinition newSyringe, float price = 10f)
        {
            if (PhilShop == null) return;

            ShopListing newListing = new ShopListing();
            newListing.name = $"{newSyringe.ID} (${price}) (Shrooms, )";
            newListing.Item = newSyringe;
            newListing.IconTint = new Color(0.6f, 0.2f, 0.8f, 1f);
            newListing.MinimumGameCreationVersion = 27;
            newListing.DefaultStock = 1000;
            newListing.CurrentStock = 100000;
            newListing.CanBeDelivered = true;
            PhilShop.Listings.Add(newListing);
            PhilShop.CreateListingUI(newListing);
            CreatePhoneShopListing(newSyringe);
            CreateDeliveryListing(newListing);
            PhilShop.RefreshShownItems();
        }

        public static void CreatePhoneShopListing(SporeSyringeDefinition newSyringe)
        {
            if (phil == null) return;
            PhoneShopInterface.Listing newEntry = new PhoneShopInterface.Listing(newSyringe);
            var updated = HarmonyLib.CollectionExtensions.AddItem(phil.SupplierData.DeliveryShopListings, newEntry);
            phil.SupplierData.DeliveryShopListings = updated.ToArray();
        }

        public static void CreateDeliveryListing(ShopListing newListing)
        {
            if (PhilShop == null) return;
            var deliveryShop = PlayerSingleton<DeliveryApp>.Instance?.GetShop(PhilShop.ShopName);
            if (deliveryShop == null) return;
            ListingEntry entry = UnityEngine.Object.Instantiate<ListingEntry>(deliveryShop.ListingEntryPrefab, deliveryShop.ListingContainer);
            entry.Initialize(newListing);
            entry.onQuantityChanged.AddListener((UnityEngine.Events.UnityAction)deliveryShop.RefreshCart);
            deliveryShop.listingEntries.Add(entry);
            deliveryShop.ListingContainer.sizeDelta = new Vector2(deliveryShop.ListingContainer.sizeDelta.x, 230f + (float)Math.Ceiling(deliveryShop.listingEntries.Count / 2.0) * 60f);
        }

        public static void ClearAll()
        {
            DiscoveredShrooms.Clear();
            PhilShop = null;
            PhilShopGo = null;
            phil = null;
            ShroomQuestManager.ResetSendableState();
            if (factory != null) factory.DeleteChildren();
        }
    }
}