# Unicorn's Custom Seeds — AI Agent Knowledge Base

## Overview
A MelonLoader mod for **Schedule I** (Unity game by TVGS) that allows players to synthesize custom weed seeds from discovered product mixes. The mod integrates deeply with the game's networking (FishNet), persistence (save/load), UI, growing, and NPC systems.

**Version:** 1.1.1  
**Author:** OverweightUnicorn  
**Mod Framework:** MelonLoader  
**Game Networking:** FishNet  
**Target Frameworks:** .NET 6 (mod) / .NET Framework 4.8 (game Assembly-CSharp)  
**Dual-compilation:** All game type references use `#if IL2CPP` / `#elif MONO` conditional compilation.

---

## Project Layout
```
Core.cs — MelonMod entry: hooks LoadManager.onLoadComplete → InitMod(), SaveManager.onSaveComplete → SaveData()
Seeds/SeedFactory.cs — Clones SeedDefinition ScriptableObject + 6 linked prefabs from base "ogkushseed". Colors meshes to match WeedDefinition appearance. All clones parented under inactive DontDestroyOnLoad root.
Seeds/SyringeFactory.cs — Clones SporeSyringeDefinition + ShroomSpawnDefinition ScriptableObjects from base "sporesyringe". Assigns custom ShroomDefinition to cloned ShroomSpawnDefinition. All clones parented under inactive DontDestroyOnLoad root.
Seeds/UnicornSeedData.cs — Shared serializable DTO: { seedId, mixId, drugType, price }. Single class for all drug types. LegacySeedData (internal) used only for migrating old save files.
Seeds/SeedVialLabel.cs — MonoBehaviour on vial "Label" transforms. Reads seedId from GO name, applies gradient shader via MaterialPropertyBlock.
Managers/CustomSeedsManager.cs — Central hub for weed seeds. Initialize(): finds Albert's shop, inits subsystems, creates listings for loaded seeds. CreateSeed(): coroutine that waits SynthesizeTime, calls SeedFactory, registers in Registry, creates shop/delivery listings, adds to pots, places in DeadDrop, broadcasts via [NET-JSON].
Managers/CustomShroomsManager.cs — Central hub for shroom syringes. Initialize(): finds Phil's shop, registers Phil's conversation, sets up "Synthesize Shrooms" sendable message. CreateSyringe(): coroutine that calls SyringeFactory, registers in Registry, places in DeadDrop.
Managers/StashManager.cs — Hooks Albert's SupplierStash.Storage.onClosed. Scans for CashInstance + WeedInstance, validates amounts, triggers seed creation if quest active. Config via MelonPreferences.
Managers/ConversationManager.cs — Finds Albert and Phil NPCs, registers their MSGConversations by name ("Albert", "Phil"). On first load: sends welcome or injects into dialogue chain based on relationship level.
Managers/SeedQuestManager.cs — Creates "Synthesize Seeds" SendableMessage on Albert's conversation. OnSent: creates S1API quest, broadcasts [NET-QUEST].
Managers/SeedVisualsManager.cs — Loads AssetBundle (shader, sprites). GenerateSpriteWithGradient(): CPU pixel manipulation for colored seed icons.
Managers/DeferredPlantsManager.cs — Handles client-join race condition. Queues PlantSeed_Client calls for unregistered seeds, replays when seed definition arrives via network.
Patches/NetworkingPatches.cs — RPC hijacking. Postfix on ProductManager.RpcLogic___CreateWeed_1777266891: intercepts [NET-JSON] (seed data), [NET-JSON-SHROOM] (syringe data), and [NET-QUEST] (config sync). Postfix on OnSpawnServer: sends all seeds + syringes to joining clients. Prefix on Pot.PlantSeed_Client/SetHarvestableActive_Client: defers for unregistered seeds.
Patches/PersistencePatches.cs — Postfix on LoadManager.StartGame: reads DiscoveredCustomMixes.json, detects legacy format via JObject, migrates if needed, splits by drugType into DiscoveredSeeds / DiscoveredShrooms. Postfix on ExitToMenu: cleans ManagementUtilities.Seeds. Postfix on ProductManager.CreateWeed: creates weed seeds during game's own weed loading.
Patches/ProductManagerAppPatches.cs — Prefix on ProductManagerApp.Start: modifies EntryPrefab to add SeedIndicator. Postfix on ProductEntry.Initialize: shows/hides indicator per product.
Patches/SendableMessagePatch.cs — Prefix on SendableMessage.IsValid: custom validation for "Synthesize Seeds" (no active quest, dead drop available, relationship ≥ 4).
Patches/AddRecipePatch.cs — Postfix on ProductDefinition.AddRecipe: caches ingredient costs.
SeedQuests/ — S1API Quest subclass "Drop off the Mix" with QuestEntry pointing to Albert's stash.
```

---

## Game Architecture Patterns (from decompiled Assembly-CSharp)

### Singleton Access
- `Singleton<T>.Instance` — scene-scoped singletons (Registry, SaveManager, ManagementUtilities)
- `NetworkSingleton<T>.Instance` — networked singletons (ProductManager, QuestManager, TimeManager, LevelManager, VariableDatabase)
- `PlayerSingleton<T>.Instance` — per-player singletons (PlayerCamera, PlayerInventory, MessagesApp, DeliveryApp, ProductManagerApp)

