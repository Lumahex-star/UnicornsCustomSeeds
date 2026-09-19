using System;
using Newtonsoft.Json;
using UnicornsCustomSeeds.Patches;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2Cpp;                 // global-namespace game types (GUIDManager) get this prefix
using Il2CppFishNet;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;
using GenericCol = Il2CppSystem.Collections.Generic;
#elif MONO
using FishNet;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Growing;
using ScheduleOne.ItemFramework;
using ScheduleOne.Management;
using ScheduleOne.ObjectScripts;
using ScheduleOne.Product;
using GenericCol = System.Collections.Generic;
#endif

namespace UnicornsCustomSeeds.Managers
{
    /// <summary>
    /// Multiplayer sync for coca / shroom / pseudo custom items.
    ///
    /// Mirrors the weed pattern: the mod cannot register its own FishNet RPCs, so it
    /// hijacks each drug type's existing ProductManager.Create&lt;Type&gt;_Server as a
    /// generic string channel. The payload rides in the `name` parameter behind a
    /// "[NET-JSON]" prefix; the `id` parameter carries a sentinel.
    ///
    /// SENTINEL CHOICE IS LOad-BEARING: every RpcLogic___Create* begins with an
    /// "already exists" early-return, so the sentinel must be a vanilla item ID that
    /// is ALWAYS in the Registry. Otherwise the game would instantiate a junk product
    /// named "[NET-JSON]{...}" and add it to AllProducts/ProductNames.
    ///
    /// Weed keeps its original Postfix receiver. The three channels added here use a
    /// Prefix that skips the original instead — coca/meth/shroom log
    /// Console.LogError("Product with ID X already exists") on the early-return path
    /// (weed returns silently), so a Postfix would spam the game console once per
    /// item per sync.
    /// </summary>
    public static class NetworkSyncManager
    {
        public const string PAYLOAD_PREFIX = "[NET-JSON]";

        /// <summary>
        /// Kill switch (MelonPreferences: SyncNonWeedDrugs). When false, nothing is sent
        /// and received payloads are ignored — weed sync is untouched either way.
        /// Defaults to true if config has not initialized yet.
        /// </summary>
        public static bool Enabled =>
            StashManager.SyncNonWeedDrugs == null || StashManager.SyncNonWeedDrugs.Value;

        // Vanilla IDs guaranteed to exist in the Registry — see class remarks.
        public const string SENTINEL_COCA   = CustomCocaSeedsManager.BASE_SEED_ID;      // "cocaseed"
        public const string SENTINEL_SHROOM = CustomShroomsManager.BASE_SYRINGE_ID;     // "sporesyringe"
        public const string SENTINEL_METH   = CustomPseudoManager.BASE_LIQUIDMETH_ID;   // "liquidmeth"

        // ─────────────────────────────────────────────────────────────────────
        // Send
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Broadcast one discovered item to all clients on its drug type's channel.
        /// Server-only; harmless no-op elsewhere.
        /// </summary>
        public static void Broadcast(UnicornSeedData data)
        {
            if (data == null || !InstanceFinder.IsServer || !Enabled) return;

            try
            {
                // Weed keeps its existing, already-working broadcast path.
                if (data.drugType == EDrugType.Marijuana)
                {
                    CustomSeedsManager.BroadcastCustomSeed(data);
                    return;
                }

                ProductManager pm = NetworkSingleton<ProductManager>.Instance;
                if (pm == null)
                {
                    Utility.Error("NetworkSyncManager.Broadcast: ProductManager instance is null.");
                    return;
                }

                string payload = PAYLOAD_PREFIX + JsonConvert.SerializeObject(data, Formatting.None);
                var props = new GenericCol.List<string>();

                switch (data.drugType)
                {
                    case EDrugType.Cocaine:
                        pm.CreateCocaine_Server(payload, SENTINEL_COCA, EDrugType.Cocaine,
                                                props, new CocaineAppearanceSettings());
                        break;

                    case EDrugType.Shrooms:
                        pm.CreateShroom_Server(payload, SENTINEL_SHROOM, EDrugType.Shrooms,
                                               props, new ShroomAppearanceSettings());
                        break;

                    case EDrugType.Methamphetamine:
                        pm.CreateMeth_Server(payload, SENTINEL_METH, EDrugType.Methamphetamine,
                                             props, new MethAppearanceSettings());
                        break;

                    default:
                        Utility.Error($"NetworkSyncManager.Broadcast: unsupported drugType '{data.drugType}'.");
                        return;
                }

                Utility.Log($"NetworkSyncManager: Broadcast {data.drugType} mix '{data.mixId}'.");
            }
            catch (Exception ex) { Utility.PrintException(ex); }
        }

