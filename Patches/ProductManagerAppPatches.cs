using HarmonyLib;
using UnicornsCustomSeeds.Managers;
using UnityEngine;
using UnityEngine.UI;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne.UI.Phone.ProductManagerApp;
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.UI.Phone.ProductManagerApp;
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.Patches
{
    public class ProductManagerAppPatches
    {
        private static List<GameObject> pendingIndicators = new List<GameObject>();
        private static bool isPrefabInitialized = false;

        [HarmonyPatch(typeof(ProductManagerApp))]
        public static class ProductManagerApp_Patch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(ProductManagerApp.Start))]
            public static bool StartPatch(ProductManagerApp __instance)
            {
                if (__instance == null || __instance.EntryPrefab == null)
                    return true;

                if (__instance.EntryPrefab.transform.Find("SeedIndicator") != null)
                {
                    isPrefabInitialized = true;
                    return true;
                }

                if (!isPrefabInitialized)
                {
                    var parent = new GameObject("Temp");
                    parent.SetActive(false);
                    var newPrefab = UnityEngine.Object.Instantiate<GameObject>(__instance.EntryPrefab, parent.transform);
                    ProductEntry entry = __instance.EntryPrefab.GetComponent<ProductEntry>();
                    var favouriteButton = UnityEngine.Object.Instantiate<GameObject>(entry.FavouriteButton.gameObject, newPrefab.transform);
                    Button temp = favouriteButton.GetComponent<Button>();
                    GameObject.Destroy(temp);
                    favouriteButton.name = "SeedIndicator";
                    RectTransform indicatorRect = favouriteButton.GetComponent<RectTransform>();
                    if (indicatorRect != null)
                    {
                        indicatorRect.anchoredPosition = new Vector2(15, -15);
                        indicatorRect.anchorMax = new Vector2(0, 1);
                        indicatorRect.anchorMin = new Vector2(0, 1);
                    }

                    // Set the sprite if available, otherwise mark for later
                    TrySetSeedIconSprite(favouriteButton);

                    __instance.EntryPrefab = newPrefab;
                    isPrefabInitialized = true;
                }

                return true;
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(ProductManagerApp.Start))]
            public static void StartPostfix(ProductManagerApp __instance)
            {
                // After Start completes, update any pending indicators
                UpdatePendingIndicators();
            }
        }

        [HarmonyPatch(typeof(ProductEntry), nameof(ProductEntry.Initialize))]
        public static class ProductEntry_Initialize_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(ProductEntry __instance, ProductDefinition definition)
            {
                if (__instance != null && definition != null)
                {
                    Transform seedIndicator = __instance.transform.Find("SeedIndicator");
                    if (seedIndicator != null)
                    {
                        // Ensure the sprite is set (handles cases where prefab wasn't updated)
                        TrySetSeedIconSprite(seedIndicator.gameObject);

                        if (CustomSeedsManager.DiscoveredSeeds.ContainsKey(definition.ID))
                        {
                            seedIndicator.gameObject.SetActive(true);
                        }
                        else
                        {
                            seedIndicator.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        private static void TrySetSeedIconSprite(GameObject indicatorObject)
        {
            try
            {
                // Ensure sprite is loaded
                if (SeedVisualsManager.baseQuestIconSprite == null)
                {
                    SeedVisualsManager.LoadSeedMaterial();
                }

                Image labelImage = indicatorObject.transform.GetChild(0).GetComponent<Image>();

                if (labelImage != null)
                {
                    if (SeedVisualsManager.baseQuestIconSprite != null)
                    {
                        labelImage.sprite = SeedVisualsManager.baseQuestIconSprite;
                        labelImage.color = Color.white;
                    }
                    else
                    {
                        // Add to pending list to retry later
                        if (!pendingIndicators.Contains(indicatorObject))
                        {
                            pendingIndicators.Add(indicatorObject);
                            Utility.Log($"Seed icon not loaded yet, added to pending list");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
            }
        }

        private static void UpdatePendingIndicators()
        {
            if (pendingIndicators.Count == 0) return;

            for (int i = pendingIndicators.Count - 1; i >= 0; i--)
            {
                GameObject indicator = pendingIndicators[i];
                if (indicator == null)
                {
                    pendingIndicators.RemoveAt(i);
                    continue;
                }

                try
                {
                    Image labelImage = indicator.transform.GetChild(0).GetComponent<Image>();
                    if (labelImage != null && SeedVisualsManager.baseQuestIconSprite != null)
                    {
                        labelImage.sprite = SeedVisualsManager.baseQuestIconSprite;
                        labelImage.color = Color.white;
                        pendingIndicators.RemoveAt(i);
                    }
                }
                catch (Exception e)
                {
                    Utility.PrintException(e);
                    pendingIndicators.RemoveAt(i);
                }
            }
        }

        public static void ClearPendingIndicators()
        {
            pendingIndicators.Clear();
            isPrefabInitialized = false;
        }
    }
}
