using MelonLoader;
using Newtonsoft.Json;
using System.Collections;
using UnicornsCustomSeeds.Seeds;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnicornsCustomSeeds.TemplateUtils;


#if IL2CPP
using Il2Cpp;
using Il2CppFishNet;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Shop;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.UI.Phone.ProductManagerApp;
using Il2CppScheduleOne.UI.Phone.Delivery;
using GenericCol = Il2CppSystem.Collections.Generic;
#elif MONO
using ScheduleOne;
using FishNet;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.Growing;
using ScheduleOne.ItemFramework;
using ScheduleOne.Management;
using ScheduleOne.Messaging;
using ScheduleOne.ObjectScripts;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Product;
using ScheduleOne.Quests;
using ScheduleOne.UI.Shop;
using ScheduleOne.UI.Phone;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.UI.Phone.ProductManagerApp;
using ScheduleOne.UI.Phone.Delivery;
using GenericCol = System.Collections.Generic;
#endif

namespace UnicornsCustomSeeds.Managers
{

    public struct SeedComponents
    {
        public string seedId;
        public string baseSeedId;
    }

    public static class CustomSeedsManager
    {
        public const string BASE_SEED_ID = "ogkushseed";
        public static bool letsMigrate = false;
        public static SeedFactory factory;
        public static Dictionary<string, UnicornSeedData> DiscoveredSeeds = new();

        public static ShopInterface Shop = null;
        public static GameObject shopGo = null;

#if IL2CPP
        public static GenericCol.Dictionary<string, SeedDefinition> baseSeedDefinitions = new GenericCol.Dictionary<string, SeedDefinition>();
        public static GenericCol.Dictionary<string, ShopListing> baseShopListing = new GenericCol.Dictionary<string, ShopListing>();
        public delegate bool ValidityCheckDelegate(SendableMessage message, out Il2CppSystem.String invalidReason);
#elif MONO
        public static GenericCol.Dictionary<string, SeedDefinition> baseSeedDefinitions = new GenericCol.Dictionary<string, SeedDefinition>();
        public static GenericCol.Dictionary<string, ShopListing> baseShopListing = new GenericCol.Dictionary<string, ShopListing>();
        public delegate bool ValidityCheckDelegate(SendableMessage message, out String invalidReason);
#endif

        public static bool FirstLoad = false;
        /// <summary>
        /// Runs from LoadManager.onLoadComplete — a UnityEvent shared with the game's own
        /// deferred loaders (ConfigurationReplicator queues pending field applications onto
        /// it during load). UnityEvent.Invoke does not isolate listeners, so an exception
        /// escaping here aborts every listener registered after us. On a client that kills
        /// LoadManager's LoadRoutine and hangs the loading screen permanently, with a stack
        /// pointing at whichever unrelated listener came next. Contain failures here.
        /// </summary>
        public static void Initialize()
        {
            try { InitializeInternal(); }
            catch (Exception e)
            {
                Utility.Error("CustomSeedsManager.Initialize failed — continuing so other listeners still run.");
                Utility.PrintException(e);
            }
        }

        private static void InitializeInternal()
        {
            var shopInterfaces = UnityEngine.Object.FindObjectsOfType<ShopInterface>();
            foreach (ShopInterface shopInterface in shopInterfaces)
            {
                if (shopInterface.gameObject.name == "WeedSupplierInterface")
                {
                    shopGo = shopInterface.gameObject;
                    Shop = shopInterface;
                    break;
                }
            }

            if (Shop == null)
            {
                Utility.Error("Shop is null!");
                return;
            }

            ConversationManager.Init();
            SeedQuestManager.Init();

            SeedVisualsManager.LoadSeedMaterial();

            InitDictionary();

            AddScrollToPhoneInterface();

            foreach (var seed in DiscoveredSeeds)
            {
                SeedDefinition customDef = Registry.GetItem<SeedDefinition>(seed.Value.seedId);
                if (customDef != null)
                {
                    if (letsMigrate)
                    {
                        WeedDefinition weedDef = Registry.GetItem<WeedDefinition>(seed.Value.mixId);
                        if (weedDef != null)
                        {
                            var cost = StashManager.GetIngredientCost(weedDef);
                            if (seed.Value.variants.Count > 0)
                                seed.Value.variants[0].price = cost;
                            customDef.BasePurchasePrice = cost;
                        }
                    }
                    CreateShopListing(customDef, seed.Value.price);
                }
            }

        }

        public static void ClearAll()
        {
            SeedVisualsManager.seedIcons.Clear();
            SeedVisualsManager.appearanceMap.Clear();
            SeedQuestManager.seedDropoff = null;
            SeedQuestManager.ResetSendableState();
            DiscoveredSeeds.Clear();
            factory.DeleteChildren();
            FirstLoad = false;
            //baseSeedDefinitions.Clear();
            //baseShopListing.Clear();
        }

