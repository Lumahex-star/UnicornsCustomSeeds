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
    public static class SalvadorStashManager
    {
        public static SupplierStash salvadorsStash;
        private static float lastClosedTime = 0f;

        public static SupplierStash GetSupplierStash()
        {
            if (salvadorsStash != null)
            {
                return salvadorsStash;
            }
            GetSalvadorsStash();
            return salvadorsStash;
        }
        public static void GetSalvadorsStash()
        {
            if (salvadorsStash != null) return;

            var stashes = UnityEngine.Object.FindObjectsOfType<SupplierStash>();
            foreach (SupplierStash stash in stashes)
            {
                if (stash != null && stash.gameObject.name.ToLower().Contains("salvador"))
                {
                    Utility.Log("Salvador's stash ready.");
                    salvadorsStash = stash;
                    stash.Storage.onClosed += (Action)SalvadorsStashClosed;
                    break;
                }
            }
        }

        public static void SalvadorsStashClosed()
        {
            if (Time.time - lastClosedTime < 1.0f) return;
            lastClosedTime = Time.time;

            if (!CocaQuestManager.HasActiveQuest) return;

            ItemSlot cashSlot = null;
            CashInstance cashInstance = null;
            ItemSlot cocaineSlot = null;
            CocaineInstance cocaineInstance = null;

            foreach (var slot in salvadorsStash.Storage.ItemSlots)
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
                if (slot.ItemInstance.TryCast<CocaineInstance>() is CocaineInstance cocaineVal)
#elif MONO
                if (slot.ItemInstance is CocaineInstance cocaineVal)
#endif
                {
                    cocaineSlot = slot;
                    cocaineInstance = cocaineVal;
                    continue;
                }
            }

            if (cashInstance == null || cocaineInstance == null) return;

            int qty = cocaineInstance.Quantity;
            if (cashInstance.Balance < StashManager.StashCostEntry.Value || qty < StashManager.StashQtyEntry.Value) return;

#if IL2CPP
            CocaineDefinition definition = cocaineInstance.Definition.TryCast<CocaineDefinition>();
#elif MONO
            CocaineDefinition definition = (CocaineDefinition)cocaineInstance.Definition;
#endif

            if (definition == null) return;
            if (CustomCocaSeedsManager.DiscoveredCocaSeeds.ContainsKey(definition.ID)) return;

            cocaineSlot.ChangeQuantity(-StashManager.StashQtyEntry.Value);
            cashInstance.ChangeBalance(-StashManager.StashCostEntry.Value);

            CocaQuestManager.CompleteQuest();
            CocaQuestManager.SendMessage("I will begin synthesizing the coca seed.");
            MelonCoroutines.Start(CustomCocaSeedsManager.CreateCocaSeed(definition));
        }
    }
}
