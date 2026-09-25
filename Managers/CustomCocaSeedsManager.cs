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
using Il2CppScheduleOne.Misc;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
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
using ScheduleOne.Misc;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Product;
using ScheduleOne.Quests;
using ScheduleOne.UI.Shop;
using ScheduleOne.UI.Phone;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.UI.Phone.Delivery;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class CustomCocaSeedsManager
    {
        public const string BASE_SEED_ID = "cocaseed";
        public const string BASE_LEAF_ID = "cocaleaf";
        public const string BASE_BASE_ID = "cocainebase";

        // ?? Test mix IDs � replace placeholders with real IDs as they are discovered ??
        // OnCocaSynthesisRequested iterates this array and synthesizes the first mix
        // that does not yet have a coca seed in the Registry.
        public static readonly string[] TEST_COCAINE_MIX_IDS = new string[]
        {
            "bighaze",        // confirmed working
            "gorillaghost",  // placeholder
            "lasmegma",  // placeholder
            "hairygold",  // placeholder
            "miraclefruit",  // placeholder
            "afghancake",  // placeholder
        };

        public static CocaFactory factory;
        public static Dictionary<string, UnicornSeedData> DiscoveredCocaSeeds = new Dictionary<string, UnicornSeedData>();

        public static ShopInterface SalvadorShop = null;
        public static Salvador salvador = null;

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
                Utility.Error("CustomCocaSeedsManager.Initialize failed — continuing so other listeners still run.");
                Utility.PrintException(e);
            }
        }

        private static void InitializeInternal()
        {
            // Restore cauldron leaf filters for all previously discovered coca seeds.
            // Must run before Salvador setup so filters are in place regardless of
            // whether the player interacts with Salvador this session.
            RestoreLeafFilters();

            salvador = GameObject.FindObjectOfType<Salvador>();
            if (salvador != null)
            {
                SalvadorShop = salvador.Shop;

                if (SalvadorShop == null)
                    Utility.Error("CustomCocaSeedsManager: Salvador's shop is null!");

                if (salvador.MSGConversation != null)
                    ConversationManager.RegisterConversation("Salvador", salvador.MSGConversation);

                ConversationManager.InitSupplierWelcome("Salvador", salvador.RelationData, salvador.DialogueHandler, ConversationManager.SalvadorWelcomeMessage, DiscoveredCocaSeeds.Count > 0);

                CocaQuestManager.Init();

                // salvador.MSGConversation can still be null here — NPC message
                // conversations aren't always created yet when LoadManager.onLoadComplete
                // fires. Without this, CocaQuestManager.Init() silently skips creating the
                // "Synthesize Coca" sendable and Salvador never offers it this session.
                if (ConversationManager.GetConversation("Salvador") == null)
                    MelonCoroutines.Start(WaitForSalvadorConversation());

                foreach (var kvp in DiscoveredCocaSeeds)
                {
#if IL2CPP
                    var existingSeed = Registry.GetItem<Il2CppScheduleOne.Growing.SeedDefinition>(kvp.Value.seedId);
#elif MONO
                    var existingSeed = Registry.GetItem<ScheduleOne.Growing.SeedDefinition>(kvp.Value.seedId);
#endif
                    if (existingSeed != null)
                        CreateShopListing(existingSeed, kvp.Value.price);
                }
            }
            else
            {
                Utility.Error("CustomCocaSeedsManager: Could not find Salvador NPC in scene. Verify Salvador is present and unlocked.");
            }
        }

        private static IEnumerator WaitForSalvadorConversation()
        {
            int timeoutFrames = 1800; // ~30 seconds at 60 fps — hard bail-out
            while (timeoutFrames-- > 0)
            {
                if (salvador == null) yield break; // scene changed / mod cleared before Salvador showed up
                if (salvador.MSGConversation != null)
                {
                    ConversationManager.RegisterConversation("Salvador", salvador.MSGConversation);
                    CocaQuestManager.Init(); // idempotent — re-checks quest state, creates the sendable now that convo exists
                    yield break;
                }
                yield return null;
            }
            Utility.Error("CustomCocaSeedsManager: Timed out waiting for Salvador's MSGConversation — 'Synthesize Coca' will not be offered this session.");
        }

        public static IEnumerator CreateCocaSeed(ProductDefinition cocaineDef)
        {
            yield return new WaitForSeconds(5f);

            if (factory == null)
            {
                Utility.Error("CustomCocaSeedsManager: CocaFactory is null! Ensure factory was initialized in Core.OnSceneWasLoaded.");
                yield break;
            }

            if (DiscoveredCocaSeeds.ContainsKey(cocaineDef.ID))
            {
                Utility.Log($"CustomCocaSeedsManager: Coca seed for '{cocaineDef.ID}' already exists, skipping.");
                yield break;
            }

            var newSeed = factory.CreateCocaSeedDefinition(cocaineDef);
            if (newSeed == null)
            {
                Utility.Error("CustomCocaSeedsManager: CreateCocaSeedDefinition returned null.");
                yield break;
            }

            Singleton<Registry>.Instance.AddToRegistry(newSeed);
            Utility.Log($"CustomCocaSeedsManager: Registered seed '{newSeed.ID}' in Registry.");

            Singleton<ManagementUtilities>.Instance.Seeds.Add(newSeed);
            CustomSeedsManager.AddSeedToPots(newSeed);

            // Add the custom leaf ID to all cauldron ingredient slot filters
            // so the Cauldron UI accepts it (mirrors AddSyringeToSpawnStations pattern)
            var customLeaf = Registry.GetItem<QualityItemDefinition>($"{cocaineDef.ID}_customcocaleaf");
            if (customLeaf != null)
                CocaFactory.AddLeafToCauldrons(customLeaf);
            else
                Utility.Error("CustomCocaSeedsManager: Could not resolve custom leaf from Registry after registration.");

            newSeed.BasePurchasePrice += StashManager.GetIngredientCost(cocaineDef);
            UnicornSeedData newData = new UnicornSeedData
            {
                mixId = cocaineDef.ID,
                drugType = EDrugType.Cocaine,
            };
            newData.SetSingleVariant(BASE_SEED_ID, newSeed.ID, newSeed.BasePurchasePrice);
            DiscoveredCocaSeeds.Add(newData.mixId, newData);
            CreateShopListing(newSeed, newData.price);

            DeadDrop randomDrop = DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position);
            if (randomDrop != null && InstanceFinder.IsServer)
            {
                ItemInstance defaultInstance = newSeed.GetDefaultInstance();
                defaultInstance.SetQuantity(3);
                randomDrop.Storage.InsertItem(defaultInstance, true);

                string guidString = GUIDManager.GenerateUniqueGUID().ToString();
                NetworkSingleton<QuestManager>.Instance.CreateDeaddropCollectionQuest(null, randomDrop.GUID.ToString(), guidString);
                ConversationManager.SendMessage("Salvador", $"{cocaineDef.name} coca seed synthesized and placed in a dead drop.");
                Utility.Log($"CustomCocaSeedsManager: Placed 3x '{newSeed.ID}' in dead drop '{randomDrop.GUID}'.");
            }
            else
            {
                Utility.Error("CustomCocaSeedsManager: No available dead drop for coca seed placement, or not server.");
            }

            NetworkSyncManager.Broadcast(newData);
        }

