using System.Collections;
using UnityEngine;
using MelonLoader;
using UnicornsCustomSeeds.TemplateUtils;


#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.NPCs.Relation;
#elif MONO
using FishNet;
using ScheduleOne.Dialogue;
using ScheduleOne.Messaging;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.NPCs.Relation;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class ConversationManager
    {
        public static Dictionary<string, MSGConversation> Conversations = new Dictionary<string, MSGConversation>();
        public static Albert albert;
        public static NPCRelationData albertRelation;
        public static string welcomeMessage = "A <color='#FA2FBD'><b>Unicorn</b></color> I know can synthesize your custom mixes into seeds. If you'd like to use that service, send me a text.";

        public const string PhilWelcomeMessage = "A <color='#FA2FBD'><b>Unicorn</b></color> I know can synthesize your custom shroom mixes into spore syringes. If you'd like to use that service, send me a text.";
        public const string SalvadorWelcomeMessage = "A <color='#FA2FBD'><b>Unicorn</b></color> I know can synthesize your custom cocaine mixes into coca seeds. If you'd like to use that service, send me a text.";
        public const string ShirleyWelcomeMessage = "A <color='#FA2FBD'><b>Unicorn</b></color> I know can synthesize your custom meth mixes into pseudo. If you'd like to use that service, send me a text.";

        public static void Init()
        {
            // Find Albert
            albert = GameObject.FindObjectOfType<Albert>();
            if (albert != null)
            {
                if (albert.MSGConversation != null)
                {
                    RegisterConversation("Albert", albert.MSGConversation);
                    albertRelation = albert.RelationData;
                }

                if (CustomSeedsManager.FirstLoad && albert.RelationData != null)
                {
                    if (albert.RelationData.RelationDelta >= 4f)
                    {
                        MelonCoroutines.Start(WelcomeRoutine());
                    }
                    else
                    {
                        DialogueDatabase db = albert.DialogueHandler.Database;
                        var generic = db.GetModule(EDialogueModule.Generic);
                        for (var i = 0; i < generic.Entries.Count; i++)
                        {
#if IL2CPP
                            if (generic.Entries[i] != null && generic.Entries[i].Key == "supplier_meetings_unlocked")
#elif MONO
                            if (generic.Entries[i].Key == "supplier_meetings_unlocked")
#endif
                            {
                                Entry supplierEntry = generic.Entries[i];

                                DialogueChain chain = supplierEntry.Chains[0];
                                if (chain != null && chain.Lines[chain.Lines.Length - 1] != welcomeMessage)
                                {
                                    string[] swap = chain.Lines;
                                    string[] newLines = new string[swap.Length + 1];
                                    newLines[0] = swap[0];
                                    newLines[1] = swap[1];
                                    newLines[2] = welcomeMessage;
                                    chain.Lines = newLines;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                Utility.Error("ConversationManager: Could not find Albert or his conversation.");
            }
        }

        /// <summary>
        /// Same proactive "you can synthesize" announcement Albert gets (see the tail of
        /// Init() above), generalized so Phil, Salvador and Shirley can use it too: if the
        /// relationship is already unlocked, send the welcome text after a short delay;
        /// otherwise inject it as an extra line onto the NPC's own "supplier_meetings_unlocked"
        /// dialogue entry so it's delivered as part of that unlock conversation whenever the
        /// player reaches it normally. Silently does nothing if that entry isn't found on a
        /// given NPC's dialogue database.
        /// </summary>
        public static void InitSupplierWelcome(string npcName, NPCRelationData relationData, DialogueHandler dialogueHandler, string welcomeText)
        {
            if (!CustomSeedsManager.FirstLoad || relationData == null) return;

            if (relationData.RelationDelta >= 4f)
            {
                MelonCoroutines.Start(GenericWelcomeRoutine(npcName, welcomeText));
            }
            else
            {
                InjectWelcomeIntoUnlockDialogue(dialogueHandler, welcomeText);
            }
        }

        private static void InjectWelcomeIntoUnlockDialogue(DialogueHandler dialogueHandler, string welcomeText)
        {
            DialogueDatabase db = dialogueHandler?.Database;
            if (db == null) return;

            var generic = db.GetModule(EDialogueModule.Generic);
            for (var i = 0; i < generic.Entries.Count; i++)
            {
#if IL2CPP
                if (generic.Entries[i] != null && generic.Entries[i].Key == "supplier_meetings_unlocked")
#elif MONO
                if (generic.Entries[i].Key == "supplier_meetings_unlocked")
#endif
                {
                    Entry supplierEntry = generic.Entries[i];

                    DialogueChain chain = supplierEntry.Chains[0];
                    if (chain != null && chain.Lines[chain.Lines.Length - 1] != welcomeText)
                    {
                        string[] swap = chain.Lines;
                        string[] newLines = new string[swap.Length + 1];
                        newLines[0] = swap[0];
                        newLines[1] = swap[1];
                        newLines[2] = welcomeText;
                        chain.Lines = newLines;
                    }
                }
            }
        }

        private static IEnumerator GenericWelcomeRoutine(string npcName, string welcomeText)
        {
            yield return new WaitForSeconds(10f);
            SendMessage(npcName, welcomeText);
        }

        public static void RegisterConversation(string name, MSGConversation convo)
        {
            if (!Conversations.ContainsKey(name))
            {
                Conversations.Add(name, convo);
            }
            else
            {
                Conversations[name] = convo; // Update if exists
            }
        }

        public static MSGConversation GetConversation(string name)
        {
            if (Conversations.TryGetValue(name, out var convo))
            {
                return convo;
            }
            return null;
        }

        public static void SendMessage(string characterName, string text)
        {
            if (!InstanceFinder.IsServer) return; // Only send messages from the server to ensure they are properly synced to clients

            if (Conversations.TryGetValue(characterName, out var convo))
            {
                if (convo != null)
                {
                    convo.SendMessage(new Message(text, Message.ESenderType.Other, true, -1), true, true);
                }
            }
            else
            {
                Utility.Error($"ConversationManager: Conversation for {characterName} not found.");
            }
        }

        public static void SendMessageChain(string characterName, System.Collections.Generic.List<string> messages)
        {
            if (!InstanceFinder.IsServer) return; // Only send messages from the server to ensure they are properly synced to clients
            if (Conversations.TryGetValue(characterName, out var convo))
            {
                if (convo != null)
                {
                    MessageChain messageChain = new MessageChain();
                    foreach (var msg in messages)
                    {
                        messageChain.Messages.Add(msg);
                    }
                    messageChain.id = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    convo.SendMessageChain(messageChain, 0.5f, true, true);
                }
            }
            else
            {
                Utility.Error($"Conversation for {characterName} not found.");
            }
        }

        public static void AlbertWelcomeMessage(NPCRelationData.EUnlockType type, bool temp)
        {
            SendMessage("Albert", welcomeMessage);
        }

        private static IEnumerator WelcomeRoutine()
        {
            yield return new WaitForSeconds(10f);
            AlbertWelcomeMessage(NPCRelationData.EUnlockType.DirectApproach, true);
        }
    }
}
