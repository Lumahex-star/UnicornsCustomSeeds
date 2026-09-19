using S1API.Quests;
using S1API.Saveables;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;



#if IL2CPP
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Economy;
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.SeedQuests
{
    /// <summary>
    /// Shared base for the four per-drug synthesis quests. Holds only plumbing that is
    /// genuinely identical across drugs (save field, dropoff entry construction); every
    /// drug-specific value is an abstract member implemented as a constant by the
    /// subclasses in SynthesisQuestVariants.cs.
    ///
    /// WHY NOT ONE GENERIC CLASS DRIVEN BY _data.drugType:
    /// S1API reads quest properties at points the mod does not control, and some of those
    /// reads happen before any post-construction setter can run:
    ///
    ///   - QuestIcon is read ONLY inside the Quest constructor (v3.0.0 Quest.cs:128, :154)
    ///     and baked into IconPrefab / the POI prefab. Never re-read.
    ///   - Title/Description are read in the constructor too, then re-read later by
    ///     CreateInternal() -> InitializeQuest(), so they *sometimes* self-correct.
    ///   - Quest entry text is baked by AddEntry() during OnCreated/OnLoaded.
    ///
    /// The previous design set the drug type via SetDrugType() AFTER CreateQuest<T>()
    /// returned, so every one of those reads saw the field's initializer and rendered the
    /// quest as weed/Albert. Subclassing fixed the icon (a constant needs no instance
    /// state) but the drugType-derived title/description/NPC stayed wrong.
    ///
    /// Constants resolved through virtual dispatch have no ordering hazard at all — C#
    /// dispatches to the derived override even when called from a base constructor — so
    /// every drug-specific member below is abstract rather than a switch on saved state.
    /// </summary>
    public abstract class CustomSynthesisQuest : Quest
    {
        // Retained for save-schema stability only. Nothing displayed is derived from it;
        // see class remarks. Kept so existing SynthesisQuestData.json files continue to
        // load and write unchanged rather than forcing another save migration.
        [SaveableField("SynthesisQuestData")]
        private CustomSynthesisQuestData _data = new CustomSynthesisQuestData();

        public QuestEntry dropoffEntry;

        /// <summary>Supplier's display name, e.g. "Albert".</summary>
        protected abstract string NpcName { get; }

        /// <summary>Mix noun used in copy, e.g. "weed mix".</summary>
        protected abstract string ItemName { get; }

        /// <summary>That supplier's stash, resolved lazily — may be null early in load.</summary>
        protected abstract SupplierStash Stash { get; }

        protected override string Description =>
            $"Take {StashManager.StashQtyEntry.Value}x of your {ItemName} and ${StashManager.StashCostEntry.Value} to {NpcName}'s supply stash";

        /// <summary>
        /// Retained so the quest managers compile unchanged and SynthesisQuestData.json
        /// keeps round-tripping. The value no longer drives any displayed property — the
        /// concrete subclass does.
        /// </summary>
        public void SetDrugType(EDrugType drugType) => _data.drugType = drugType;

        protected override void OnCreated()
        {
            if (QuestEntries.Count == 0)
                AddDropoffEntry();
        }

        protected override void OnLoaded()
        {
            if (QuestEntries.Count == 0)
                AddDropoffEntry();
        }

        private void AddDropoffEntry()
        {
            // Stash is resolved lazily and is not guaranteed to exist yet — on a joining
            // client this runs before the stash scan completes. Fall back to Vector3.zero
            // rather than dereferencing null.
            SupplierStash stash = Stash;
            Vector3 poi = stash != null ? stash.transform.position : Vector3.zero;

            dropoffEntry = AddEntry(
                $"Give {NpcName} {StashManager.StashQtyEntry.Value}x of a {ItemName} and ${StashManager.StashCostEntry.Value}",
                poiPosition: poi);

            Utility.Log($"[{GetType().Name}] dropoff entry added (npc={NpcName}, stash={(stash != null ? "found" : "null")}).");
        }
    }
}
