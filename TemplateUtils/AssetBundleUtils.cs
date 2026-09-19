using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
#if IL2CPP
using AssetBundle = UnityEngine.Il2CppAssetBundle;
#elif MONO
using AssetBundle = UnityEngine.AssetBundle;
#endif

namespace UnicornsCustomSeeds.TemplateUtils
{
    public static class AssetBundleUtils
    {
        static Core mod;
        static MelonAssembly melonAssembly;
        static Dictionary<string, AssetBundle> assetBundles = new Dictionary<string, AssetBundle>();

        static string DataFolderName = "UnicornsCanBundle";
        static string ModFolderName = "UnicornsCustomSeeds";

#if IL2CPP
        private static string GetBundleCachePath(string bundleFileName)
        {
            string cacheDir = Path.Combine(MelonEnvironment.UserDataDirectory, DataFolderName, ModFolderName);
            Directory.CreateDirectory(cacheDir);

            return Path.Combine(cacheDir, $"{bundleFileName}_{BuildInfo.Version}");
        }

        private static byte[] ReadEmbeddedBundleBytes(string bundleFileName)
        {
            string streamPath = $"{typeof(Core).Namespace}.Assets.{bundleFileName}";
            using Stream bundleStream = melonAssembly.Assembly.GetManifestResourceStream(streamPath);
            if (bundleStream == null)
            {
                MelonLogger.Error($"AssetBundle resource '{streamPath}' not found. Check EmbeddedResource entry for '{bundleFileName}'.");
                return null;
            }

            using MemoryStream ms = new MemoryStream();
            bundleStream.CopyTo(ms);
            byte[] bundleData = ms.ToArray();

            if (bundleData.Length == 0)
            {
                MelonLogger.Error($"AssetBundle '{bundleFileName}' is empty.");
                return null;
            }

            return bundleData;
        }

        public static bool EnsureBundleCached(string bundleFileName)
        {
            string bundlePath = GetBundleCachePath(bundleFileName);
            if (File.Exists(bundlePath))
            {
                return true;
            }

            byte[] bundleData = ReadEmbeddedBundleBytes(bundleFileName);
            if (bundleData == null)
            {
                return false;
            }

            try
            {
                File.WriteAllBytes(bundlePath, bundleData);
                MelonLogger.Msg($"Cached AssetBundle '{bundleFileName}' to '{bundlePath}'.");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed writing AssetBundle cache '{bundleFileName}': {ex}");
                return false;
            }
        }

        private static AssetBundle LoadBundleFromFile(string bundleFileName)
        {
            string bundlePath = GetBundleCachePath(bundleFileName);
            try
            {
                AssetBundle bundle = Il2CppAssetBundleManager.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    MelonLogger.Error($"LoadFromFile returned null for bundle '{bundleFileName}' at '{bundlePath}'.");
                }

                return bundle;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"LoadFromFile failed for bundle '{bundleFileName}' at '{bundlePath}': {ex}");
                return null;
            }
        }