### FishNet Networking Pattern
Game uses FishNet for multiplayer. Methods follow this pattern:
- `MethodName_Server` → `[ServerRpc]` — client calls server
- `MethodName` / `MethodName_Client` → `[ObserversRpc]` + `[TargetRpc]` — server broadcasts to clients
- `RpcLogic___MethodName_{hash}` → **actual logic body** generated by FishNet codegen. This is where the real work happens after serialization/deserialization.

**Key constraint:** The mod cannot create its own FishNet RPCs because it has no access to FishNet's code generation pipeline. This is why it **hijacks existing RPCs** as a transport channel.

### Registry System
- `Registry` stores all `ItemDefinition` instances in a hash-based dictionary (`ItemDictionary`)
- `AddToRegistry()` adds items at runtime, tracked in `ItemsAddedAtRuntime` list
- `RemoveRuntimeItems()` cleans up on scene change (hooked to `LoadManager.onPreSceneChange`)
- Lookup: `Registry.GetItem<T>(string ID)`, `Registry.ItemExists(string ID)`
- Hash-based: `GetHash(string ID)` generates int key for dictionary

### Item Definition Hierarchy
```
BaseItemDefinition (ScriptableObject)
  Fields: Name, ID, Description, Icon, Category, StackLimit, EquippableData, legalStatus
  └── ItemDefinition (abstract)
       Fields: Equippable (legacy), EquipMode, CustomItemUI
       └── StorableItemDefinition
            Fields: StoredItem, RequiresLevelToPurchase, RequiredRank, BasePurchasePrice
    ├── SeedDefinition        ← mod creates instances of this
            │   Fields: FunctionSeedPrefab (FunctionalSeed), PlantPrefab (Plant)
            └── PropertyItemDefinition
      Fields: Properties (List<Effect>)
    └── ProductDefinition (abstract, ISaveable)
    Fields: DrugTypes, BasePrice, MarketValue, FunctionalProduct,
 EffectsDuration, BaseAddictiveness, ValidPackaging,
      ConsumeAnimation, Recipes (List<StationRecipe>)
        └── WeedDefinition         ← mod reads from this
       Fields: MainMat, SecondaryMat, LeafMat, StemMat (Materials)
       appearance (WeedAppearanceSettings, private)
```

### Item Instance Hierarchy
```
BaseItemInstance
  Fields: _definition, Quantity
  Methods: SetQuantity(), ChangeQuantity(), RequestClearSlot(), GetMonetaryValue()
  └── ItemInstance (abstract)
  Property: Definition (ItemDefinition), Equippable
       Methods: GetCopy(), GetItemData(), Write(), Read()
       └── StorableItemInstance
    Property: StoredItem
 ├── QualityItemInstance
 │   Property: Quality (EQuality)
            │   └── ProductItemInstance
          │        Property: AppliedPackaging (PackagingDefinition)
      │        Field: PackagingID
         │        └── WeedInstance
            │             Constructor: (definition, quantity, quality, packaging)
         │       Methods: GetCopy(), ApplyEffectsToNPC/Player()
    └── CashInstance
         Property: Balance (float)
 Methods: ChangeBalance(amount), SetBalance(newBalance)
```

### Plant/Growing Hierarchy
```
Plant (NetworkBehaviour)
  Fields: SeedDefinition, GrowthStages (PlantGrowthStage[])
  Methods: GetHarvestedProduct(), SetHarvestableActive()
  └── WeedPlant
   Field: BranchPrefab (PlantHarvestable)
       GetHarvestedProduct() → creates QualityItemInstance from BranchPrefab.Product

PlantGrowthStage (MonoBehaviour)
  Field: GrowthSites (Transform[]) — positions where harvestables grow

PlantHarvestable (MonoBehaviour)
  Fields: Product (StorableItemDefinition), ProductQuantity
  Harvest() → gets product from Plant, discovers ProductDefinition if needed

FunctionalSeed (MonoBehaviour)
  Fields: Vial (Draggable), SeedBlocker, Cap (VialCap), SeedCollider, SeedRigidbody, TrashPrefab
  — Physical vial prefab used in seed planting task

Equippable_Seed (Equippable_Viewmodel)
  Field: Seed (SeedDefinition)
  Update() → raycasts for Pot, checks CanAcceptSeed, starts SowSeedTask
  StartSowSeedTask() → new SowSeedTask(pot, this.Seed)

AvatarEquippable (MonoBehaviour)
  Fields: AlignmentPoint (Transform), Hand (EHand), AnimationTrigger, AssetPath
  Equip() → parents to avatar hand container, positions, initializes animation
  — Third-person visual representation when holding seed
```

### Key Game Classes — Detailed

#### Pot (NetworkBehaviour, ObjectScripts)
```
Fields: ModelTransform, SeedStartPoint, PlantContainer, ConfigurationReplicator,
        PotRadius, YieldMultiplier, GrowSpeedMultiplier
Properties: Plant, Configuration (→ PotConfiguration), ConfigurableType
Methods:
  PlantSeed_Server(seedID, normalizedProgress) [ServerRpc]
  PlantSeed_Client(conn, seedID, normalizedProgress) [ObserversRpc+TargetRpc]
  SetHarvestableActive_Server(index, active) [ServerRpc]
  SetHarvestableActive_Client(conn, index, active) [ObserversRpc+TargetRpc]
  CanAcceptSeed(out reason) → checks if pot is empty
  RpcLogic___PlantSeed_Client_4077118173 → actual plant logic (patched by mod)
  RpcLogic___SetHarvestableActive_Client_338960014 → actual harvestable logic (patched by mod)
```

