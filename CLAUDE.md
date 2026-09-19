# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Unicorns Custom Seeds** is a MelonLoader mod for the Unity game *Schedule I* (by TVGS). It lets players synthesize custom seeds/syringes (weed, shrooms, coca, pseudo/meth) from discovered product mixes, with full save/load persistence and FishNet multiplayer sync.

**For deep architecture knowledge (game class hierarchies, networking patterns, persistence flow, manager responsibilities), read [Robots.md](Robots.md) first** — it is a comprehensive AI-agent knowledge base covering the decompiled game API and how this mod hooks into it. [README.md](README.md) is the player/dev-facing walkthrough of the original weed-seed flow (some details are now superseded by Robots.md, which covers coca/pseudo/shroom additions too).

## Build Commands

This is a standard .NET SDK project (`UnicornsCustomSeeds.csproj`) built via `dotnet build` or Visual Studio/Rider, **not** via test runners — there is no test suite.

- Two configurations: `MONO` (netstandard2.1, targets the "Schedule 1 alternate" depot build) and `IL2CPP` (net6.0, targets the live Steam build). Configuration controls which `Il2Cpp*` vs plain game assemblies are referenced and which `#if IL2CPP` / `#elif MONO` branches compile.
- Game install paths are hardcoded per-configuration via `$(S1Dir)` in the csproj (`Q:\code\UnityModding\DepotDownloader-windows-x64\Schedule 1 alternate` for MONO, `Z:\Steam\steamapps\common\Schedule I` for IL2CPP) — these are machine-specific.
- `PostBuild` target automatically: kills a running "Schedule I.exe", deletes stale versioned DLLs from the game's `Mods` folder and from `BundleInfo\`, then copies the freshly built DLL into both the game's `Mods` folder and `BundleInfo\` (the release directory).

Build a specific configuration:
```powershell
dotnet build UnicornsCustomSeeds.csproj -c IL2CPP
dotnet build UnicornsCustomSeeds.csproj -c MONO
```

To verify a change actually works, the mod must be built and the game launched with MelonLoader — there's no headless test path.

## Code Style Caveat — This Is a Game Mod

This codebase patches a closed-source game via MelonLoader + Harmony. Code that looks inefficient or awkward (reflection lookups, polling coroutines, FindObjectsOfType scans, RPC hijacking, prefab cloning instead of referencing, string-sentinel protocols) is usually that way **because of game restrictions**, not oversight — the mod has no access to the game's source, code generation pipeline, or asset pipeline. Do not "clean up" such patterns without understanding the constraint that forced them (Robots.md usually documents the why).

## Dual-Compilation Pattern (IL2CPP / MONO)

Every file touching game types uses conditional namespaces:
```csharp
#if IL2CPP
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Product;
#endif
```
and conditional casts where IL2CPP requires `.TryCast<T>()` vs MONO's plain `as T` / `is T`. When adding new game-type usages, mirror this pattern — both configurations must compile.

## High-Level Architecture

### Entry point
[Core.cs](Core.cs) is the `MelonMod` subclass. It wires up:
- `LoadManager.onLoadComplete` → `InitMod()` — initializes all four product managers (`CustomSeedsManager`, `CustomShroomsManager`, `CustomCocaSeedsManager`, `CustomPseudoManager`) and `StashManager`.
- `SaveManager.onSaveComplete` → `SaveData()` — serializes all discovered custom items (across all four managers) into one `DiscoveredCustomSeeds.json`, plus `UnicornsActiveCooking.json` for in-progress cooking state.
- `OnSceneWasLoaded` — (re)constructs factories (`SeedFactory`, `SyringeFactory`, `CocaFactory`, `PseudoFactory`) once their base game definitions exist in the `Registry`, and clears all manager state when leaving the "Main" scene to avoid cross-save contamination. `PseudoFactory` init is deferred via polling coroutine because `ChemistryStationCanvas.Recipes` isn't populated yet at scene-load time.

### One pattern, four product lines
Each drug type (Marijuana/seeds, Mushroom/syringes, Cocaine, Pseudo/meth) follows the same shape, implemented in parallel files:

| Concern | Marijuana | Shrooms | Coca | Pseudo |
|---|---|---|---|---|
| Manager (lifecycle hub) | `Managers/CustomSeedsManager.cs` | `Managers/CustomShroomsManager.cs` | `Managers/CustomCocaSeedsManager.cs` | `Managers/CustomPseudoManager.cs` |
| Factory (prefab/SO cloning) | `Seeds/SeedFactory.cs` | `Seeds/SyringeFactory.cs` | `Seeds/CocaFactory.cs` | `Seeds/PseudoFactory.cs` |
| Quest manager | `Managers/SeedQuestManager.cs` | `Managers/ShroomQuestManager.cs` | `Managers/CocaQuestManager.cs` | `Managers/PseudoQuestManager.cs` |
| Quest definition | `SeedQuests/CustomSeedQuest*.cs` / `CustomSynthesisQuest*.cs` | — | — | — |
| Stash/supplier hook | `Managers/StashManager.cs` (Albert) | `Managers/PhilStashManager.cs` (Phil) | `Managers/SalvadorStashManager.cs` (Salvador) | `Managers/ShirleyStashManager.cs` (Shirley) |

Shared DTO across all four: `Seeds/UnicornSeedData.cs` (`{ seedId, mixId, drugType, price }`, keyed in-memory by `mixId`). `LegacySeedData` in the same file exists only to migrate pre-multi-drug-type save files (old `weedId`/`baseSeedId` schema) — handled in `Patches/PersistencePatches.cs`.

### Networking — RPC hijacking
The mod cannot register new FishNet RPCs, so it repurposes `ProductManager.CreateWeed_Server` (sentinel ID `"ogkushseed"`) as a generic broadcast channel, prefixing the payload string to distinguish traffic: `[NET-JSON]` (seed/syringe/coca/pseudo data), `[NET-JSON-SHROOM]`, `[NET-QUEST]` (quest + config sync). Receiving side is a Harmony postfix on `RpcLogic___CreateWeed_1777266891` in `Patches/NetworkingPatches.cs`. See Robots.md "Networking" section for the full client race-condition handling via `Managers/DeferredPlantsManager.cs`.

### Persistence
Load/migrate logic is a Harmony postfix on `LoadManager.StartGame` in `Patches/PersistencePatches.cs` — it reads `DiscoveredCustomMixes.json`/`DiscoveredCustomSeeds.json`, detects and migrates legacy schema, and splits entries by `drugType` into each manager's `Discovered*` dictionary. Actual game object recreation (factories) happens later in `InitMod()`/`OnSceneWasLoaded`, since runtime Unity objects don't survive scene transitions and must be rebuilt from the lightweight persisted data each load.

### Visuals
`Managers/SeedVisualsManager.cs` loads an embedded AssetBundle (`Assets/customshaders`) containing icon sprites and a label gradient shader, and generates per-mix colored seed icons via CPU pixel manipulation.

### Cooking/Cauldron integration
`Managers/ActiveCookingRegistry.cs` and `Patches/CauldronPatches.cs` track in-progress cauldron cooking sessions (mapping station GUIDs to mix IDs) so they survive save/load, persisted via `UnicornsActiveCooking.json`.

The mod registers **no developer console commands**. An earlier `finishcooking` command was removed: the game's `Console.AddCommand` is private and IL2CPP strips it, so invoking it via reflection throws `EntryPointNotFoundException` at runtime. Don't reintroduce console commands on the IL2CPP target without confirming the native method survives stripping.

## Historical Task Specs

`AgentPrompt_*.md` files at the repo root are **completed** task specs (the `weedId`→`mixId`+`drugType` schema migration, the coca/shroom/pseudo factories, a null-ref fix). All have been implemented — treat them as historical design documents explaining why those features are shaped the way they are, not as outstanding work.