        public static void AddScrollToPhoneInterface()
        {
            var phoneShopTransform = PlayerSingleton<MessagesApp>.Instance.PhoneShopInterface.transform;
            Transform shopEntries = phoneShopTransform.Find("Shade/Content/Entries");
            if (shopEntries != null)
            {
                GameObject entries = shopEntries.gameObject;

                // Get the RectTransform of entries to copy dimensions
                RectTransform entriesRect = entries.GetComponent<RectTransform>();
                Image entriesImage = entries.GetComponent<Image>();
                if (entriesRect == null)
                {
                    Utility.Error("Entries does not have a RectTransform component");
                    return;
                }

                // Create new GameObject for ScrollRect
                GameObject scrollViewGO = new GameObject("ScrollView");
                scrollViewGO.transform.SetParent(entriesRect.parent, false);

                // Add RectTransform and copy dimensions from entries
                RectTransform scrollViewRect = scrollViewGO.AddComponent<RectTransform>();
                scrollViewRect.anchorMin = new Vector2(0, 1);
                scrollViewRect.anchorMax = new Vector2(1, 1);
                scrollViewRect.pivot = new Vector2(0.5f, 1);
                scrollViewRect.anchoredPosition = new Vector2(0, -140);
                scrollViewRect.sizeDelta = entriesRect.sizeDelta;

                // Add Mask component
                scrollViewGO.AddComponent<Mask>();
                entries.GetComponent<Mask>().enabled = false;

                // Add Image component (required for Mask)
                Image maskImage = scrollViewGO.AddComponent<Image>();
                maskImage.color = entriesImage.color; // Transparent

                // Add ScrollRect component
                ScrollRect scrollRect = scrollViewGO.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.inertia = true;
                scrollRect.scrollSensitivity = 20f;

                entriesRect.pivot = new Vector2(0.5f, 1);
                //entriesRect.anchorMax = new Vector2(0.5f, 1);
                //entriesRect.anchorMin = new Vector2(0,0.5f);

                var temp = entries.AddComponent<ContentSizeFitter>();
                if (temp != null)
                {
                    temp.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
                VerticalLayoutGroup layoutGroup = entries.GetComponent<VerticalLayoutGroup>();
                layoutGroup.childAlignment = TextAnchor.UpperCenter;
                layoutGroup.spacing = 20;
                layoutGroup.childForceExpandHeight = false;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childControlHeight = false;
                layoutGroup.childControlWidth = false;
                layoutGroup.padding.top = 20;
                layoutGroup.padding.bottom = 20;
                // Reparent entries to be child of ScrollView
                entries.transform.SetParent(scrollViewGO.transform, false);

                // Set entries as the content of the ScrollRect
                scrollRect.content = entriesRect;

                // Reset entries position to top
                entriesRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                Utility.Error("Could not find Shade/Content/Entries in phone shop interface");
            }
        }

        public static void StartSeedCreation(WeedDefinition weedDef)
        {
            MelonCoroutines.Start(CreateSeed(weedDef));
        }

        public static void BroadcastCustomSeed(UnicornSeedData seed)
        {
            ProductManager prodManager = NetworkSingleton<ProductManager>.Instance;
            string json = JsonConvert.SerializeObject(seed);
            string payload = "[NET-JSON]" + json;

            var props = new GenericCol.List<string>();
            var appearance = new WeedAppearanceSettings(
                prodManager.DefaultWeed.MainMat.color,
                prodManager.DefaultWeed.SecondaryMat.color,
                prodManager.DefaultWeed.LeafMat.color,
                prodManager.DefaultWeed.StemMat.color);

            prodManager.CreateWeed_Server(payload, BASE_SEED_ID,
                                             EDrugType.Marijuana, props, appearance);
        }

        public static IEnumerator CreateSeed(WeedDefinition weedDef)
        {
            yield return new WaitForSeconds(StashManager.SynthesizeTime.Value);

            var newSeed = factory.CreateSeedDefinition(weedDef);
            float price = StashManager.GetIngredientCost(weedDef);
            newSeed.BasePurchasePrice = price;

            Singleton<Registry>.Instance.AddToRegistry(newSeed);
            UnicornSeedData newSeedData = new UnicornSeedData
            {
                mixId = weedDef.ID,
                drugType = EDrugType.Marijuana,
            };
            newSeedData.SetSingleVariant(BASE_SEED_ID, newSeed.ID, price);
            DiscoveredSeeds.Add(newSeedData.mixId, newSeedData);
            CreateShopListing(newSeed, price);
            AddSeedToPots(newSeed);
            EnableSeedIndicator(newSeedData.mixId);

            DeadDrop randomEmptyDrop = DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position);
            if (randomEmptyDrop != null && InstanceFinder.IsServer)
            {
                ItemInstance defaultInstance = newSeed.GetDefaultInstance();
                defaultInstance.SetQuantity(10);
                randomEmptyDrop.Storage.InsertItem(defaultInstance, true);
                string guidString = GUIDManager.GenerateUniqueGUID().ToString();
                NetworkSingleton<QuestManager>.Instance.CreateDeaddropCollectionQuest(null, randomEmptyDrop.GUID.ToString(), guidString);
                SeedQuestManager.SendMessage($"{weedDef.name} is synthesized and placed in the deaddrop");
            }
            else
            {
                if (randomEmptyDrop == null)
                    Utility.Error("No Dead Drop found");
                SeedQuestManager.SendMessage($"Wasn't able to find a deadrop for {weedDef.name} Seed. It is synthesized and available in the shop");
            }
            BroadcastCustomSeed(newSeedData);

        }