#### PotConfiguration (Management)
```
Fields: Seed (ItemField), Additive1/2/3 (ItemField), AssignedBotanist (NPCField),
        Destination (ObjectField)
Properties: Pot, DestinationRoute
— Seed.Options populated from ManagementUtilities.Seeds list
— Mod adds custom seeds to both ManagementUtilities.Seeds AND each existing pot's config
```

#### ManagementUtilities (Singleton)
```
Fields: Seeds (List<SeedDefinition>), MushroomSpawns (List<ShroomSpawnDefinition>)
Properties: StorageTypeIcon, StorageUIElementPrefab
— PotConfiguration reads Seeds from here to populate seed selection UI
— Mod adds/removes custom seeds from this list
```

#### StorageEntity (NetworkBehaviour, IItemSlotOwner)
```
Fields: StorageEntityName, StorageEntitySubtitle, SlotCount, ItemSlots (List<ItemSlot>)
Properties: IsOpened, CurrentPlayerAccessor, ItemCount
Events: onOpened, onClosed, onContentsChanged
Methods: InsertItem(item, network), GetAllItems(), CanItemFit(), ClearContents()
RPCs: SetStoredInstance(), SetItemSlotQuantity(), SetSlotLocked(), SetSlotFilter()
```

#### ItemSlot
```
Properties: ItemInstance, Quantity, IsAtCapacity, IsLocked, SlotOwner
Events: onItemDataChanged, onItemInstanceChanged
Methods: SetStoredItem(), InsertItem(), AddItem(), ClearStoredInstance(),
         SetQuantity(), ChangeQuantity(), ApplyLock(), RemoveLock()
Static: TryInsertItemIntoSet(List<ItemSlot>, ItemInstance)
```

#### DeadDrop (MonoBehaviour, IGUIDRegisterable)
```
Static: DeadDrops (List<DeadDrop>), GetRandomEmptyDrop(origin)
Fields: DeadDropName, DeadDropDescription, Region, Storage (WorldStorageEntity),
    PoI, Light, BakedGUID
Properties: GUID
GetRandomEmptyDrop() → filters empty drops, removes closest, removes far half, random pick
— Mod uses this to place synthesized seeds for player pickup
```

#### SupplierStash (MonoBehaviour)
```
Fields: Supplier, Storage (StorageEntity), IntObj, Light, StashPoI, locationDescription
Properties: CashAmount
Events: Storage.onClosed (mod hooks this)
Awake() → sets up interaction message, hooks onUnlocked, subscribes onContentsChanged
RecalculateCash() → iterates slots, sums CashInstance.Balance
— Mod hooks onClosed to detect when player deposits weed + cash
```

#### ShopInterface (MonoBehaviour, ISaveable)
```
Fields: ShopName, ShopCode, Listings (List<ShopListing>), Canvas, ListingContainer,
        Cart, DeliveryBays, DeliveryVehicle
Methods: CreateListingUI(listing) — creates UI for a listing
         RefreshShownItems() — updates visibility based on filters/stock
         GetListing(itemID), SetIsOpen(), HandoverItems()
```

#### ShopListing (Serializable)
```
Fields: name, Item (StorableItemDefinition), OverridePrice, OverriddenPrice,
        LimitedStock, DefaultStock, CurrentStock, CanBeDelivered,
        UseIconTint, IconTint, ConditionalVisibility, MinimumGameCreationVersion
Properties: IsInStock, Price, IsUnlimitedStock, Shop, QuantityInCart
Methods: Initialize(shop), Restock(), RemoveStock(), SetStock(), ShouldShow()
— Mod creates new ShopListing instances with OverridePrice=true
```

#### PhoneShopInterface (MonoBehaviour)
```
Fields: EntryPrefab (RectTransform), EntryContainer, Container, ConfirmButton
Methods: Open(title, subtitle, conversation, listings, orderLimit, debt, callback)
   Close(), ChangeListingQuantity()
Inner classes:
  Listing { Item (StorableItemDefinition), Price → Item.BasePurchasePrice }
  CartEntry { Listing, Quantity }
— Mod adds Listing entries to Albert's OnlineShopItems array
```

#### DeliveryApp (App<DeliveryApp>, PlayerSingleton)
```
Fields: deliveryShops (List<DeliveryShop>), StatusDisplayPrefab, StatusDisplayContainer
Methods: GetShop(ShopInterface), GetShop(string shopName)
— Mod calls GetShop("Albert Hoover") to find delivery shop
— Then instantiates ListingEntry prefab and adds to shop's ListingContainer
```

#### ListingEntry (MonoBehaviour)
```
Fields: Icon, ItemNameLabel, ItemPriceLabel, QuantityInput, IncrementButton, DecrementButton
Properties: MatchingListing (ShopListing), SelectedQuantity
Methods: Initialize(match) — sets up icon, name, price, buttons
         SetQuantity(quant), RefreshLocked()
```

#### Albert (Supplier → NPC → NetworkBehaviour)
```
Inherits: Supplier → NPC
— Simple subclass with only NetworkInitialize boilerplate
— Supplier has: OnlineShopItems (PhoneShopInterface.Listing[]),
                MSGConversation, RelationData, DialogueHandler,
                SupplierStash reference
```

