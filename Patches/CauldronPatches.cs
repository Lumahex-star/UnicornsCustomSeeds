using System;
using System.Collections.Generic;
using HarmonyLib;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;
using MelonLoader;


#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
#elif MONO
using FishNet;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.ItemFramework;
using ScheduleOne.ObjectScripts;
#endif

namespace UnicornsCustomSeeds.Patches
{
    // ─────────────────────────────────────────────────────────────────────────
    // Strategy: swap Cauldron.CocaineBaseDefinition on the specific instance.
    //
    // We fully replace RemoveIngredients() so we can:
    //   1. Count every leaf type present (first-seen order for tie-breaking).
    //   2. Elect a winner: largest total quantity; first-discovered breaks ties.
    //   3. Swap CocaineBaseDefinition to the winner's custom base (or restore
    //      the vanilla base if the winner is a plain cocaleaf).
    //   4. Replicate the vanilla removal loop restricted to the winner's slots.
    //   5. Record GUID→mixId in ActiveCookingRegistry for persistence.
    //
    // CauldronStartPatch restores any in-progress custom cook from
    // ActiveCookingRegistry when a cauldron is loaded from a save.
    //
    // Patch B (Postfix on RpcLogic___FinishCookOperation) restores
    // CocaineBaseDefinition and clears the ActiveCookingRegistry entry.
    // ─────────────────────────────────────────────────────────────────────────