        /// <summary>
        /// Broadcasts quest-start config (cost/qty/time) plus which drug started the
        /// quest, over the same hijacked CreateWeed_Server channel weed's [NET-QUEST]
        /// already used. Previously only weed had this — Coca/Shroom/Pseudo's OnSent()
        /// created their quest locally on whichever machine ran it (always the host,
        /// since a client's own onSent() no-ops via the IsServer check) and never told
        /// the other side, so those three quests only ever appeared for the host.
        /// Server-only; harmless no-op elsewhere.
        /// </summary>
        public static void BroadcastQuestConfig(EDrugType drugType)
        {
            if (!InstanceFinder.IsServer) return;
            if (drugType != EDrugType.Marijuana && !Enabled) return;

            ProductManager pm = NetworkSingleton<ProductManager>.Instance;
            if (pm == null)
            {
                Utility.Error("NetworkSyncManager.BroadcastQuestConfig: ProductManager instance is null.");
                return;
            }

            string payload = $"[NET-QUEST]{(int)drugType},{StashManager.StashCostEntry.Value},{StashManager.StashQtyEntry.Value},{StashManager.SynthesizeTime.Value}";

            var props = new GenericCol.List<string>();
            var appearance = new WeedAppearanceSettings(
                pm.DefaultWeed.MainMat.color,
                pm.DefaultWeed.SecondaryMat.color,
                pm.DefaultWeed.LeafMat.color,
                pm.DefaultWeed.StemMat.color);

            pm.CreateWeed_Server(payload, CustomSeedsManager.BASE_SEED_ID, EDrugType.Marijuana, props, appearance);
        }

        public const string COOK_PREFIX = "[NET-COOK]";

        /// <summary>
        /// Client -> server: "this cauldron is cooking this custom mix, swap your copy".
        ///
        /// CauldronTask.Success() is a PlayerTask — on a client cook, RemoveIngredients()
        /// (and the mod's CocaineBaseDefinition swap) runs ONLY on that client. The output
        /// is created server-side from the server's own CocaineBaseDefinition, so without
        /// this the server hands back vanilla cocaine base.
        ///
        /// Rides CreateWeed_Server, which is [ServerRpc(RequireOwnership = false)] — a
        /// client calling it executes on the server, which is the direction needed here
        /// (unlike the [NET-JSON]/[NET-QUEST] traffic, which flows server -> clients).
        /// </summary>
        public static void BroadcastCauldronCook(string cauldronGuid, string mixId)
        {
            if (string.IsNullOrEmpty(cauldronGuid) || string.IsNullOrEmpty(mixId)) return;

            try
            {
                ProductManager pm = NetworkSingleton<ProductManager>.Instance;
                if (pm == null)
                {
                    Utility.Error("NetworkSyncManager.BroadcastCauldronCook: ProductManager instance is null.");
                    return;
                }

                string payload = $"{COOK_PREFIX}{cauldronGuid},{mixId}";
                var props = new GenericCol.List<string>();
                var appearance = new WeedAppearanceSettings(
                    pm.DefaultWeed.MainMat.color,
                    pm.DefaultWeed.SecondaryMat.color,
                    pm.DefaultWeed.LeafMat.color,
                    pm.DefaultWeed.StemMat.color);

                pm.CreateWeed_Server(payload, CustomSeedsManager.BASE_SEED_ID, EDrugType.Marijuana, props, appearance);
                Utility.Log($"NetworkSyncManager: Sent cauldron cook GUID={cauldronGuid} mixId={mixId} to server.");
            }
            catch (Exception ex) { Utility.PrintException(ex); }
        }