#### MSGConversation (Serializable, ISaveable)
```
Fields: contactName, sender (NPC), messageHistory, Sendables (List<SendableMessage>),
        entry, container, senderInterface (MessageSenderInterface)
Events: onMessageRendered, onLoaded, onResponsesShown, onConversationOpened
Methods: SendMessage(message, notify, network)
   SendMessageChain(messages, initialDelay, notify, network)
 CreateSendableMessage(text) → creates SendableMessage, adds to senderInterface
 RenderPlayerMessage(sendable), SetOpen(open)
```

#### SendableMessage
```
Fields: Text, ShouldShowCheck (delegate), IsValidCheck (delegate),
 onSelected (Action), onSent (Action), disableDefaultSendBehaviour
Methods: ShouldShow() → checks delegate or returns true
     IsValid(out invalidReason) → checks delegate or returns true
         Send(network, id) → calls onSelected, conversation.SendPlayerMessage()
— Mod patches IsValid() prefix to intercept "Synthesize Seeds" message
— Validation: no active quest, dead drop available, Albert relationship ≥ 4
```

#### MessageSenderInterface (MonoBehaviour)
```
Enum: EVisibility { Hidden, Docked, Expanded }
Fields: Menu, SendablesContainer, ComposeButton, CancelButtons
Methods: SetVisibility(visibility) — shows/hides compose UI
         UpdateSendables() — shows/hides/enables sendable message bubbles
AddSendable(sendable) — creates MessageBubble UI for sendable
— When visibility=Expanded, iterates sendables calling ShouldShow() and IsValid()
```

#### NPCRelationData (Serializable)
```
Properties: RelationDelta (float, 0-5), NormalizedRelationDelta, Unlocked, UnlockType, NPC
Events: onRelationshipChange (Action<float>), onUnlocked (Action<EUnlockType, bool>)
Methods: ChangeRelationship(), SetRelationship(), Unlock()
Enum: EUnlockType { Recommendation, DirectApproach }
Constants: DEFAULT_RELATION_DELTA = 2f, MaxDelta = 5f
— Mod checks RelationDelta >= 4f for seed synthesis eligibility
— Hooks onUnlocked to send welcome message when Albert is first unlocked
```

#### DialogueHandler (MonoBehaviour)
```
Fields: Database (DialogueDatabase), LookPosition, DialogueEvents
Methods: InitializeDialogue(), ShowNode(), EndDialogue()
— Mod accesses Database.GetModule(EDialogueModule.Generic) to find dialogue entries
```

#### DialogueChain (Serializable)
```
Fields: Lines (string[])
Methods: GetMessageChain() → converts to MessageChain
— Mod appends welcome message to Albert's "supplier_meetings_unlocked" chain
  by creating new Lines array with extra element
```

#### MessageChain (Serializable)
```
Fields: Messages (List<string>), id (int)
Static: Combine(a, b) → merges two chains
— Mod creates MessageChain instances for sending multi-message sequences
```

#### Player (NetworkBehaviour)
```
Static: Local (Player), PlayerList, onLocalPlayerSpawned, onPlayerSpawned
Fields: Avatar, Anim, Health, CrimeData, Energy, Inventory (ItemSlot[])
Properties: PlayerName, PlayerCode, IsLocalPlayer, CameraPosition, EyePosition
— Mod uses Player.Local.transform.position for DeadDrop.GetRandomEmptyDrop()
```

#### WeedAppearanceSettings (Serializable)
```
Fields: MainColor, SecondaryColor, LeafColor, StemColor (all Color32)
Constructor: (mainColor, secondaryColor, leafColor, stemColor)
Methods: IsUnintialized() → checks if any color is Color.clear
```

#### WeedDefinition (ProductDefinition)
```
Fields: MainMat, SecondaryMat, LeafMat, StemMat (Material), appearance (WeedAppearanceSettings, private)
Methods:
  Initialize(properties, drugTypes, appearance) → base.Initialize + ApplyAppearanceSettings
  GetAppearanceSettings(properties) [static] → generates colors from Effect list
  ApplyAppearanceSettings() → creates new Material instances with appearance colors
  GetMaterial(type) → returns material by EWeedAppearanceType enum
  GetSaveData() → WeedProductData with properties and appearance
```

#### CashInstance (StorableItemInstance)
```
Property: Balance (float, 0 to 1e9)
Methods: ChangeBalance(amount), SetBalance(newBalance, blockClear)
         — SetBalance clamps and calls RequestClearSlot if balance <= 0
```

#### WeedInstance (ProductItemInstance)
```
Constructor: (definition, quantity, quality, packaging)
Property: AppliedPackaging (from base ProductItemInstance)
Methods: GetCopy(), GetItemData(), ApplyEffectsToNPC/Player(), ClearEffectsFromNPC/Player()
— Mod reads AppliedPackaging.Name to determine package multiplier (Brick=20, Jar=5, Baggie=1)
— Mod reads Definition (cast to WeedDefinition) for seed creation
```

---

## Core Mod Flow

