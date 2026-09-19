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
    public static class ShroomQuestManager
    {
        public static CustomSynthesisQuest shroomDropoff;
        public static string sendableMessageId = "Synthesize Shrooms";
        public static bool IsWaitingForDropoff = false;
        public static bool HasActiveQuest => IsWaitingForDropoff;

        private static float lastSentTime = 0f;

        public static void Init()
        {
            var quest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Shroom Mix") as CustomSynthesisQuest;
            if (quest != null)
            {
                shroomDropoff = quest;
                IsWaitingForDropoff = true;
            }
            else
            {
                IsWaitingForDropoff = false;
            }

            MSGConversation convo = ConversationManager.GetConversation("Phil");
            if (convo != null)
            {
                SendableMessage sendable = convo.CreateSendableMessage(sendableMessageId);
                sendable.onSent += (Action)OnSent;
            }
        }

        public static void OnSent()
        {
            // Diagnostic bracketing: the shroom quest was appearing on the client but
            // not the host. onSent fires on BOTH machines (SendableMessage.Send routes
            // through MessagingManager, and every machine re-runs Send(network:false)),
            // so the host must reach the create path here. Remove once confirmed.
            Utility.Log($"[ShroomQuestManager.OnSent] fired. IsServer={InstanceFinder.IsServer}, IsClientOnly={InstanceFinder.IsClientOnly}, shroomDropoff={(shroomDropoff == null ? "null" : "set")}");

            if (!InstanceFinder.IsServer) return;
            if (Time.time - lastSentTime < 1f)
            {
                Utility.Log("[ShroomQuestManager.OnSent] skipped: debounce (<1s since last send).");
                return;
            }
            lastSentTime = Time.time;

            List<string> messages = new List<string> { "Drop the shroom mix and cash in my drop box." };
            ConversationManager.SendMessageChain("Phil", messages);

            IsWaitingForDropoff = true;

            if (shroomDropoff == null)
            {
                Utility.Log("[ShroomQuestManager.OnSent] broadcasting + creating quest on host...");
                NetworkSyncManager.BroadcastQuestConfig(EDrugType.Shrooms);
                shroomDropoff = S1API.Quests.QuestManager.CreateQuest<ShroomSynthesisQuest>() as CustomSynthesisQuest;
                shroomDropoff?.SetDrugType(EDrugType.Shrooms);
                Utility.Log($"[ShroomQuestManager.OnSent] host create done, shroomDropoff={(shroomDropoff == null ? "NULL — CreateQuest failed" : "ok")}");
            }
            else
            {
                Utility.Log("[ShroomQuestManager.OnSent] skipped create: shroomDropoff already set.");
            }
        }

        /// <summary>
        /// Asynchronously creates a CustomSynthesisQuest with retry logic, mirroring
        /// SeedQuestManager.CreateQuestAsync. Called on a client on receipt of a
        /// [NET-QUEST] broadcast for Shrooms.
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
                    var existingQuest = S1API.Quests.QuestManager.GetQuestByName("Drop off the Shroom Mix") as CustomSynthesisQuest;
                    if (existingQuest != null)
                    {
                        shroomDropoff = existingQuest;
                        IsWaitingForDropoff = true;
                        yield break;
                    }

                    shroomDropoff = S1API.Quests.QuestManager.CreateQuest<ShroomSynthesisQuest>() as CustomSynthesisQuest;
                    shroomDropoff?.SetDrugType(EDrugType.Shrooms);

                    if (shroomDropoff != null)
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

            Utility.Error($"[ShroomQuestManager.CreateQuestAsync] Failed to load quest after {maxRetries} attempts");
        }

        public static void CompleteQuest()
        {
            if (shroomDropoff != null)
            {
                shroomDropoff.Complete();
                shroomDropoff = null;
            }
            IsWaitingForDropoff = false;
        }

        public static void SendMessage(string text) => ConversationManager.SendMessage("Phil", text);
    }
}