#endif

        public static void Initialize(Core coreMod)
        {
            mod = coreMod;
            melonAssembly = mod.MelonAssembly;
        }

        public static AssetBundle LoadAssetBundle(string bundleFileName)
        {
            if (assetBundles.TryGetValue(bundleFileName, out AssetBundle loadedBundle) && loadedBundle != null)
            {
                return loadedBundle;
            }

            try
            {
#if IL2CPP
                string bundlePath = GetBundleCachePath(bundleFileName);
                AssetBundle ab;

                if (File.Exists(bundlePath))
                {
                    ab = LoadBundleFromFile(bundleFileName);
                }
                else
                {
                    byte[] bundleData = ReadEmbeddedBundleBytes(bundleFileName);
                    if (bundleData == null)
                    {
                        mod.Unregister($"Failed to read required AssetBundle '{bundleFileName}' from embedded resources.");
                        return null;
                    }

                    try
                    {
                        AssetBundle memoryBundle = Il2CppAssetBundleManager.LoadFromMemory(bundleData);
                        if (memoryBundle == null)
                        {
                            mod.Unregister($"Failed to validate required AssetBundle '{bundleFileName}' from memory.");
                            return null;
                        }

                        try
                        {
                            File.WriteAllBytes(bundlePath, bundleData);
                            MelonLogger.Msg($"Cached AssetBundle '{bundleFileName}' to '{bundlePath}'.");
                        }
                        catch (Exception writeEx)
                        {
                            try
                            {
                                memoryBundle.Unload(false);
                            }
                            catch
                            {
                            }

                            mod.Unregister($"Failed writing required AssetBundle '{bundleFileName}' to cache: {writeEx}");
                            return null;
                        }

                        try
                        {
                            memoryBundle.Unload(false);
                        }
                        catch (Exception unloadEx)
                        {
                            MelonLogger.Warning($"Failed to unload memory validation bundle '{bundleFileName}': {unloadEx.Message}");
                        }

                        ab = LoadBundleFromFile(bundleFileName);
                    }
                    catch (Exception memoryEx)
                    {
                        mod.Unregister($"Failed to validate required AssetBundle '{bundleFileName}' from memory: {memoryEx}");
                        return null;
                    }
                }
#elif MONO
                string streamPath = $"{typeof(Core).Namespace}.Assets.{bundleFileName}";
                Stream bundleStream = melonAssembly.Assembly.GetManifestResourceStream($"{streamPath}");
                if (bundleStream == null)
                {
                    mod.Unregister($"AssetBundle resource '{streamPath}' not found.");
                    return null;
                }

                byte[] bundleData;
                using (bundleStream)
                using (MemoryStream ms = new MemoryStream())
                {
                    bundleStream.CopyTo(ms);
                    bundleData = ms.ToArray();
                }

                if (bundleData.Length == 0)
                {
                    mod.Unregister($"AssetBundle '{bundleFileName}' is empty.");
                    return null;
                }

                AssetBundle ab = AssetBundle.LoadFromMemory(bundleData);
#endif

                if (ab == null)
                {
                    mod.Unregister($"Failed to load required AssetBundle '{bundleFileName}'.");
                    return null;
                }

                assetBundles[bundleFileName] = ab;
                return ab;
            }
            catch (Exception e)
            {
                mod.Unregister($"Failed to load required AssetBundle '{bundleFileName}': {e}");
                return null;
            }
        }

        public static void ClearCache()
        {
            assetBundles.Clear();
        }

        public static AssetBundle GetLoadedAssetBundle(string bundleName)
        {
            if (assetBundles.ContainsKey(bundleName))
            {
                return assetBundles[bundleName];
            }
            else
            {
                MelonLogger.Warning($"Asset bundle '{bundleName}' is not loaded.");
                return null;
            }
        }

        public static T LoadAssetFromBundle<T>(string assetName, string bundleName) where T : UnityEngine.Object
        {
            var bundle = GetLoadedAssetBundle(bundleName);
            if (bundle == null)
            {
                MelonLogger.Error($"Couldn't find loaded bundle '{bundleName}'.");
                return null;
            }

            T asset = null;
            try
            {
                asset = bundle.LoadAsset<T>(assetName);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed loading asset '{assetName}' from bundle '{bundleName}'. The bundle may be invalid or have a zero internal pointer. {ex}");
                return null;
            }

            if (asset == null)
            {
                MelonLogger.Error($"Asset '{assetName}' not found in bundle '{bundleName}'.");
                return null;
            }

            return asset;
        }

        // Loads the first Texture2D in the bundle whose (lowercased) asset path contains ALL of the
        // given substrings. Handles path/case/extension differences (e.g. "asphalt","diff" matches
        // "assets/assebundle/road_asphalt1_diff.png"). Returns null if none match.
        public static Texture2D LoadTextureContaining(string bundleName, params string[] contains)
        {
            var bundle = GetLoadedAssetBundle(bundleName);
            if (bundle == null) return null;

            var names = bundle.GetAllAssetNames();
            for (int i = 0; i < names.Length; i++)
            {
                string lower = names[i].ToLowerInvariant();
                bool all = true;
                for (int c = 0; c < contains.Length; c++)
                {
                    if (!lower.Contains(contains[c].ToLowerInvariant())) { all = false; break; }
                }
                if (!all) continue;

                try
                {
                    var tex = bundle.LoadAsset<Texture2D>(names[i]);
                    if (tex != null) return tex;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Failed loading texture '{names[i]}' from bundle '{bundleName}': {ex.Message}");
                }
            }
            return null;
        }
    }
}