### 1. Initialization Sequence
```
OnInitializeMelon() → AssetBundleUtils.Initialize()
OnLateInitializeMelon() → StashManager.InitializeConfig(), SeedVisualsManager.LoadSeedMaterial()
  → Hook LoadManager.onLoadComplete → InitMod()
     → Hook SaveManager.onSaveComplete → SaveData()
OnSceneWasLoaded("Main") → Create SeedFactory if null, StashManager.GetAlbertsStash()
OnSceneWasLoaded(other)  → ClearAll() to prevent cross-save contamination
     → Re-check visual assets (GC protection)
```

### 2. Two Distinct Seed Creation Contexts

There are two fundamentally different contexts in which custom seeds are created:

#### Runtime Creation (During Gameplay)
When a player synthesizes a new seed while the game is already running, all game systems are initialized. The WeedDefinition exists, pots are loaded, shop UI is ready. This is the simple path:

```
StashManager.AlbertsStashClosed() validates items
→ CustomSeedsManager.StartSeedCreation(weedDef) — coroutine
  → Wait SynthesizeTime seconds
  → factory.CreateSeedDefinition(weedDef)
  → Registry.AddToRegistry(), CreateShopListing(), AddSeedToPots()
  → Insert 10 seeds into DeadDrop, create collection quest
  → BroadcastCustomSeed() via [NET-JSON] to all connected clients
```

#### Boot-Time Recreation (Loading a Save)
When loading a save with previously-discovered seeds, the game's load order matters critically. The game was designed assuming all seeds are hardcoded compile-time assets — always present in the Registry. Custom seeds don't exist until the mod creates them at runtime, but pots, storage, and inventory may reference them during load.

### 3. Host Load Order (Orderly — Seeds Before Consumers)

On the host, the game's natural load sequence provides a safe ordering:

```
1. LoadManager.StartGame() fires
   → [Mod Postfix] Reads DiscoveredCustomSeeds.json
   → Populates DiscoveredSeeds dictionary (data only, no objects yet)

2. Game loads saveables in order. ProductManager loads early.
   → ProductManager.CreateWeed() fires for each saved weed mix
   → [Mod Postfix: Patch_ProductManager_CreateWeed] For each weed ID
      with a matching DiscoveredSeed entry:
      → SeedDefinitionLoader() → factory.CreateSeedDefinition()
      → Adds to ManagementUtilities.Seeds
   ★ Custom SeedDefinitions now exist in Registry BEFORE pots load

3. Game loads Pots → PlantSeed_Client fires with custom seedIDs
   → Registry.GetItem(seedID) succeeds — step 2 already created it
   → Plants render correctly with custom colors

4. Storage entities load — stored custom seeds resolve from Registry

5. LoadManager.onLoadComplete fires
   → [Mod hook] InitMod() runs:
     → Creates shop listings, delivery listings, phone shop entries
 → Adds seeds to pot configuration UI options
     → Sets up NPC conversation, quest system
```
**Why this works:** ProductManager (weed mixes) loads before Pots (which reference seeds). The mod hooks `ProductManager.CreateWeed` to piggyback seed definition creation, ensuring seeds exist in the Registry before any consumer tries to look them up.

### 4. Client Load Order (Unordered — Race Condition)

On a joining client, state arrives via FishNet RPCs with **no guaranteed ordering**. The client doesn't load from a save file — it receives replicated state as network objects spawn.

```
1. Client connects to server

2. Game replicates NetworkBehaviour state via OnSpawnServer callbacks
   (arbitrary order as network objects spawn):
   
   → Pot.OnSpawnServer: Server sends PlantSeed_Client RPC
     PlantSeed_Client(conn, "someweed_customseeddefinition", progress)
 ⚠ Custom seed definition DOESN'T EXIST YET on client!
     Registry.GetItem("someweed_customseeddefinition") → null
      
   → ProductManager.OnSpawnServer: [Mod Postfix] Server sends all
     discovered seeds as [NET-JSON] via CreateWeed_Server
     ⚠ May arrive AFTER the pot RPCs above!

3. [Mod Prefix on Pot.RpcLogic___PlantSeed_Client_4077118173]:
   → Only intercepts on pure client (not server/host)
   → Detects "customseeddefinition" in seedID AND !Registry.ItemExists
   → Queues {Pot, SeedId, Progress} in DeferredPlantsManager
   → Returns false (skips original — prevents crash/silent failure)
   
   [Mod Prefix on Pot.RpcLogic___SetHarvestableActive_Client_338960014]:
   → If pot GUID pending, defers harvestable update too

4. [NET-JSON] payloads arrive via RpcLogic___CreateWeed:
 → SeedDefinitionLoader() creates SeedDefinition + all prefabs
   → Registers, creates listings, adds to pots
   → DeferredPlantsManager.TrySpawnQueuedPlants() on client
   → Queued pots are replayed: PlantSeed_Client + SetHarvestableActive
   → Pot from PendingPotGuids is removed
```
**Why DeferredPlantsManager exists:** The game assumes seeds are always available (hardcoded assets). On the host, the mod exploits natural load order. On a client, FishNet RPCs arrive unordered — pots may reference seeds that haven't been created yet. Without deferred replay, `PlantSeed_Client` would fail on Registry lookup, leaving pots visually empty.

