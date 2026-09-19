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
    public static class CocaQuestManager
    {
        public static CustomSynthesisQuest cocaDropoff;
        public static string sendableMessageId = "Synthesize Coca";
        public static bool IsWaitingForDropoff = false;
        public static bool HasActiveQuest => IsWaitingForDropoff;

        private static float lastSentTime = 0f;

        public static void Init()
        {
            var quest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Cocaine Mix") as CustomSynthesisQuest;
            if (quest != null)
            {
                cocaDropoff = quest;
                IsWaitingForDropoff = true;
            }
            else
            {
                IsWaitingForDropoff = false;
            }

            MSGConversation convo = ConversationManager.GetConversation("Salvador");
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

            List<string> messages = new List<string> { "Drop the cocaine mix and cash in my drop box." };
            ConversationManager.SendMessageChain("Salvador", messages);

            IsWaitingForDropoff = true;

            if (cocaDropoff == null)
            {
                NetworkSyncManager.BroadcastQuestConfig(EDrugType.Cocaine);
                cocaDropoff = S1API.Quests.QuestManager.CreateQuest<CocaSynthesisQuest>() as CustomSynthesisQuest;
                cocaDropoff?.SetDrugType(EDrugType.Cocaine);
            }
        }

        /// <summary>
        /// Asynchronously creates a CustomSynthesisQuest with retry logic, mirroring
        /// SeedQuestManager.CreateQuestAsync. Called on a client on receipt of a
        /// [NET-QUEST] broadcast for Cocaine.
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
                    var existingQuest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Cocaine Mix") as CustomSynthesisQuest;
                    if (existingQuest != null)
                    {
                        cocaDropoff = existingQuest;
                        IsWaitingForDropoff = true;
                        yield break;
                    }

                    cocaDropoff = S1API.Quests.QuestManager.CreateQuest<CocaSynthesisQuest>() as CustomSynthesisQuest;
                    cocaDropoff?.SetDrugType(EDrugType.Cocaine);

                    if (cocaDropoff != null)
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

            Utility.Error($"[CocaQuestManager.CreateQuestAsync] Failed to load quest after {maxRetries} attempts");
        }

        public static void CompleteQuest()
        {
            if (cocaDropoff != null)
            {
                cocaDropoff.Complete();
                cocaDropoff = null;
            }
            IsWaitingForDropoff = false;
        }

        public static void SendMessage(string text) => ConversationManager.SendMessage("Salvador", text);
    }
}
