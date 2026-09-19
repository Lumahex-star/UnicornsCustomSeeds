using System;
using MelonLoader;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;

#if IL2CPP
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class ShirleyStashManager
    {
        public static SupplierStash shirleysStash;
        private static float lastClosedTime = 0f;

        public static SupplierStash GetSupplierStash()
        {
            if (shirleysStash != null)
            {
                return shirleysStash;
            }
            GetShirleysStash();
            return shirleysStash;
        }
        public static void GetShirleysStash()
        {
            if (shirleysStash != null) return;

            var stashes = UnityEngine.Object.FindObjectsOfType<SupplierStash>();
            foreach (SupplierStash stash in stashes)
            {
                if (stash != null && stash.gameObject.name.ToLower().Contains("shirley"))
                {
                    Utility.Log("Shirley's stash ready.");
                    shirleysStash = stash;
                    stash.Storage.onClosed += (Action)ShirleysStashClosed;
                    break;
                }
            }
        }

        public static void ShirleysStashClosed()
        {
            if (Time.time - lastClosedTime < 1.0f) return;
            lastClosedTime = Time.time;

            if (!PseudoQuestManager.HasActiveQuest) return;

            ItemSlot cashSlot = null;
            CashInstance cashInstance = null;
            ItemSlot methSlot = null;
            MethInstance methInstance = null;

            foreach (var slot in shirleysStash.Storage.ItemSlots)
            {
                if (slot?.ItemInstance == null) continue;

#if IL2CPP
                if (slot.ItemInstance.TryCast<CashInstance>() is CashInstance cashVal)
#elif MONO
                if (slot.ItemInstance is CashInstance cashVal)
#endif
                {
                    cashSlot = slot;
                    cashInstance = cashVal;
                    continue;
                }

#if IL2CPP
                if (slot.ItemInstance.TryCast<MethInstance>() is MethInstance methVal)
#elif MONO
                if (slot.ItemInstance is MethInstance methVal)
#endif
                {
                    methSlot = slot;
                    methInstance = methVal;
                    continue;
                }
            }

            if (cashInstance == null || methInstance == null) return;

            int qty = methInstance.Quantity;
            if (cashInstance.Balance < StashManager.StashCostEntry.Value || qty < StashManager.StashQtyEntry.Value) return;

#if IL2CPP
            MethDefinition definition = methInstance.Definition.TryCast<MethDefinition>();
#elif MONO
            MethDefinition definition = (MethDefinition)methInstance.Definition;
#endif

            if (definition == null) return;
            if (CustomPseudoManager.DiscoveredPseudoSeeds.ContainsKey(definition.ID)) return;

            methSlot.ChangeQuantity(-StashManager.StashQtyEntry.Value);
            cashInstance.ChangeBalance(-StashManager.StashCostEntry.Value);

            PseudoQuestManager.CompleteQuest();
            PseudoQuestManager.SendMessage("I will begin synthesizing the pseudo chain.");
            MelonCoroutines.Start(CustomPseudoManager.CreatePseudoChain(definition,methInstance.Quality));
        }
    }
}
