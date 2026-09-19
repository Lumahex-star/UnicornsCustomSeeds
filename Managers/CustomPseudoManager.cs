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
using ScheduleOne.ItemFramework;
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
    public static class CustomPseudoManager
    {
        
        // 
        public const string PSEUDO_BASE_ID     = "pseudo";
        public const string PSEUDO_HI_ID     = "highqualitypseudo";
        public const string PSEUDO_LO_ID     = "lowqualitypseudo";
        public const string BASE_LIQUIDMETH_ID = "liquidmeth";

        public static PseudoFactory factory;
        public static Dictionary<string, UnicornSeedData> DiscoveredPseudoSeeds
            = new Dictionary<string, UnicornSeedData>();

        public static ShopInterface ShirleyShop = null;
        public static Shirley shirley = null;

        // ─────────────────────────────────────────────────────────────────────
        // Initialize — called from InitMod on onLoadComplete.
        //
        // RestorePseudoFilters runs first so station filters and canvas recipes
        // are in place before the player can interact with any chemistry station.
        // ─────────────────────────────────────────────────────────────────────
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
                Utility.Error("CustomPseudoManager.Initialize failed — continuing so other listeners still run.");
                Utility.PrintException(e);
            }
        }

        private static void InitializeInternal()
        {
            RestorePseudoFilters();

            shirley = GameObject.FindObjectOfType<Shirley>();
            if (shirley != null)
            {
                if (shirley.MSGConversation != null)
                    ConversationManager.RegisterConversation("Shirley", shirley.MSGConversation);

                ConversationManager.InitSupplierWelcome("Shirley", shirley.RelationData, shirley.DialogueHandler, ConversationManager.ShirleyWelcomeMessage, DiscoveredPseudoSeeds.Count > 0);

                ShirleyShop = shirley.Shop;
                if (ShirleyShop == null)
                    Utility.Error("CustomPseudoManager: Shirley's shop is null!");

                PseudoQuestManager.Init();

                foreach (var kvp in DiscoveredPseudoSeeds)
                {
                    foreach (var variant in kvp.Value.variants)
                    {
                        var existingPseudo = Registry.GetItem<QualityItemDefinition>(variant.seedId);
                        if (existingPseudo != null)
                            CreateShopListing(existingPseudo, variant.price);
                    }
                }
            }
            else
            {
                Utility.Error("CustomPseudoManager: Could not find Shirley NPC in scene. Verify she is present and unlocked.");
            }
        }

        public static IEnumerator CreatePseudoChain(MethDefinition methDef, EQuality quality)
        {
            yield return new WaitForSeconds(5f);

            if (factory == null)
            {
                Utility.Error("CustomPseudoManager: PseudoFactory is null! Ensure factory was initialized in Core.OnSceneWasLoaded.");
                yield break;
            }

            if (DiscoveredPseudoSeeds.ContainsKey(methDef.ID))
            {
                Utility.Log($"CustomPseudoManager: Pseudo chain for '{methDef.ID}' already exists — skipping.");
                yield break;
            }

            UnicornSeedData newData = new UnicornSeedData
            {
                mixId = methDef.ID,
                drugType = EDrugType.Methamphetamine,
            };

            // Reserve the key NOW, not after the loop. The ContainsKey guard above and the
            // dictionary write used to sit in the same frame with no yield between them, so
            // check-then-act was atomic. Frame-spreading opened a multi-hundred-ms window
            // between them, letting a second CreatePseudoChain for the same mix pass the
            // guard before the first one wrote — throwing
            // "An item with the same key has already been added".
            // Registering the reference up front closes that window; variants are appended
            // to this same instance below, so the dictionary sees them either way.
            DiscoveredPseudoSeeds.Add(newData.mixId, newData);

            // ── TEMPORARY PROFILING ────────────────────────────────────────────
            // Reducing icon resolution 4x did not remove the synthesis freeze, so the
            // cost is not necessarily the pixel loops. Time each phase separately to
            // find where the frame actually goes before optimising anything further.
            // Remove this block once the hot spot is identified.
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            long msChain = 0, msPrice = 0, msListing = 0;

            foreach (string pseudoBaseId in GetSupportedPseudoBaseIds())
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                QualityItemDefinition customPseudo = factory.CreatePseudoChain(methDef, pseudoBaseId);
                sw.Stop(); msChain += sw.ElapsedMilliseconds;

                if (customPseudo == null)
                {
                    Utility.Error($"CustomPseudoManager: CreatePseudoChain returned null for base '{pseudoBaseId}'.");
                    // Release the reservation made above, or this mix can never be
                    // synthesized again this session (the guard would reject every retry).
                    DiscoveredPseudoSeeds.Remove(newData.mixId);
                    yield break;
                }

                sw.Restart();
                float price = CalculatePseudoPrice(methDef, pseudoBaseId);
                sw.Stop(); msPrice += sw.ElapsedMilliseconds;

                VariantSeedData variant = new VariantSeedData
                {
                    baseItemId = pseudoBaseId,
                    seedId = customPseudo.ID,
                    price = price,
                };
                newData.variants.Add(variant);
                customPseudo.BasePurchasePrice = price;

                sw.Restart();
                CreateShopListing(customPseudo, variant.price);
                sw.Stop(); msListing += sw.ElapsedMilliseconds;

                // Frame-spreading, confirmed by prior diagnostic: `yield return null`
                // correctly lands each tier on its own distinct frame (verified
                // frame-count deltas of exactly 1), but back-to-back yields put the 3
                // elevated-cost frames immediately adjacent with zero gap — still felt
                // as one continuous stutter even though no single frame exceeds a 60fps
                // budget. A real time gap between tiers, not just a frame gap, spaces the
                // 3 cost-spikes far enough apart to read as isolated non-events instead of
                // one continuous judder. Tune the interval to taste — it only adds
                // (interval x2) to a synthesis that already has a 5s delay up front.
                yield return new WaitForSeconds(0.5f);
            }

            var swRecipe = System.Diagnostics.Stopwatch.StartNew();
            factory.InjectRecipeForMix(newData, methDef.ID);
            swRecipe.Stop();
            swTotal.Stop();

            Utility.Log(
                // total now includes ~2 frames of yield return null (frame-spreading, not
                // wasted work) — compare msChain/msPrice/msListing for actual CPU cost,
                // not total, when judging whether this is still an improvement.
                $"[PROFILE pseudo '{methDef.ID}'] total(incl. yields)={swTotal.ElapsedMilliseconds}ms | " +
                $"CreatePseudoChain(x3, prefabs+icons, spread over 3 frames)={msChain}ms | " +
                $"CalculatePseudoPrice(x3)={msPrice}ms | " +
                $"CreateShopListing(x3)={msListing}ms | " +
                $"InjectRecipeForMix={swRecipe.ElapsedMilliseconds}ms");

            string deadDropPseudoBaseId = ResolvePseudoBaseIdForQuality(quality);
            VariantSeedData deadDropVariant = newData.GetVariant(deadDropPseudoBaseId) ?? newData.variants[0];
            QualityItemDefinition deadDropPseudo = Registry.GetItem<QualityItemDefinition>(deadDropVariant.seedId);

            DeadDrop randomDrop = DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position);
            if (randomDrop != null && InstanceFinder.IsServer && deadDropPseudo != null)
            {
                ItemInstance defaultInstance = deadDropPseudo.GetDefaultInstance();
                defaultInstance.SetQuantity(5);
                randomDrop.Storage.InsertItem(defaultInstance, true);

                string guidString = GUIDManager.GenerateUniqueGUID().ToString();
                NetworkSingleton<QuestManager>.Instance.CreateDeaddropCollectionQuest(null, randomDrop.GUID.ToString(), guidString);
                ConversationManager.SendMessage("Shirley", $"{methDef.name} pseudo synthesized and placed in a dead drop.");
                Utility.Log($"CustomPseudoManager: Placed 5x '{deadDropPseudo.ID}' in dead drop '{randomDrop.GUID}'.");
            }
            else
            {
                Utility.Error("CustomPseudoManager: No available dead drop for pseudo placement, not server, or dead drop pseudo could not be resolved.");
            }

            NetworkSyncManager.Broadcast(newData);
        }

        // ─────────────────────────────────────────────────────────────────────
        // RestorePseudoFilters — called from Initialize() on onLoadComplete.
        //
        // For every entry in DiscoveredPseudoSeeds:
        //   - If the pseudo is NOT in the Registry → full rebuild via factory
        //     (covers the case where Patch_ProductManager_CreateMeth ran before
        //     DiscoveredPseudoSeeds was populated, or factory was not yet ready).
        //   - If the pseudo IS in the Registry → re-inject the StationRecipe only
        //     (the canvas may be a fresh instance after scene reload).
        //   - Always re-add to chemistry station slot filters (runtime objects
        //     that reset every scene load).
        // ─────────────────────────────────────────────────────────────────────
        public static void RestorePseudoFilters()
        {
            if (DiscoveredPseudoSeeds.Count == 0) return;

            if (factory == null)
            {
                Utility.Error("CustomPseudoManager.RestorePseudoFilters: factory is null — cannot restore.");
                return;
            }

            foreach (var kvp in DiscoveredPseudoSeeds)
            {
                string mixId = kvp.Key;
#if IL2CPP
                MethDefinition methDef = Registry.GetItem<ProductDefinition>(mixId)?.TryCast<MethDefinition>();
#elif MONO
                MethDefinition methDef = Registry.GetItem<MethDefinition>(mixId);
#endif
                if (methDef == null)
                {
                    Utility.Error($"CustomPseudoManager.RestorePseudoFilters: MethDefinition '{mixId}' not in Registry — skipping.");
                    continue;
                }

                NormalizeVariants(kvp.Value, methDef);

                foreach (var variant in kvp.Value.variants)
                {
                    if (Registry.ItemExists(variant.seedId))
                        continue;

                    QualityItemDefinition rebuilt = factory.CreatePseudoChain(methDef, variant.baseItemId);
                    if (rebuilt == null)
                    {
                        Utility.Error($"CustomPseudoManager.RestorePseudoFilters: CreatePseudoChain returned null for '{mixId}' / '{variant.baseItemId}'.");
                        continue;
                    }
                    Utility.Log($"CustomPseudoManager.RestorePseudoFilters: Rebuilt chain for '{mixId}' / '{variant.baseItemId}'.");
                }

                factory.InjectRecipeForMix(kvp.Value, mixId);

                // Always re-add to station filters — runtime objects reset each load
                foreach (var variant in kvp.Value.variants)
                {
                    var pseudo = Registry.GetItem<QualityItemDefinition>(variant.seedId);
                    if (pseudo != null)
                    {
                        Utility.Log($"CustomPseudoManager.RestorePseudoFilters: Confirmed '{variant.seedId}' in Registry.");
                    }
                    else
                    {
                        Utility.Error($"CustomPseudoManager.RestorePseudoFilters: Could not resolve '{variant.seedId}' from Registry after rebuild.");
                    }
                }
            }
        }

        public static void CreateShopListing(QualityItemDefinition newPseudo, float price = 10f)
        {
            if (ShirleyShop == null) return;

            ShopListing newListing = new ShopListing();
            newListing.name = $"{newPseudo.ID} (${price}) (Pseudo, )";
            newListing.Item = newPseudo;
            newListing.IconTint = new Color(0.2f, 0.8f, 0.2f, 1f);
            newListing.MinimumGameCreationVersion = 27;
            newListing.DefaultStock = 1000;
            newListing.CurrentStock = 100000;
            newListing.CanBeDelivered = true;
            ShirleyShop.Listings.Add(newListing);
            ShirleyShop.CreateListingUI(newListing);
            CreatePhoneShopListing(newPseudo);
            CreateDeliveryListing(newListing);
            ShirleyShop.RefreshShownItems();
        }

        public static void CreatePhoneShopListing(QualityItemDefinition newPseudo)
        {
            if (shirley == null) return;
            PhoneShopInterface.Listing newEntry = new PhoneShopInterface.Listing(newPseudo);
            var updated = HarmonyLib.CollectionExtensions.AddItem(shirley.SupplierData.DeliveryShopListings, newEntry);
            shirley.SupplierData.DeliveryShopListings = updated.ToArray();
        }

        public static void CreateDeliveryListing(ShopListing newListing)
        {
            if (ShirleyShop == null) return;
            var deliveryShop = PlayerSingleton<DeliveryApp>.Instance?.GetShop(ShirleyShop.ShopName);
            if (deliveryShop == null) return;
            ListingEntry entry = UnityEngine.Object.Instantiate<ListingEntry>(deliveryShop.ListingEntryPrefab, deliveryShop.ListingContainer);
            entry.Initialize(newListing);
            entry.onQuantityChanged.AddListener((UnityEngine.Events.UnityAction)deliveryShop.RefreshCart);
            deliveryShop.listingEntries.Add(entry);
            deliveryShop.ListingContainer.sizeDelta = new Vector2(deliveryShop.ListingContainer.sizeDelta.x, 230f + (float)Math.Ceiling(deliveryShop.listingEntries.Count / 2.0) * 60f);
        }

        public static void ClearAll()
        {
            DiscoveredPseudoSeeds.Clear();
            ShirleyShop = null;
            shirley = null;
            if (factory != null) factory.DeleteChildren();
        }

        public static string ResolvePseudoBaseIdForQuality(EQuality quality)
        {
            switch (quality)
            {
                case EQuality.Trash:
                case EQuality.Poor:
                    return PSEUDO_LO_ID;

                case EQuality.Premium:
                case EQuality.Heavenly:
                    // Heavenly gets its own dedicated pseudo base in a future spec.
                    // Until then it uses the highest currently-supported pseudo tier.
                    return PSEUDO_HI_ID;

                case EQuality.Standard:
                default:
                    return PSEUDO_BASE_ID;
            }
        }

        public static string InferPseudoBaseIdFromSeedId(string seedId)
        {
            if (string.IsNullOrEmpty(seedId))
                return PSEUDO_BASE_ID;

            if (seedId.Contains($"_{PSEUDO_LO_ID}_"))
                return PSEUDO_LO_ID;

            if (seedId.Contains($"_{PSEUDO_HI_ID}_"))
                return PSEUDO_HI_ID;

            if (seedId.Contains($"_{PSEUDO_BASE_ID}_"))
                return PSEUDO_BASE_ID;

            Utility.Error($"CustomPseudoManager: Could not infer pseudo base from seedId '{seedId}', defaulting to '{PSEUDO_BASE_ID}'.");
            return PSEUDO_BASE_ID;
        }

        public static string[] GetSupportedPseudoBaseIds()
        {
            return new[] { PSEUDO_LO_ID, PSEUDO_BASE_ID, PSEUDO_HI_ID };
        }

        private static void NormalizeVariants(UnicornSeedData data, MethDefinition methDef)
        {
            if (data.variants == null)
                data.variants = new List<VariantSeedData>();

            foreach (string pseudoBaseId in GetSupportedPseudoBaseIds())
            {
                if (data.GetVariant(pseudoBaseId) != null)
                    continue;

                data.variants.Add(new VariantSeedData
                {
                    baseItemId = pseudoBaseId,
                    seedId = BuildPseudoSeedId(data.mixId, pseudoBaseId),
                    price = CalculatePseudoPrice(methDef, pseudoBaseId),
                });
            }
        }

        public static string BuildPseudoSeedId(string mixId, string pseudoBaseId)
        {
            return $"{mixId}_{pseudoBaseId}_custompseudo";
        }

        private static float CalculatePseudoPrice(MethDefinition methDef, string pseudoBaseId)
        {
            float ingredientCost = StashManager.GetIngredientCost(methDef);
            
            float pseudoBaseCost = 0f;

            var pseudoBase = Registry.GetItem<QualityItemDefinition>(pseudoBaseId);
            if (pseudoBase != null)
                pseudoBaseCost = pseudoBase.BasePurchasePrice;
            else
                Utility.Error($"CustomPseudoManager: Could not resolve pseudo base '{pseudoBaseId}' for price calculation.");

            return ingredientCost + pseudoBaseCost;
        }
    }
}
