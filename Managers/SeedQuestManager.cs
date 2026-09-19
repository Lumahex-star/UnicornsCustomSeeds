using UnicornsCustomSeeds.SeedQuests;
using MelonLoader;
using System.Collections;
using UnicornsCustomSeeds.TemplateUtils;



#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Product;
using GenericCol = Il2CppSystem.Collections.Generic;
#elif MONO
using FishNet;
using ScheduleOne.Messaging;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.DevUtilities;
using ScheduleOne.Product;
using GenericCol = System.Collections.Generic;
#endif
namespace UnicornsCustomSeeds.Managers
{
    public static class SeedQuestManager
    {
        public static CustomSynthesisQuest seedDropoff;

        public static string sendableMessageId = "Synthesize Seeds";
        public static bool IsWaitingForDropoff = false;
        public static bool HasActiveQuest => IsWaitingForDropoff;

        private static float lastSentTime = 0f;

        public static void Init()
        {
            var quest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Mix") as CustomSynthesisQuest;
            if (quest != null)
            {
                seedDropoff = quest;
                IsWaitingForDropoff = true;
            }
            else
            {
                IsWaitingForDropoff = false;
            }

            MSGConversation convo = ConversationManager.GetConversation("Albert");
            if (convo != null)
            {
                MessageSenderInterface senderInterface = convo.senderInterface;
                SendableMessage sendable = convo.CreateSendableMessage(sendableMessageId);
                sendable.onSent += (Action)OnSent;
            }
        }

        public static void OnSent()
        {
            if (InstanceFinder.IsServer)
            {
                if (UnityEngine.Time.time - lastSentTime < 1f)
                {
                    return;
                }
                List<string> messages = new List<string>();
                messages.Add("Drop the weed mix and cash in my drop box.");
                ConversationManager.SendMessageChain("Albert", messages);

                IsWaitingForDropoff = true;

                if (seedDropoff == null)
                {
                    if (InstanceFinder.IsServer) BroadcastCustomQuest();
                    seedDropoff = S1API.Quests.QuestManager.CreateQuest<WeedSynthesisQuest>() as CustomSynthesisQuest;
                    seedDropoff?.SetDrugType(EDrugType.Marijuana);
                }
            }
        }

        public static void BroadcastCustomQuest() => NetworkSyncManager.BroadcastQuestConfig(EDrugType.Marijuana);

        /// <summary>
        /// Asynchronously creates a CustomSynthesisQuest with retry logic
        /// Retries up to 5 times with 1 second delays if creation fails
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
                    // Check if quest already exists
                    var existingQuest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Mix") as CustomSynthesisQuest;
                    if (existingQuest != null)
                    {
                        seedDropoff = existingQuest;
                        IsWaitingForDropoff = true;
                        yield break; // Success - exit coroutine
                    }

                    // Try to create the quest
                    seedDropoff = S1API.Quests.QuestManager.CreateQuest<WeedSynthesisQuest>() as CustomSynthesisQuest;
                    seedDropoff?.SetDrugType(EDrugType.Marijuana);

                    if (seedDropoff != null)
                    {
                        IsWaitingForDropoff = true;
                        yield break; // Success - exit coroutine
                    }
                }
                catch
                {
                    // Silently fail - components are still initializing
                }

                // Wait 1 second before next retry
                if (attemptCount < maxRetries)
                {
                    yield return new UnityEngine.WaitForSeconds(1f);
                }
            }

            // All retries failed
            Utility.Error($"[CreateQuestAsync] Failed to Load Quest after {maxRetries} attempts");
        }

        public static void CompleteQuest()
        {
            if (seedDropoff != null)
            {
                seedDropoff.Complete();
                seedDropoff = null;
            }
            IsWaitingForDropoff = false;
        }

        public static void SendMessage(string text)
        {
            ConversationManager.SendMessage("Albert", text);
        }

        public static void OnSelected()
        {
            MSGConversation convo = ConversationManager.GetConversation("Albert");
            if (convo != null)
            {
                convo.senderInterface.SetVisibility(MessageSenderInterface.EVisibility.Docked);
            }
        }
    }
}
