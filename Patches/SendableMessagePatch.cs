using HarmonyLib;
using UnicornsCustomSeeds.Managers;

#if IL2CPP
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.PlayerScripts;
#elif MONO
using ScheduleOne.Economy;
using ScheduleOne.Messaging;
using ScheduleOne.PlayerScripts;
#endif

namespace UnicornsCustomSeeds.Patches
{
    [HarmonyPatch(typeof(SendableMessage), nameof(SendableMessage.IsValid))]
    public class SendableMessage_IsValid_Patch
    {
        public static bool Prefix(SendableMessage __instance, ref bool __result, out string invalidReason)
        {
            invalidReason = string.Empty;

            string text = __instance.Text;
            if (text == null) return true;

            if (text == SeedQuestManager.sendableMessageId)
            {
                return ValidateRequest(
                    hasActiveQuest:  SeedQuestManager.HasActiveQuest,
                    npcUnlocked:     ConversationManager.albert != null && ConversationManager.albert.RelationData.RelationDelta >= 4f,
                    busyMessage:     "Seed synthesizing is already in progress",
                    relationMessage: "Relationship with Albert isn't good enough",
                    ref __result, out invalidReason);
            }
            else if (text == ShroomQuestManager.sendableMessageId)
            {
                return ValidateRequest(
                    hasActiveQuest:  ShroomQuestManager.HasActiveQuest,
                    npcUnlocked:     CustomShroomsManager.phil != null && CustomShroomsManager.phil.RelationData.RelationDelta >= 4f,
                    busyMessage:     "Shroom syringe synthesizing is already in progress",
                    relationMessage: "Relationship with Phil isn't good enough",
                    ref __result, out invalidReason);
            }
            else if (text == CocaQuestManager.sendableMessageId)
            {
                return ValidateRequest(
                    hasActiveQuest:  CocaQuestManager.HasActiveQuest,
                    npcUnlocked:     CustomCocaSeedsManager.salvador != null && CustomCocaSeedsManager.salvador.RelationData.RelationDelta >= 4f,
                    busyMessage:     "Coca seed synthesizing is already in progress",
                    relationMessage: "Relationship with Salvador isn't good enough",
                    ref __result, out invalidReason);
            }
            else if (text == PseudoQuestManager.sendableMessageId)
            {
                return ValidateRequest(
                    hasActiveQuest:  PseudoQuestManager.HasActiveQuest,
                    npcUnlocked:     CustomPseudoManager.shirley != null && CustomPseudoManager.shirley.RelationData.RelationDelta >= 4f,
                    busyMessage:     "Pseudo synthesizing is already in progress",
                    relationMessage: "Relationship with Shirley isn't good enough",
                    ref __result, out invalidReason);
            }

            return true; // let the game handle all other messages
        }

        // Returns false (skip original) after setting __result and invalidReason.
        private static bool ValidateRequest(
            bool hasActiveQuest, bool npcUnlocked,
            string busyMessage, string relationMessage,
            ref bool __result, out string invalidReason)
        {
            if (hasActiveQuest)
            {
                invalidReason = busyMessage;
                __result = false;
            }
            else if (DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position) == null)
            {
                invalidReason = "No dead drops are available";
                __result = false;
            }
            else if (!npcUnlocked)
            {
                invalidReason = relationMessage;
                __result = false;
            }
            else
            {
                invalidReason = string.Empty;
                __result = true;
            }
            return false; // always skip original — we handled it
        }
    }
}