    public static class CauldronBaseSwap
    {
        // Cauldron instance → original CocaineBaseDefinition to restore after cook
        public static readonly Dictionary<Cauldron, QualityItemDefinition> OriginalBase
     = new Dictionary<Cauldron, QualityItemDefinition>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CauldronStartPatch
    //
    // Two responsibilities:
    //   A) Register an onCookEnd listener — the single authoritative cleanup
    //      point for every cook completion (natural timer or finishcooking).
    //      Restores CocaineBaseDefinition and clears ActiveCookingRegistry.
    //   B) Restore any saved custom cook from ActiveCookingRegistry on load.
    //      Start fires before the cook timer resumes, so swapping here ensures
    //   the correct output even without another RemoveIngredients call.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Cauldron), "Start")]
    public class CauldronStartPatch
    {
        [HarmonyPostfix]
   public static void Postfix(Cauldron __instance)
        {
         if (__instance.isGhost) return;

            try
            {
    // ── A0) Whitelist every already-discovered custom coca leaf ───────
    // Cauldron.Start fires for every instance regardless of when it comes
    // into existence (initial load, mid-session placement, network
    // rebuild), unlike the one-shot FindObjectsOfType sweeps in
    // CocaFactory.AddLeafToCauldrons callers — so patching here guarantees
    // this specific cauldron always has the current whitelist.
    CocaFactory.AddKnownLeavesToCauldron(__instance);

    // ── A) onCookEnd cleanup listener ─────────────────────────────────
  var cauldron = __instance;
    cauldron.onCookEnd.AddListener(new Action(() =>
    {
          try
     {
      if (CauldronBaseSwap.OriginalBase.TryGetValue(cauldron, out QualityItemDefinition original))
        {
      cauldron.CocaineBaseDefinition = original;
       CauldronBaseSwap.OriginalBase.Remove(cauldron);
        Utility.Log($"CauldronStartPatch.onCookEnd: Restored CocaineBaseDefinition on '{cauldron.name}'.");
     }

      string guid = cauldron.GUID.ToString();
         if (ActiveCookingRegistry.GuidToMixId.ContainsKey(guid))
      {
   ActiveCookingRegistry.Unregister(guid);
  Utility.Log($"CauldronStartPatch.onCookEnd: Cleared ActiveCookingRegistry for GUID={guid}.");
          }
      }
        catch (Exception e) { Utility.PrintException(e); }
     }));

            // ── B) Restore saved custom cook on load ──────────────────────────
             string savedGuid = __instance.GUID.ToString();
                string mixId = ActiveCookingRegistry.GetMixId(savedGuid);
    if (mixId == null) return;

                string baseId = $"{mixId}_customcocainebase";
      var rawBase = Registry.GetItem(baseId);
#if IL2CPP
 QualityItemDefinition customBase = rawBase?.TryCast<QualityItemDefinition>();
#elif MONO
             QualityItemDefinition customBase = rawBase as QualityItemDefinition;
#endif
    if (customBase == null)
       {
 Utility.Error($"CauldronStartPatch: Could not resolve '{baseId}' — cauldron will finish with vanilla base.");
       return;
         }

   if (!CauldronBaseSwap.OriginalBase.ContainsKey(__instance))
      CauldronBaseSwap.OriginalBase[__instance] = __instance.CocaineBaseDefinition;

    __instance.CocaineBaseDefinition = customBase;
    Utility.Log($"CauldronStartPatch: Restored custom cook '{mixId}' on cauldron '{savedGuid}' from save.");
   }
   catch (Exception e) { Utility.PrintException(e); }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch A — full replacement of RemoveIngredients().
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Cauldron), nameof(Cauldron.RemoveIngredients))]
    public static class Patch_Cauldron_RemoveIngredients
    {
        static bool Prefix(Cauldron __instance, ref EQuality __result)
        {
            try
            {
                // ── Phase 1: tally leaf types in first-seen order ─────────────────
                var orderedIds = new List<string>();
                var counts = new Dictionary<string, int>();

                for (int i = 0; i < __instance.IngredientSlots.Length; i++)
                {
                    ItemSlot slot = __instance.IngredientSlots[i];
                    if (slot?.ItemInstance == null || slot.Quantity <= 0) continue;

                    string id = slot.ItemInstance.ID;
                    if (!counts.ContainsKey(id))
                    {
                        orderedIds.Add(id);
                        counts[id] = 0;
                    }
                    counts[id] += slot.Quantity;
                }

                if (orderedIds.Count == 0)
                {
                    Utility.Error("CauldronPatches: RemoveIngredients called with empty slots.");
                    return true;
                }

                // ── Phase 2: elect winner ─────────────────────────────────────────
                string winnerId;
                if (orderedIds.Count == 1)
                {
                    winnerId = orderedIds[0];
                }
                else
                {
                    int maxCount = 0;
                    foreach (var kvp in counts)
                        if (kvp.Value > maxCount) maxCount = kvp.Value;

                    winnerId = null;
                    foreach (string id in orderedIds)
                    {
                        if (counts[id] == maxCount) { winnerId = id; break; }
                    }
                }

                Utility.Log($"CauldronPatches: Elected leaf '{winnerId}' " +
     $"(count {counts[winnerId]}) on '{__instance.name}'.");

                // ── Phase 3: set CocaineBaseDefinition + update ActiveCookingRegistry ──
                if (CocaFactory.CustomLeafIdToBaseId.TryGetValue(winnerId, out string baseId))
                {
                    var rawBase = Registry.GetItem(baseId);
#if IL2CPP
                    QualityItemDefinition customBase = rawBase?.TryCast<QualityItemDefinition>();
#elif MONO
         QualityItemDefinition customBase = rawBase as QualityItemDefinition;
#endif
                    if (customBase != null)
                    {
                        if (!CauldronBaseSwap.OriginalBase.ContainsKey(__instance))
                            CauldronBaseSwap.OriginalBase[__instance] = __instance.CocaineBaseDefinition;

                        __instance.CocaineBaseDefinition = customBase;
                        Utility.Log($"CauldronPatches: Swapped CocaineBaseDefinition → '{customBase.ID}'.");

                        // Persist: record GUID → mixId so a save/reload can restore this
                        if (CocaFactory.CustomBaseIdToMixId.TryGetValue(baseId, out string mixId))
                        {
                            ActiveCookingRegistry.Register(__instance.GUID.ToString(), mixId);
                            Utility.Log($"CauldronPatches: Registered active cook GUID={__instance.GUID} mixId={mixId}.");

                            // The swap above is LOCAL ONLY. CauldronTask.Success() is a
                            // PlayerTask, so on a client cook this whole method runs only on
                            // that client — the server never elects the leaf and never swaps
                            // its own CocaineBaseDefinition. But the output is created
                            // server-side:
                            //
                            //   RpcLogic___FinishCookOperation_2166136261()
                            //       if (InstanceFinder.IsServer)
                            //           this.CocaineBaseDefinition.GetDefaultInstance(10)
                            //
                            // so a client cook produced vanilla cocaine base. (Meth is
                            // unaffected: ChemistryStation sends a ChemistryCookOperation
                            // that carries the recipe, and the recipe carries the product, so
                            // it travels as data. Cauldron sends only cookTime + quality.)
                            //
                            // Tell the server which mix this cauldron is cooking so it can
                            // swap its own copy before the cook completes.
                            if (!InstanceFinder.IsServer)
                                NetworkSyncManager.BroadcastCauldronCook(__instance.GUID.ToString(), mixId);
                        }
                    }
                    else
                    {
                        Utility.Error($"CauldronPatches: Could not resolve '{baseId}' as " +
                       "QualityItemDefinition — vanilla base will be used.");
                    }
                }
                else
                {
                    // Vanilla leaf winner — restore if previously swapped
                    if (CauldronBaseSwap.OriginalBase.TryGetValue(__instance, out QualityItemDefinition original))
                    {
                        __instance.CocaineBaseDefinition = original;
                        CauldronBaseSwap.OriginalBase.Remove(__instance);
                        ActiveCookingRegistry.Unregister(__instance.GUID.ToString());
                        Utility.Log("CauldronPatches: Restored vanilla CocaineBaseDefinition for vanilla leaf winner.");
                    }
                }

                // ── Phase 4: replicate vanilla removal — winner slots only ─────────
                __instance.LiquidSlot.ChangeQuantity(-1, false);

                EQuality bestQuality = EQuality.Heavenly;
                int remaining = 20;

                for (int i = __instance.IngredientSlots.Length - 1; i >= 0 && remaining > 0; i--)
                {
                    ItemSlot slot = __instance.IngredientSlots[i];
                    if (slot?.ItemInstance == null || slot.Quantity <= 0) continue;
                    if (slot.ItemInstance.ID != winnerId) continue;

#if IL2CPP
                    QualityItemInstance qi = slot.ItemInstance.TryCast<QualityItemInstance>();
#elif MONO
   QualityItemInstance qi = slot.ItemInstance as QualityItemInstance;
#endif
                    if (qi != null && qi.Quality < bestQuality)
                        bestQuality = qi.Quality;

                    int toRemove = Mathf.Min(remaining, slot.Quantity);
                    slot.ChangeQuantity(-toRemove, false);
                    remaining -= toRemove;
                }

                __result = bestQuality;
                return false;
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
                return true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch B — Postfix on RpcLogic___FinishCookOperation_2166136261.
    //
    // RpcLogic does not reliably fire in practice (confirmed via testing).
    // Cleanup is owned entirely by the onCookEnd listener in CauldronStartPatch.
    // This patch is retained as a diagnostic log only.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Cauldron), nameof(Cauldron.RpcLogic___FinishCookOperation_2166136261))]
    public static class Patch_Cauldron_FinishCookOperation
    {
   public static void Postfix(Cauldron __instance)
        {
    Utility.Log($"CauldronPatches: RpcLogic___FinishCookOperation fired on '{__instance.name}' — cleanup handled by onCookEnd listener.");
        }
    }
}