### 5. InitMod() (after game loads on host)
```
CustomSeedsManager.Initialize()
  → Find "WeedSupplierInterface" ShopInterface (Albert's seed shop)
  → ConversationManager.Init() — find Albert NPC, setup messaging
  → SeedQuestManager.Init() — create "Synthesize Seeds" sendable message
  → InitDictionary() — cache all base seed listings from shop
  → AddScrollToPhoneInterface() — add ScrollRect to phone shop UI
  → For each DiscoveredSeed (already created in step 2/3 above):
    → Get existing SeedDefinition from Registry
    → Create shop listing + phone listing + delivery listing
StashManager.GetAlbertsStash() — find Albert's SupplierStash, hook onClosed
```

### 6. Seed Synthesis Flow (Player-Initiated at Runtime)
```
Player sends "Synthesize Seeds" text to Albert
  → SeedQuestManager.OnSent()
    → Sends Albert reply chain: "Drop the weed mix and cash in my drop box."
    → Creates CustomSeedQuest (via S1API)
    → BroadcastCustomQuest() via [NET-QUEST] channel
    → Sets IsWaitingForDropoff = true

Player places weed + cash in Albert's stash, closes it
  → StashManager.AlbertsStashClosed()
    → Iterates storage slots for CashInstance and WeedInstance
    → Calculates total weed units: quantity × PackageAmount(packaging.Name)
    → Validates: cash ≥ StashCostEntry, total ≥ StashQtyEntry, no existing seed for mix
    → If valid + quest active:
      → Deducts weed (StashQtyEntry/packageAmount) and cash (StashCostEntry)
      → SeedQuestManager.CompleteQuest()
      → CustomSeedsManager.StartSeedCreation(weedDef)

StartSeedCreation(weedDef) — coroutine:
  → Wait SynthesizeTime seconds
  → factory.CreateSeedDefinition(weedDef) — clones all prefabs
  → Register in Registry, create shop listing, add to pots, enable UI indicator
  → Place 10 seeds in random DeadDrop + create collection quest
  → Send Albert message about completion
  → BroadcastCustomSeed() via [NET-JSON] channel
```

### 7. SeedFactory — Prefab Cloning

**Why cloning is necessary:** A `SeedDefinition` is a `ScriptableObject` that references 4-5 prefab GameObjects (`PlantPrefab`, `FunctionSeedPrefab`, `StoredItem`, and via `Equippable` → `Equippable_Seed` → `AvatarEquippable`). Each prefab contains component references back to the definition and to the product it yields. The game's systems (Registry, Pot.PlantSeed, equipping, storage grid rendering) all follow these references. You can't just create a SeedDefinition and point it at existing prefabs — the `WeedPlant.BranchPrefab.Product` would still reference the original weed, `Equippable_Seed.Seed` would reference the wrong definition, and the plant appearance (material colors on mesh renderers) wouldn't match the custom mix.

```
CreateSeedDefinition(WeedDefinition weedDef)
  → ScriptableObject.Instantiate(baseSeedDefinition) — clone SO
  → Set ID = "{weedDef.ID}_customseeddefinition", Name = "{weedDef.name} Seed"
  → CloneEquippableSeedPrefab(newDef, appearance):
  → Instantiate baseEquippableSeedPrefab → set .Seed = newDef
      → CloneAvatarEquippablePrefab() for third-person model
      → GrowLabel() on both → adds SeedVialLabel + custom shader material
  → ClonePlantPrefab(weedDef):
      → Instantiate basePlantPrefab (WeedPlant)
      → SetPlantAppearance() → iterate all GrowthStages, color Stem/BigLeaves/SmallLeaves
        → For each GrowthSite: color harvestable branch meshes (Main/Secondary/Leaves/Stem)
      → CloneBranchPrefab() → set Product = weedDef
      → Set newPlant.BranchPrefab and all child PlantHarvestable.Product = weedDef
  → CloneFunctionalSeedPrefab() → GrowLabel()
  → CloneStoredItem() → GrowLabel()
  → Generate colored icon via SeedVisualsManager.GenerateSpriteWithGradient()
  → Store appearance in SeedVisualsManager.appearanceMap[seedId]
  → All clones parented under inactive DontDestroyOnLoad root GO
```

---

## Networking — The RPC Hijacking Pattern

### Why This Pattern Exists
The mod needs to synchronize custom seed data between host and clients. Since mods can't create new FishNet `[ServerRpc]`/`[ObserversRpc]` methods (FishNet requires compile-time code generation), the mod repurposes an existing RPC that:
1. Accepts string parameters (can carry serialized data)
2. Broadcasts from server to all clients (ObserversRpc)
3. Won't cause side effects when called with sentinel values

`ProductManager.CreateWeed_Server()` fits: it takes a `name` string, an `id` string, and broadcasts via `RpcLogic___CreateWeed_1777266891`. The mod uses `id="ogkushseed"` as a sentinel and prefixes `name` with `[NET-JSON]` or `[NET-QUEST]` to distinguish mod traffic from real weed creation.

### Transport Channel
**Sending (server → all):**
```csharp
prodManager.CreateWeed_Server(
    payload, // "[NET-JSON]{json}" or "[NET-QUEST]cost,qty,time"
    "ogkushseed",          // sentinel ID — triggers mod interception
    EDrugType.Marijuana,   // dummy (required parameter)
    new List<string>(),    // dummy
    defaultAppearance      // dummy
);
```

