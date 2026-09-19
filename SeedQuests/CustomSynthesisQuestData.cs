using System;

#if IL2CPP
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.SeedQuests
{
    [Serializable]
    public class CustomSynthesisQuestData
    {
        public EDrugType drugType { get; set; } = EDrugType.Marijuana;
    }
}