#if IL2CPP
        public static void CreateShopListing(Il2CppScheduleOne.Growing.SeedDefinition newSeed, float price = 10f)
#elif MONO
        public static void CreateShopListing(ScheduleOne.Growing.SeedDefinition newSeed, float price = 10f)
#endif
        {
            if (SalvadorShop == null) return;

            ShopListing newListing = new ShopListing();
            newListing.name = $"{newSeed.ID} (${price}) (Coca, )";
            newListing.Item = newSeed;
            newListing.IconTint = new Color(0.9f, 0.9f, 0.9f, 1f);
            newListing.MinimumGameCreationVersion = 27;
            newListing.DefaultStock = 1000;
            newListing.CurrentStock = 100000;
            newListing.CanBeDelivered = true;
            SalvadorShop.Listings.Add(newListing);
            SalvadorShop.CreateListingUI(newListing);
            CreatePhoneShopListing(newSeed);
            CreateDeliveryListing(newListing);
            SalvadorShop.RefreshShownItems();
        }

#if IL2CPP
        public static void CreatePhoneShopListing(Il2CppScheduleOne.Growing.SeedDefinition newSeed)
#elif MONO
        public static void CreatePhoneShopListing(ScheduleOne.Growing.SeedDefinition newSeed)
#endif
        {
            if (salvador == null) return;
            PhoneShopInterface.Listing newEntry = new PhoneShopInterface.Listing(newSeed);
            var updated = HarmonyLib.CollectionExtensions.AddItem(salvador.SupplierData.DeliveryShopListings, newEntry);
            salvador.SupplierData.DeliveryShopListings = updated.ToArray();
        }

        public static void CreateDeliveryListing(ShopListing newListing)
        {
            if (SalvadorShop == null) return;
            var deliveryShop = PlayerSingleton<DeliveryApp>.Instance?.GetShop(SalvadorShop.ShopName);
            if (deliveryShop == null) return;
            ListingEntry entry = UnityEngine.Object.Instantiate<ListingEntry>(deliveryShop.ListingEntryPrefab, deliveryShop.ListingContainer);
            entry.Initialize(newListing);
            entry.onQuantityChanged.AddListener((UnityEngine.Events.UnityAction)deliveryShop.RefreshCart);
            deliveryShop.listingEntries.Add(entry);
            deliveryShop.ListingContainer.sizeDelta = new Vector2(deliveryShop.ListingContainer.sizeDelta.x, 230f + (float)Math.Ceiling(deliveryShop.listingEntries.Count / 2.0) * 60f);
        }

        public static void ClearAll()
        {
            DiscoveredCocaSeeds.Clear();
            SalvadorShop = null;
            salvador = null;
            CocaQuestManager.ResetSendableState();
            if (factory != null) factory.DeleteChildren();
        }

        /// <summary>
        /// For every entry in DiscoveredCocaSeeds:
        ///   - If the seed is not yet in the Registry (e.g. CreateCocaine fired before
        /// DiscoveredCocaSeeds was populated), rebuild the full factory chain now.
        ///   - Call AddLeafToCauldrons so every cauldron in the scene accepts the leaf.
        ///
        /// Called from Initialize() which runs on onLoadComplete, guaranteeing that the
        /// factory, cauldrons, and cocaine ProductDefinitions are all available.
        /// </summary>
        public static void RestoreLeafFilters()
        {
            if (DiscoveredCocaSeeds.Count == 0) return;

            if (factory == null)
            {
                Utility.Error("CustomCocaSeedsManager.RestoreLeafFilters: factory is null � cannot restore leaf filters.");
                return;
            }

            foreach (var kvp in DiscoveredCocaSeeds)
            {
                string mixId = kvp.Key;

                // Rebuild the full seed chain if it was not recreated by the CreateCocaine patch
                // (which silently skips when DiscoveredCocaSeeds is empty at load time).
                if (!Registry.ItemExists(mixId + "_customcocaseed"))
                {
                    var cocaineDef = Registry.GetItem<ProductDefinition>(mixId);
                    if (cocaineDef == null)
                    {
                        Utility.Error($"CustomCocaSeedsManager.RestoreLeafFilters: ProductDefinition '{mixId}' not in Registry � skipping.");
                        continue;
                    }

                    var newSeed = factory.CreateCocaSeedDefinition(cocaineDef);
                    if (newSeed == null)
                    {
                        Utility.Error($"CustomCocaSeedsManager.RestoreLeafFilters: CreateCocaSeedDefinition returned null for '{mixId}'.");
                        continue;
                    }

                    Singleton<Registry>.Instance.AddToRegistry(newSeed);
#if IL2CPP
                    CreateShopListing((Il2CppScheduleOne.Growing.SeedDefinition)(object)newSeed, kvp.Value.price);
#elif MONO
                    CreateShopListing((ScheduleOne.Growing.SeedDefinition)(object)newSeed, kvp.Value.price);
#endif
                    Utility.Log($"CustomCocaSeedsManager.RestoreLeafFilters: Rebuilt seed '{newSeed.ID}'.");
                }

                // Always re-add to cauldron filters � they are runtime objects reset each load.
                string leafId = $"{mixId}_customcocaleaf";
                var leaf = Registry.GetItem<QualityItemDefinition>(leafId);
                if (leaf != null)
                {
                    CocaFactory.AddLeafToCauldrons(leaf);
                    Utility.Log($"CustomCocaSeedsManager.RestoreLeafFilters: Added '{leafId}' to cauldron filters.");
                }
                else
                {
                    Utility.Error($"CustomCocaSeedsManager.RestoreLeafFilters: Could not resolve '{leafId}' from Registry.");
                }
            }
        }
    }
}