**Receiving (Harmony Postfix on `RpcLogic___CreateWeed_1777266891`):**
```csharp
if (id != "ogkushseed") return;     // not mod traffic, ignore
if (name.StartsWith("[NET-JSON]"))        // seed definition broadcast
  → Deserialize UnicornSeedData from JSON
  → Skip if seed already exists in Registry
  → SeedDefinitionLoader() → factory.CreateSeedDefinition()
  → Add to ManagementUtilities.Seeds, create shop listing, add to pots
  → DeferredPlantsManager.TrySpawnQueuedPlants() on client
if (name.StartsWith("[NET-QUEST]"))       // quest + config sync
  → Parse comma-separated config values (cost, qty, time)
  → Update StashManager config entries
  → CreateQuestAsync() with retry logic
```

### Late-Join Client Sync
When a new client connects, game calls `OnSpawnServer(connection)`:
- **`ProductManager.OnSpawnServer` Postfix:** Server iterates all `DiscoveredSeeds` and sends each as `[NET-JSON]` via `CreateWeed_Server`. Skips host connection.
- **`QuestManager.OnSpawnServer` Postfix:** If quest exists, broadcasts `[NET-QUEST]`

### Deferred Plant Spawning (Client-Side Race Condition)
**Problem:** When a client joins, the game sends Pot state RPCs before custom seed definitions arrive via [NET-JSON]. Pot.PlantSeed_Client does Registry.GetItem(seedID) which fails. Solution: Prefix intercepts unregistered custom seed IDs, queues them in DeferredPlantsManager, replays after seed definition is created.

1. **Prefix on `Pot.RpcLogic___PlantSeed_Client_4077118173`:**
   - Only intercepts on pure client (not server/host)
   - Only intercepts IDs containing `"customseeddefinition"` that aren't in Registry
   - Stores `{Pot, SeedId, Progress}` in `DeferredPlantsManager.seedsToLoad[seedId]`
   - Tracks pot GUID in `PendingPotGuids` set
   - Returns `false` (skips original method)

2. **Prefix on `Pot.RpcLogic___SetHarvestableActive_Client_338960014`:**
   - If pot GUID is in `PendingPotGuids`, defers the update
   - Stores `{Index, Active}` in `DeferredHarvestables[potGuid]`

3. **When `[NET-JSON]` arrives** and seed is created:
   - `TrySpawnQueuedPlants(seedId)`:
     - Sets `IsReplaying = true` (prevents prefix from re-intercepting)
     - Calls `pot.PlantSeed_Client(null, seedId, progress)` for each deferred pot
     - Replays any deferred harvestable updates via `pot.Plant.SetHarvestableActive()`
     - Removes pot from `PendingPotGuids`

---

## Persistence

### Save (hooked to `SaveManager.onSaveComplete`)
```
Core.SaveData()
  → Get LoadedGameFolderPath from LoadManager
  → Combine DiscoveredSeeds.Values + DiscoveredShrooms.Values into one List<UnicornSeedData>
  → Serialize to JSON via Newtonsoft
  → Write to "{LoadedGameFolderPath}/DiscoveredCustomMixes.json"
```

### Load (Harmony Postfix on `LoadManager.StartGame`)
```
LoadManager_StartGame_Patch.Postfix()
  → Read "{LoadedGameFolderPath}/DiscoveredCustomMixes.json"
  → Peek at raw JSON via JArray/JObject to detect legacy format (presence of "weedId" key)
  → If legacy: deserialize as List<LegacySeedData>, migrate each to UnicornSeedData
      (seedId = legacy.seedId, mixId = legacy.weedId, drugType = EDrugType.Marijuana, price = legacy.price)
      Re-save migrated data immediately to overwrite legacy file
  → If current: deserialize as List<UnicornSeedData>
  → Filter by drugType:
      EDrugType.Marijuana → CustomSeedsManager.DiscoveredSeeds[entry.mixId]
      EDrugType.Mushroom  → CustomShroomsManager.DiscoveredShrooms[entry.mixId]
  → Set FirstLoad = false (existing save) or true (new save / no file)
  → Note: Does NOT create definitions yet — that happens in InitMod()
```

### Why Seeds/Syringes Are Recreated Each Load (Not Cached)
Runtime Unity objects (ScriptableObjects, cloned GameObjects) are destroyed on scene change. `Registry.RemoveRuntimeItems()` clears the registry. `SeedFactory.DeleteChildren()` and `SyringeFactory.DeleteChildren()` destroy prefab clones. `ManagementUtilities.Seeds`, shop listings, pot configurations all reset. Only the lightweight `UnicornSeedData` is persisted — everything else is rebuilt from base game assets + this data.

---

## UI Integration

### ProductManagerApp — Seed Indicator
**Prefix on `ProductManagerApp.Start`:**
- Clones `EntryPrefab` GameObject, adds "SeedIndicator" child (duplicated from `FavouriteButton`)
- Removes Button component from clone, keeps Image
- Positions at top-left corner (anchored 0,1), sets seed icon sprite from AssetBundle
- Replaces `__instance.EntryPrefab` with modified clone
- Original prefab stays as template; all future entries instantiated from modified version

**Postfix on `ProductEntry.Initialize`:**
- After each entry initializes with a ProductDefinition
- Finds "SeedIndicator" child transform
- Shows if `DiscoveredSeeds.ContainsKey(definition.ID)`, hides otherwise