        public static void InitDictionary()
        {
            var listings = Shop.Listings;
            foreach (ShopListing listing in listings)
            {
#if IL2CPP
                if (listing.Item.TryCast<SeedDefinition>() is SeedDefinition seed)
#elif MONO
                if (listing.Item is SeedDefinition seed)
#endif
                {
                    if (!baseSeedDefinitions.ContainsKey(seed.ID))
                    {
                        baseSeedDefinitions.Add(seed.ID, seed);
                        baseShopListing.Add(seed.ID, listing);
                    }
                }
                else
                {
                    MelonLogger.Warning($"{listing.name} could not be cast to SeedDefinition");
                }
            }
        }



        public static void CreateDeliveryListing(ShopListing newListing)
        {
            var albertDeliveryShop = PlayerSingleton<DeliveryApp>.Instance?.GetShop("Albert Hoover");
            if (albertDeliveryShop == null)
            {
                //Utility.Log("Albert delivery shop not ready, skipping delivery listing");
                return;
            }
            ListingEntry listingEntry = UnityEngine.Object.Instantiate<ListingEntry>(albertDeliveryShop.ListingEntryPrefab, albertDeliveryShop.ListingContainer);
            listingEntry.Initialize(newListing);
            listingEntry.onQuantityChanged.AddListener((UnityAction)albertDeliveryShop.RefreshCart);
            albertDeliveryShop.listingEntries.Add(listingEntry);
            albertDeliveryShop.ListingContainer.sizeDelta = new Vector2(albertDeliveryShop.ListingContainer.sizeDelta.x, 230f + (float)Math.Ceiling(albertDeliveryShop.listingEntries.Count / 2.0) * 60f);
        }

        public static void CreatePhoneShopListing(SeedDefinition customDef)
        {
            if (ConversationManager.albert != null)
            {
                PhoneShopInterface.Listing newSeed = new PhoneShopInterface.Listing(customDef);
                var updatedItems = HarmonyLib.CollectionExtensions.AddItem(ConversationManager.albert.SupplierData.DeliveryShopListings, newSeed);
                ConversationManager.albert.SupplierData.DeliveryShopListings = updatedItems.ToArray();
            }
        }

        public static void CreateShopListing(SeedDefinition newSeed, float price = 10)
        {
            if (Shop == null)
            {
                return;
            }
            ShopListing baseListing = Shop.Listings[0];
            baseListing.name = "Test";
            ShopListing newListing = new ShopListing();
            newListing.name = $"{newSeed.ID} (${price}) (Agriculture, )";
            newListing.Item = newSeed;
            newListing.IconTint = new Color(0, 0.859f, 1, 1);
            newListing.MinimumGameCreationVersion = 27;
            newListing.DefaultStock = 1000;
            newListing.CurrentStock = 100000;
            newListing.CanBeDelivered = true;
            Shop.Listings.Add(newListing);
            Shop.CreateListingUI(newListing);
            CreatePhoneShopListing(newSeed);
            CreateDeliveryListing(newListing);
            Shop.RefreshShownItems();
        }