        /// <summary>
        /// Server-side handler for [NET-COOK]. Resolves the cauldron by GUID and applies
        /// the same swap the initiating client already made locally, so
        /// FinishCookOperation's server-side output uses the custom base.
        /// </summary>
        public static void HandleCauldronCook(string payload)
        {
            try
            {
                string body = payload.Substring(COOK_PREFIX.Length);
                int comma = body.IndexOf(',');
                if (comma <= 0)
                {
                    Utility.Error($"NetworkSyncManager.HandleCauldronCook: malformed payload '{body}'.");
                    return;
                }

                string cauldronGuid = body.Substring(0, comma);
                string mixId = body.Substring(comma + 1);

                // GUIDManager.GetObject takes the runtime's own Guid type, which differs
                // between the two targets.
#if IL2CPP
                Cauldron cauldron = GUIDManager.GetObject<Cauldron>(new Il2CppSystem.Guid(cauldronGuid));
#elif MONO
                Cauldron cauldron = GUIDManager.GetObject<Cauldron>(new Guid(cauldronGuid));
#endif
                if (cauldron == null)
                {
                    Utility.Error($"NetworkSyncManager.HandleCauldronCook: no cauldron for GUID '{cauldronGuid}'.");
                    return;
                }

                string baseId = $"{mixId}_customcocainebase";
                var rawBase = Registry.GetItem(baseId);
#if IL2CPP
                QualityItemDefinition customBase = rawBase?.TryCast<QualityItemDefinition>();
#elif MONO
                QualityItemDefinition customBase = rawBase as QualityItemDefinition;
#endif
                if (customBase == null)
                {
                    Utility.Error($"NetworkSyncManager.HandleCauldronCook: could not resolve '{baseId}' — server will output vanilla base.");
                    return;
                }

                // Mirror CauldronPatches: remember the original so the shared onCookEnd
                // listener restores it, and register so a save/reload rebuilds the swap.
                if (!CauldronBaseSwap.OriginalBase.ContainsKey(cauldron))
                    CauldronBaseSwap.OriginalBase[cauldron] = cauldron.CocaineBaseDefinition;

                cauldron.CocaineBaseDefinition = customBase;
                ActiveCookingRegistry.Register(cauldronGuid, mixId);

                Utility.Log($"NetworkSyncManager: Server swapped cauldron {cauldronGuid} CocaineBaseDefinition -> '{customBase.ID}' for client cook.");
            }
            catch (Exception ex) { Utility.PrintException(ex); }
        }

