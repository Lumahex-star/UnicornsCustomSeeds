#if IL2CPP
using Il2CppScheduleOne.ObjectScripts;
#elif MONO
using ScheduleOne.ObjectScripts;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class DeferredPlantsManager
    {
        public struct DeferredSeedData
        {
            public Pot Pot;
            public string SeedId;
            public float Progress;
        }

        public struct HarvestableUpdateData
        {
            public int Index;
            public bool Active;
        }
        public static bool IsReplaying = false;

        /// <summary>
        /// ID suffixes for seeds that are planted in a Pot and therefore ride the
        /// Pot.PlantSeed_Client RPC. Coca uses the same path as weed, so a joining
        /// client can receive a pot's plant RPC before the seed definition arrives.
        /// Pseudo and shrooms are NOT here — neither is pot-planted.
        /// </summary>
        private static readonly string[] PotPlantedSeedMarkers =
        {
            "customseeddefinition", // weed
            "customcocaseed",       // coca
        };

        /// <summary>True if the ID belongs to a custom seed planted via a Pot.</summary>
        public static bool IsPotPlantedCustomSeed(string seedId)
        {
            if (string.IsNullOrEmpty(seedId)) return false;
            foreach (string marker in PotPlantedSeedMarkers)
                if (seedId.Contains(marker)) return true;
            return false;
        }

        public static HashSet<string> PendingPotGuids = new HashSet<string>();
        public static Dictionary<string, List<DeferredSeedData>> seedsToLoad = new Dictionary<string, List<DeferredSeedData>>();
        public static Dictionary<string, List<HarvestableUpdateData>> DeferredHarvestables = new Dictionary<string, List<HarvestableUpdateData>>();

        public static void AddDeferredSeed(Pot pot, string seedId, float progress)
        {
            if (seedsToLoad.ContainsKey(seedId))
            {
                seedsToLoad[seedId].Add(new DeferredSeedData { Pot = pot, SeedId = seedId, Progress = progress });
            }
            else
            {
                List<DeferredSeedData> seedList = new List<DeferredSeedData>();
                seedList.Add(new DeferredSeedData { Pot = pot, SeedId = seedId, Progress = progress });
                seedsToLoad.Add(seedId, seedList);
            }
            PendingPotGuids.Add(pot.GUID.ToString());
        }

        public static void AddHarvestableUpdate(string potGuid, int index, bool active)
        {
            if (!DeferredHarvestables.ContainsKey(potGuid))
            {
                DeferredHarvestables[potGuid] = new List<HarvestableUpdateData>();
            }
            DeferredHarvestables[potGuid].Add(new HarvestableUpdateData { Index = index, Active = active });
        }

        public static void TrySpawnQueuedPlants(string seedId)
        {
            if (seedsToLoad.ContainsKey(seedId))
            {
                var potsToQueue = seedsToLoad[seedId];
                foreach (var data in potsToQueue)
                {
                    data.Pot.PlantSeed_Client(null, data.SeedId, data.Progress);

                    string potGuid = data.Pot.GUID.ToString();
                    if (DeferredHarvestables.ContainsKey(potGuid))
                    {
                        foreach (var update in DeferredHarvestables[potGuid])
                        {
                            data.Pot.Plant.SetHarvestableActive(update.Index, update.Active);
                        }
                        DeferredHarvestables.Remove(potGuid);
                    }
                    PendingPotGuids.Remove(potGuid);
                }
            }
        }
    }
}