        public static void EnableSeedIndicator(string weedId)
        {
            ProductDefinition prodDef = Registry.GetItem<ProductDefinition>(weedId);
            if (prodDef == null)
            {
                Utility.Error($"Could not find ProductDefinition for weedId: {weedId}");
                return;
            }

            var app = PlayerSingleton<ProductManagerApp>.instance;
            if (app == null)
            {
                Utility.Error("ProductManagerApp instance is null");
                return;
            }

            bool isFavourited = ProductManager.FavouritedProducts.Contains(prodDef);

            // Search in regular entries
            if (app.entries != null)
            {
                for (int i = 0; i < app.entries.Count; i++)
                {
                    ProductEntry entry = app.entries[i];
                    if (entry != null && entry.Definition != null && entry.Definition.ID == weedId)
                    {
                        Transform seedIndicator = entry.transform.Find("SeedIndicator");
                        if (seedIndicator != null)
                        {
                            seedIndicator.gameObject.SetActive(true);
                        }
                        break;
                    }
                }
            }

            // Search in favourite entries if the product is favourited
            if (isFavourited && app.favouriteEntries != null)
            {
                for (int i = 0; i < app.favouriteEntries.Count; i++)
                {
                    ProductEntry entry = app.favouriteEntries[i];
                    if (entry != null && entry.Definition != null && entry.Definition.ID == weedId)
                    {
                        Transform seedIndicator = entry.transform.Find("SeedIndicator");
                        if (seedIndicator != null)
                        {
                            seedIndicator.gameObject.SetActive(true);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Adds a custom SeedDefinition to a single Pot's Configuration.Seed.Options list,
        /// if not already present. Shared by AddSeedToPots (one-shot scene sweep) and
        /// AddKnownSeedsToPot (per-instance, called from Pot.Start).
        /// </summary>
        private static bool AddSeedToPot(Pot pot, SeedDefinition seed)
        {
#if IL2CPP
            if (!(pot.Configuration.TryCast<PotConfiguration>() is PotConfiguration config))
                return false;
#elif MONO
            if (!(pot.Configuration is PotConfiguration config))
                return false;
#endif
            if (config.Seed.Options.Contains(seed)) return false;

            config.Seed.Options.Add(seed);
            return true;
        }

        /// <summary>
        /// Adds a custom SeedDefinition to every Pot currently in the scene's
        /// Configuration.Seed.Options list. Call this after the seed has been registered.
        ///
        /// This only reaches Pots that already exist at call time — any Pot created
        /// afterwards (placed by the player, rebuilt on a network client, recreated when a
        /// save is loaded, etc.) is caught by AddKnownSeedsToPot instead, via the Pot.Start
        /// patch — without it, botanists treat such a pot as having no valid seed configured
        /// and never fetch matching items from storage, appearing to "not recognize" them.
        /// </summary>
        public static void AddSeedToPots(SeedDefinition newSeed)
        {
            var pots = GameObject.FindObjectsOfType<Pot>();
            foreach (Pot pot in pots)
                AddSeedToPot(pot, newSeed);
        }

        /// <summary>
        /// Adds every currently-known custom SeedDefinition — both weed seeds and coca
        /// seeds, since both are planted in Pots — to a single Pot's Configuration.Seed.Options.
        /// Called from PotStartPatch so a Pot always has the full, up-to-date option list the
        /// moment it exists, regardless of whether it was already in the scene when
        /// AddSeedToPots last ran (see that method's remarks).
        /// </summary>
        public static void AddKnownSeedsToPot(Pot pot)
        {
            if (pot == null) return;

            int patched = 0;
            patched += AddDiscoveredSeedsToPot(pot, DiscoveredSeeds.Values);
            patched += AddDiscoveredSeedsToPot(pot, CustomCocaSeedsManager.DiscoveredCocaSeeds.Values);

            if (patched > 0)
                Utility.Log($"CustomSeedsManager: Whitelisted {patched} custom seed(s) on pot '{pot.name}' at Start.");
        }

        private static int AddDiscoveredSeedsToPot(Pot pot, IEnumerable<UnicornSeedData> discovered)
        {
            int patched = 0;
            foreach (UnicornSeedData data in discovered)
            {
                string seedId = data?.seedId;
                if (string.IsNullOrEmpty(seedId)) continue;

                SeedDefinition seed = Registry.GetItem<SeedDefinition>(seedId);
                if (seed == null) continue;

                if (AddSeedToPot(pot, seed)) patched++;
            }
            return patched;
        }

        public static void SeedFactoryLoader()
        {
            var baseSeed = Registry.GetItem<SeedDefinition>("ogkushseed");
            if (baseSeed != null)
            {
                factory = new SeedFactory(baseSeed);
            }
        }

        public static SeedDefinition SeedDefinitionLoader(UnicornSeedData newSeedData)
        {
            SeedDefinition newSeed = null;
            WeedDefinition weedDef = Registry.GetItem<WeedDefinition>(newSeedData.mixId);


            if (weedDef != null)
            {

                newSeed = factory.CreateSeedDefinition(weedDef);
                if (newSeed == null)
                {
                    Utility.Error("New seed not created");
                }
                newSeed.BasePurchasePrice = newSeedData.price;
                Singleton<Registry>.Instance.AddToRegistry(newSeed);
            }
            else
            {
                MelonLogger.Error("Could not parse weed and seed defitions");
            }

            return newSeed;
        }
    }
}