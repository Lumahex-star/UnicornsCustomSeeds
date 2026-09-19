using UnicornsCustomSeeds.Managers;
using UnityEngine;

#if IL2CPP
using Il2CppScheduleOne.Economy;
#elif MONO
using ScheduleOne.Economy;
#endif

namespace UnicornsCustomSeeds.SeedQuests
{
    // ─────────────────────────────────────────────────────────────────────────
    // One concrete quest class per drug. Deliberately duplicated rather than
    // parameterised: every member here is a constant, so S1API can read any of them at
    // any point in the quest lifecycle (including inside the base constructor) and get
    // the right answer. See CustomSynthesisQuest's remarks for why the previous
    // drugType-driven design could not work.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Weed synthesis quest — Albert Hoover.</summary>
    public class WeedSynthesisQuest : CustomSynthesisQuest
    {
        protected override string Title => "Drop off the Mix";
        protected override string NpcName => "Albert";
        protected override string ItemName => "weed mix";
        protected override SupplierStash Stash => StashManager.GetSupplierStash();
        protected override Sprite QuestIcon =>
            SeedVisualsManager.weedQuestIconSprite ?? SeedVisualsManager.baseQuestIconSprite;
    }

    /// <summary>Shroom synthesis quest — Fungal Phil.</summary>
    public class ShroomSynthesisQuest : CustomSynthesisQuest
    {
        protected override string Title => "Drop off the Shroom Mix";
        protected override string NpcName => "Phil";
        protected override string ItemName => "shroom mix";
        protected override SupplierStash Stash => PhilStashManager.GetSupplierStash();
        protected override Sprite QuestIcon =>
            SeedVisualsManager.shroomQuestIconSprite ?? SeedVisualsManager.baseQuestIconSprite;
    }

    /// <summary>Coca synthesis quest — Salvador.</summary>
    public class CocaSynthesisQuest : CustomSynthesisQuest
    {
        protected override string Title => "Drop off the Cocaine Mix";
        protected override string NpcName => "Salvador";
        protected override string ItemName => "cocaine mix";
        protected override SupplierStash Stash => SalvadorStashManager.GetSupplierStash();
        protected override Sprite QuestIcon =>
            SeedVisualsManager.cocaineQuestIconSprite ?? SeedVisualsManager.baseQuestIconSprite;
    }

    /// <summary>Pseudo/meth synthesis quest — Shirley.</summary>
    public class PseudoSynthesisQuest : CustomSynthesisQuest
    {
        protected override string Title => "Drop off the Meth Mix";
        protected override string NpcName => "Shirley";
        protected override string ItemName => "meth mix";
        protected override SupplierStash Stash => ShirleyStashManager.GetSupplierStash();
        protected override Sprite QuestIcon =>
            SeedVisualsManager.methQuestIconSprite ?? SeedVisualsManager.baseQuestIconSprite;
    }
}
