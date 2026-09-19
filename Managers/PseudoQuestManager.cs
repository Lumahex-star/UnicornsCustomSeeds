using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnicornsCustomSeeds.SeedQuests;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;

#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Product;
#elif MONO
using FishNet;
using ScheduleOne.Messaging;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.DevUtilities;
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class PseudoQuestManager
    {
        public static CustomSynthesisQuest pseudoDropoff;
        public static string sendableMessageId = "Synthesize Pseudo";
        public static bool IsWaitingForDropoff = false;
        public static bool HasActiveQuest => IsWaitingForDropoff;

        private static float lastSentTime = 0f;

        public static void Init()
        {
            var quest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Meth Mix") as CustomSynthesisQuest;
            if (quest != null)
            {
                pseudoDropoff = quest;
                IsWaitingForDropoff = true;
            }
            else
            {
                IsWaitingForDropoff = false;
            }

            MSGConversation convo = ConversationManager.GetConversation("Shirley");
            if (convo != null)
            {
                SendableMessage sendable = convo.CreateSendableMessage(sendableMessageId);
                sendable.onSent += (Action)OnSent;
            }
        }

        public static void OnSent()
        {
            if (!InstanceFinder.IsServer) return;
            if (Time.time - lastSentTime < 1f) return;
            lastSentTime = Time.time;

            List<string> messages = new List<string> { "Drop the meth mix and cash in my drop box." };
            ConversationManager.SendMessageChain("Shirley", messages);

            IsWaitingForDropoff = true;

            if (pseudoDropoff == null)
            {
                NetworkSyncManager.BroadcastQuestConfig(EDrugType.Methamphetamine);
                pseudoDropoff = S1API.Quests.QuestManager.CreateQuest<PseudoSynthesisQuest>() as CustomSynthesisQuest;
                pseudoDropoff?.SetDrugType(EDrugType.Methamphetamine);
            }
        }

        /// <summary>
        /// Asynchronously creates a CustomSynthesisQuest with retry logic, mirroring
        /// SeedQuestManager.CreateQuestAsync. Called on a client on receipt of a
        /// [NET-QUEST] broadcast for Methamphetamine.
        /// </summary>
        public static void CreateQuestAsync()
        {
            MelonCoroutines.Start(CreateQuestCoroutine());
        }

        private static IEnumerator CreateQuestCoroutine()
        {
            const int maxRetries = 5;
            int attemptCount = 0;

            while (attemptCount < maxRetries)
            {
                attemptCount++;
                try
                {
                    var existingQuest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Meth Mix") as CustomSynthesisQuest;
                    if (existingQuest != null)
                    {
                        pseudoDropoff = existingQuest;
                        IsWaitingForDropoff = true;
                        yield break;
                    }

                    pseudoDropoff = S1API.Quests.QuestManager.CreateQuest<PseudoSynthesisQuest>() as CustomSynthesisQuest;
                    pseudoDropoff?.SetDrugType(EDrugType.Methamphetamine);

                    if (pseudoDropoff != null)
                    {
                        IsWaitingForDropoff = true;
                        yield break;
                    }
                }
                catch
                {
                    // Silently fail - components are still initializing
                }

                if (attemptCount < maxRetries)
                {
                    yield return new WaitForSeconds(1f);
                }
            }

            Utility.Error($"[PseudoQuestManager.CreateQuestAsync] Failed to load quest after {maxRetries} attempts");
        }

        public static void CompleteQuest()
        {
            if (pseudoDropoff != null)
            {
                pseudoDropoff.Complete();
                pseudoDropoff = null;
            }
            IsWaitingForDropoff = false;
        }

        public static void SendMessage(string text) => ConversationManager.SendMessage("Shirley", text);
    }
}