### Phone Shop Interface — Scroll Support
`AddScrollToPhoneInterface()`:
- Finds `Shade/Content/Entries` in `MessagesApp.PhoneShopInterface.transform`
- Creates ScrollView wrapper with `ScrollRect`, `Mask`, `ContentSizeFitter`
- Reparents entries under ScrollView, sets as ScrollRect content
- Adds `VerticalLayoutGroup` settings for proper spacing
- Game's phone shop UI doesn't scroll by default — needed when many custom seeds listed

### Shop Listing Creation
`CreateShopListing(SeedDefinition, price)`:
- Creates new `ShopListing` with: `OverridePrice=true`, `OverriddenPrice=price`, `Item=newSeed`, `DefaultStock=1000`, `CurrentStock=100000`, `CanBeDelivered=true`, tinted cyan
- Adds to `Shop.Listings` (WeedSupplierInterface)
- Calls `Shop.CreateListingUI(newListing)` — game creates the in-world shop UI
- Calls `Shop.RefreshShownItems()` — updates visible items
- Also creates delivery listing via `CreateDeliveryListing()`:
  - Gets Albert's DeliveryShop via `DeliveryApp.GetShop("Albert Hoover")`
  - Instantiates `ListingEntry` prefab, calls `Initialize(newListing)`
  - Adds listener for `RefreshCart`, updates container sizing

### Albert's Online Shop (Phone)
In `Initialize()`, for each DiscoveredSeed:
- Creates `PhoneShopInterface.Listing(customSeedDef)` 
- Appends to `albert.OnlineShopItems` array using `HarmonyLib.CollectionExtensions.AddItem`
- Price uses `StorableItemDefinition.BasePurchasePrice` (set during SeedDefinition creation)

---

## NPC Integration — Albert Hoover

### ConversationManager
- Finds `Albert` NPC via `FindObjectOfType<Albert>()`
- Registers his `MSGConversation` in `Conversations["Albert"]`
- Stores `albertRelation = albert.RelationData`
- **First load + relationship ≥ 4:** Sends welcome message after 10s delay (coroutine)
- **First load + relationship < 4:** Injects welcome text into Albert's dialogue:
  - Gets `DialogueDatabase` from `albert.DialogueHandler.Database`
  - Finds `EDialogueModule.Generic` module
  - Locates entry with key `"supplier_meetings_unlocked"`
  - Appends welcome message to first chain's `Lines` array (creates new array, copies existing + adds message)

### SeedQuestManager
- On `Init()`: Creates `SendableMessage` via `convo.CreateSendableMessage("Synthesize Seeds")`
- This automatically adds a sendable bubble to `senderInterface` (the compose UI)
- `sendable.onSent` += `OnSent` callback
- `OnSent()`:
  - Server only (checked via `InstanceFinder.IsServer`)
  - Sends reply chain to Albert: "Drop the weed mix and cash in my drop box."
  - Creates `CustomSeedQuest` via S1API
  - Broadcasts `[NET-QUEST]` to sync quest + config to clients
- Uses S1API's `QuestManager.GetQuestByName()` and `QuestManager.CreateQuest<T>()` 

### SendableMessage Validation (Harmony Prefix)
```csharp
// Prefix on SendableMessage.IsValid
if (Text == "Synthesize Seeds") {
    if (HasActiveQuest) → "Seed Synthesizing is already in progress"
    if (no empty dead drop) → "No deaddrops are available"  
    if (albert.RelationDelta < 4f) → "Relationship isn't good enough"
    else → valid
    return false; // skip original IsValid
}
return true; // all other messages use original logic
```

### StashManager — Albert's Supply Stash
- Finds Albert's `SupplierStash` via `FindObjectsOfType<SupplierStash>()` filtering by name
- Hooks `stash.Storage.onClosed += AlbertsStashClosed`
- **`AlbertsStashClosed()`:**
  - Debounce: checks `Time.time - lastClosedTime < 1.0f`
  - Scans all `Storage.ItemSlots` for `CashInstance` and `WeedInstance`
  - `PackageAmount()`: Brick→20, Jar→5, Baggie→1, default→1
  - Total units = `weedInstance.Quantity × PackageAmount(AppliedPackaging.Name)`
  - If cash ≥ StashCostEntry AND total ≥ StashQtyEntry AND no existing seed AND quest active:
 - Deducts weed: `weedSlot.ChangeQuantity(-(StashQtyEntry / packageAmount))`
    - Deducts cash: `cashInstance.ChangeBalance(-StashCostEntry)`
    - Completes quest, starts seed creation

---

## Visual System

### SeedVisualsManager
- Loads embedded AssetBundle "customshaders" containing:
  - `customseed_icon.png` — base seed sprite for icon generation (512x512 vial image)
  - `seedicon.png` — small seed indicator icon for ProductManagerApp
  - `labelgradient.shader` — custom shader for vial label gradient effect
- All assets marked `DontDestroyOnLoad` to survive scene transitions
- `GenerateSpriteWithGradient(topColor, bottomColor)`:
  - Gets pixels from base sprite texture
  - Applies vertical gradient in normalized rect (0.37, 0.312, 0.24, 0.38) — the label area
  - Blend modes: Lerp (default), Multiply, Add, Screen
  - Creates new Texture2D + Sprite with colored result

### SeedVialLabel (MonoBehaviour, RegisterTypeInIl2Cpp)
- Attached to "Label" transforms on vial/equippable/stored item prefabs via `GrowLabel()`
- Label's GameObject name format: `"Label:{seedDefId}"`
- On `Star
