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
    public static class PhilStashManager
    {
        public static SupplierStash philsStash;
        private static float lastClosedTime = 0f;

        public static SupplierStash GetSupplierStash()
        {
            if (philsStash != null)
            {
                return philsStash;
            }
            GetPhilsStash();
            return philsStash;
        }
        public static void GetPhilsStash()
        {
            if (philsStash != null) return;

            var stashes = UnityEngine.Object.FindObjectsOfType<SupplierStash>();
            foreach (SupplierStash stash in stashes)
            {
                if (stash != null && stash.gameObject.name.ToLower().Contains("phil"))
                {
                    Utility.Log("Phil's stash ready.");
                    philsStash = stash;
                    stash.Storage.onClosed += (Action)PhilsStashClosed;
                    break;
                }
            }
        }

        public static void PhilsStashClosed()
        {
            if (Time.time - lastClosedTime < 1.0f) return;
            lastClosedTime = Time.time;

            if (!ShroomQuestManager.HasActiveQuest) return;

            ItemSlot cashSlot = null;
            CashInstance cashInstance = null;
            ItemSlot shroomSlot = null;
            ShroomInstance shroomInstance = null;

            foreach (var slot in philsStash.Storage.ItemSlots)
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
                if (slot.ItemInstance.TryCast<ShroomInstance>() is ShroomInstance shroomVal)
#elif MONO
                if (slot.ItemInstance is ShroomInstance shroomVal)
#endif
                {
                    shroomSlot = slot;
                    shroomInstance = shroomVal;
                    continue;
                }
            }

            if (cashInstance == null || shroomInstance == null) return;

            int qty = shroomInstance.Quantity;
            if (cashInstance.Balance < StashManager.StashCostEntry.Value || qty < StashManager.StashQtyEntry.Value) return;

#if IL2CPP
            ShroomDefinition definition = shroomInstance.Definition.TryCast<ShroomDefinition>();
#elif MONO
            ShroomDefinition definition = (ShroomDefinition)shroomInstance.Definition;
#endif

            if (definition == null) return;
            if (CustomShroomsManager.DiscoveredShrooms.ContainsKey(definition.ID)) return;

            shroomSlot.ChangeQuantity(-StashManager.StashQtyEntry.Value);
            cashInstance.ChangeBalance(-StashManager.StashCostEntry.Value);

            ShroomQuestManager.CompleteQuest();
            ShroomQuestManager.SendMessage("I will begin synthesizing the shroom syringe.");
            CustomShroomsManager.StartSyringeCreation(definition);
        }
    }
}