        /// <summary>
        /// Replay every discovered coca / shroom / pseudo item onto the wire.
        ///
        /// Called from a Postfix on ProductManager.OnSpawnServer so it runs AFTER the
        /// game has queued its own product-creation RPCs for the joining client. That
        /// ordering matters: our payloads reference a base mix by ID, and the client
        /// must already have that vanilla ProductDefinition in its Registry to rebuild.
        /// </summary>
        public static void BroadcastAllDiscovered()
        {
            if (!InstanceFinder.IsServer || !Enabled) return;

            foreach (var kvp in CustomCocaSeedsManager.DiscoveredCocaSeeds) Broadcast(kvp.Value);
            foreach (var kvp in CustomShroomsManager.DiscoveredShrooms)     Broadcast(kvp.Value);
            foreach (var kvp in CustomPseudoManager.DiscoveredPseudoSeeds)  Broadcast(kvp.Value);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Receive
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if this RPC carries a mod payload on the expected channel.
        /// Callers use the result to skip the game's original method.
        /// </summary>
        public static bool TryParsePayload(string name, string id, string sentinel, out UnicornSeedData data)
        {
            data = null;
            if (!Enabled) return false;
            if (id != sentinel) return false;
            if (string.IsNullOrEmpty(name) || !name.StartsWith(PAYLOAD_PREFIX)) return false;

            try
            {
                data = JsonConvert.DeserializeObject<UnicornSeedData>(name.Substring(PAYLOAD_PREFIX.Length));
            }
            catch (Exception ex)
            {
                Utility.PrintException(ex);
                return true; // still mod traffic — swallow it rather than let vanilla run
            }
            return true;
        }

        /// <summary>
        /// Rebuild a received item locally. Idempotent — safe on the host, where the
        /// item already exists (RunLocally = true means the host runs this too).
        /// </summary>
        public static void RebuildFromNetwork(UnicornSeedData data)
        {
            if (data == null || string.IsNullOrEmpty(data.mixId)) return;

            try
            {
                switch (data.drugType)
                {
                    case EDrugType.Cocaine:        RebuildCoca(data);   break;
                    case EDrugType.Shrooms:        RebuildShroom(data); break;
                    case EDrugType.Methamphetamine: RebuildPseudo(data); break;
                    default:
                        Utility.Error($"NetworkSyncManager: unsupported drugType '{data.drugType}' for '{data.mixId}'.");
                        break;
                }
            }
            catch (Exception ex) { Utility.PrintException(ex); }
        }

        private static void RebuildCoca(UnicornSeedData data)
        {
            if (!CustomCocaSeedsManager.DiscoveredCocaSeeds.ContainsKey(data.mixId))
                CustomCocaSeedsManager.DiscoveredCocaSeeds.Add(data.mixId, data);

            string seedId = $"{data.mixId}_customcocaseed";
            if (Registry.ItemExists(seedId)) return;

            if (CustomCocaSeedsManager.factory == null)
            {
                Utility.Error($"NetworkSyncManager: CocaFactory is null — cannot rebuild '{data.mixId}'.");
                return;
            }

            var cocaineDef = Registry.GetItem<ProductDefinition>(data.mixId);
            if (cocaineDef == null)
            {
                Utility.Error($"NetworkSyncManager: ProductDefinition '{data.mixId}' not in Registry yet — coca rebuild skipped.");
                return;
            }

            SeedDefinition newSeed = CustomCocaSeedsManager.factory.CreateCocaSeedDefinition(cocaineDef);
            if (newSeed == null) return;

            Singleton<Registry>.Instance.AddToRegistry(newSeed);

            try { Singleton<ManagementUtilities>.Instance.Seeds.Add(newSeed); }
            catch (Exception ex) { Utility.PrintException(ex); }

            CustomSeedsManager.AddSeedToPots(newSeed);
            CustomCocaSeedsManager.CreateShopListing(newSeed, data.price);

            var customLeaf = Registry.GetItem<QualityItemDefinition>($"{data.mixId}_customcocaleaf");
            if (customLeaf != null)
                CocaFactory.AddLeafToCauldrons(customLeaf);
            else
                Utility.Error($"NetworkSyncManager: Could not resolve leaf for '{data.mixId}' — cauldrons will not accept it.");

            // Coca plants ride the same Pot/PlantSeed_Client path as weed, so a pot
            // that referenced this seed before it arrived is now replayable.
            DeferredPlantsManager.TrySpawnQueuedPlants(newSeed.ID);

            Utility.Log($"NetworkSyncManager: Rebuilt coca seed '{newSeed.ID}'.");
        }

        private static void RebuildShroom(UnicornSeedData data)
        {
            if (!CustomShroomsManager.DiscoveredShrooms.ContainsKey(data.mixId))
                CustomShroomsManager.DiscoveredShrooms.Add(data.mixId, data);

            if (Registry.ItemExists($"{data.mixId}_customsyringedefinition")) return;

            // SyringeDefinitionLoader registers the syringe, adds it to
            // ManagementUtilities.MushroomSpawns, wires spawn stations and shop listing.
            var newSyringe = CustomShroomsManager.SyringeDefinitionLoader(data);
            if (newSyringe == null) return;

            try { CustomShroomsManager.AddSpawnToMushroomBeds(newSyringe.SpawnDefinition); }
            catch (Exception ex) { Utility.PrintException(ex); }

            Utility.Log($"NetworkSyncManager: Rebuilt syringe '{newSyringe.ID}'.");
        }

        private static void RebuildPseudo(UnicornSeedData data)
        {
            if (!CustomPseudoManager.DiscoveredPseudoSeeds.ContainsKey(data.mixId))
                CustomPseudoManager.DiscoveredPseudoSeeds.Add(data.mixId, data);

            if (CustomPseudoManager.factory == null)
            {
                Utility.Error($"NetworkSyncManager: PseudoFactory is null — cannot rebuild '{data.mixId}'.");
                return;
            }

#if IL2CPP
            MethDefinition methDef = Registry.GetItem<ProductDefinition>(data.mixId)?.TryCast<MethDefinition>();
#elif MONO
            MethDefinition methDef = Registry.GetItem<MethDefinition>(data.mixId);
#endif
            if (methDef == null)
            {
                Utility.Error($"NetworkSyncManager: MethDefinition '{data.mixId}' not in Registry yet — pseudo rebuild skipped.");
                return;
            }

            foreach (var variant in data.variants)
            {
                if (Registry.ItemExists(variant.seedId)) continue;
                if (CustomPseudoManager.factory.CreatePseudoChain(methDef, variant.baseItemId) == null)
                    Utility.Error($"NetworkSyncManager: CreatePseudoChain returned null for '{data.mixId}' / '{variant.baseItemId}'.");
            }

            CustomPseudoManager.factory.InjectRecipeForMix(data, data.mixId);

            Utility.Log($"NetworkSyncManager: Rebuilt pseudo chain for '{data.mixId}'.");
        }
    }
}
