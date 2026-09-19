using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;


#if IL2CPP
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.ItemFramework;
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.Seeds
{
    /// <summary>
    /// Legacy DTO for deserializing old DiscoveredCustomSeeds.json files.
    /// Only used during migration in PersistencePatches.
    /// </summary>
    [Serializable]
    internal class LegacySeedData
    {
        public string seedId { get; set; }
        public string weedId { get; set; }
        public string baseSeedId { get; set; }
        public float price { get; set; }
    }

    /// <summary>
    /// Legacy DTO for the current pre-release UnicornSeedData format.
    /// This is the single migration source for the released variants-based format.
    /// </summary>
    [Serializable]
    internal class LegacyUnicornSeedData
    {
        public string seedId { get; set; }
        public string mixId { get; set; }
        public EDrugType drugType { get; set; }
        public float price { get; set; }
    }

    [Serializable]
    public class VariantSeedData
    {
        public string baseItemId { get; set; }
        public string seedId { get; set; }
        public float price { get; set; }
    }

    [Serializable]
    public class UnicornSeedData
    {
        public string mixId { get; set; }
        public EDrugType drugType { get; set; }
        public List<VariantSeedData> variants { get; set; } = new List<VariantSeedData>();

        [JsonIgnore]
        public string seedId => variants.FirstOrDefault()?.seedId;

        [JsonIgnore]
        public float price => variants.FirstOrDefault()?.price ?? 0f;

        public VariantSeedData GetVariant(string baseItemId)
        {
            return variants.FirstOrDefault(v => v.baseItemId == baseItemId);
        }

        public void SetSingleVariant(string baseItemId, string itemSeedId, float variantPrice)
        {
            variants = new List<VariantSeedData>
            {
                new VariantSeedData
                {
                    baseItemId = baseItemId,
                    seedId = itemSeedId,
                    price = variantPrice,
                }
            };
        }
    }
}
