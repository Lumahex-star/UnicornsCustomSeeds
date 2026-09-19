using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;

#if IL2CPP
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class SeedVisualsManager
    {
        public static Dictionary<string, Sprite> seedIcons = new Dictionary<string, Sprite>();
        public static Dictionary<string, WeedAppearanceSettings> appearanceMap = new Dictionary<string, WeedAppearanceSettings>();

        public static Shader customShader;
        public static Material customMat;
        public static Sprite baseSeedSprite;
        public static Sprite baseShroomSpawnSprite;
        public static Sprite baseSyringeSprite;
        public static Sprite basePseudoSprite;
        public static Sprite baseLiquidMethSprite;
        public static Sprite baseCocaLeafSprite;
        public static Sprite baseCocaBaseSprite;
        public static Sprite baseQuestIconSprite;
        public static Sprite weedQuestIconSprite;
        public static Sprite methQuestIconSprite;
        public static Sprite shroomQuestIconSprite;
        public static Sprite cocaineQuestIconSprite;

        public enum BlendMode { Lerp, Multiply, Add, Screen }
        public static BlendMode blendMode = BlendMode.Lerp;
        public static Rect gradientArea01 = new Rect(0.37f, 0.312f, 0.24f, 0.38f);
        public static float gradientOpacity = 1f;

        public static void LoadSeedMaterial()
        {
            try
            {
                var bundle = AssetBundleUtils.LoadAssetBundle("customshaders");
                //if (bundle != null)
                //{
                //    var assetNames = bundle.GetAllAssetNames();
                //    Utility.Log($"[SeedVisualsManager] 'customshaders' bundle contains {assetNames.Length} asset(s):");
                //    foreach (var assetName in assetNames)
                //    {
                //        Utility.Log($"  - {assetName}");
                //    }
                //}

                Sprite BaseSeedIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("customseedicon.png", "customshaders");

                if (BaseSeedIconSprite != null)
                {
                    baseSeedSprite = BaseSeedIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseSeedSprite);
                }

                Sprite BaseShroomSpawnIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("shroomspawnicon.png", "customshaders");

                if (BaseShroomSpawnIconSprite != null)
                {
                    baseShroomSpawnSprite = BaseShroomSpawnIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseShroomSpawnSprite);
                }

                Sprite BaseSyringeIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("syringeicon.png", "customshaders");

                if (BaseSyringeIconSprite != null)
                {
                    baseSyringeSprite = BaseSyringeIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseSyringeSprite);
                }

                Sprite BasePseudoIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("pseduoicon.png", "customshaders");

                if (BasePseudoIconSprite != null)
                {
                    basePseudoSprite = BasePseudoIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(basePseudoSprite);
                }

                Sprite BaseLiquidMethIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("liquidmethicon.png", "customshaders");

                if (BaseLiquidMethIconSprite != null)
                {
                    baseLiquidMethSprite = BaseLiquidMethIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseLiquidMethSprite);
                }

                Sprite BaseCocaLeafIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("cocaleaficon.png", "customshaders");

                if (BaseCocaLeafIconSprite != null)
                {
                    baseCocaLeafSprite = BaseCocaLeafIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseCocaLeafSprite);
                }

                Sprite BaseCocaBaseIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("cocaainebaseicon.png", "customshaders");

                if (BaseCocaBaseIconSprite != null)
                {
                    baseCocaBaseSprite = BaseCocaBaseIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseCocaBaseSprite);
                }

                Sprite BaseQuestIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("basequest_icon.png", "customshaders");

                if (BaseQuestIconSprite != null)
                {
                    baseQuestIconSprite = BaseQuestIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseQuestIconSprite);
                }

                Sprite WeedQuestSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("weedquest_icon.png", "customshaders");

                if (WeedQuestSprite != null)
                {
                    weedQuestIconSprite = WeedQuestSprite;
                    UnityEngine.Object.DontDestroyOnLoad(weedQuestIconSprite);
                }

                Sprite MethQuestSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("methquest_icon.png", "customshaders");

                if (MethQuestSprite != null)
                {
                    methQuestIconSprite = MethQuestSprite;
                    UnityEngine.Object.DontDestroyOnLoad(methQuestIconSprite);
                }

                Sprite ShroomQuestSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("shroomquest_icon.png", "customshaders");

                if (ShroomQuestSprite != null)
                {
                    shroomQuestIconSprite = ShroomQuestSprite;
                    UnityEngine.Object.DontDestroyOnLoad(shroomQuestIconSprite);
                }

                Sprite CocaineQuestSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("cocainequest_icon.png", "customshaders");

                if (CocaineQuestSprite != null)
                {
                    cocaineQuestIconSprite = CocaineQuestSprite;
                    UnityEngine.Object.DontDestroyOnLoad(cocaineQuestIconSprite);
                }

                Shader labelGradient = AssetBundleUtils.LoadAssetFromBundle<Shader>("labelgradient.shader", "customshaders");
                if (labelGradient != null)
                {
                    customShader = labelGradient;
                    Material newMat = new Material(customShader);
                    if (newMat != null)
                    {
                        customMat = newMat;
                        UnityEngine.Object.DontDestroyOnLoad(labelGradient);
                    }
                }
                else
                {
                    Utility.Error("Fail");
                }
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
            }
        }

        private static readonly Color TestTopColor = new Color(1f, 0f, 0f, 1f);    // red
        private static readonly Color TestBottomColor = new Color(1f, 1f, 0f, 1f); // yellow

        /// <summary>
        /// Smoke test run at mod load: generates a key-color fill for a base icon and logs
        /// whether it worked, so loading the mod alone confirms both "did the bundle/sprite
        /// load" and "did the fill actually replace the placeholder color" without needing any
        /// factory integration or UI to look at yet.
        /// </summary>

        public static Sprite GenerateSpriteWithGradient(Color topColor, Color bottomColor)
        {
            return GenerateSpriteWithGradient(topColor, bottomColor, 0f);
        }

        /// <summary>
        /// Same as GenerateSpriteWithGradient, but rotates the finished vial by
        /// rotationDegrees (positive = counter-clockwise). The gradient is applied before
        /// rotating so the label stays aligned to the vial. Used to distinguish drug types
        /// that share the same vial art — e.g. coca seeds reuse the weed vial at 45 degrees.
        /// </summary>
        public static Sprite GenerateSpriteWithGradient(Color topColor, Color bottomColor, float rotationDegrees)
        {
            if (baseSeedSprite == null || baseSeedSprite.texture == null)
            {
                Utility.Error("[VialTextureGenerator] Base Sprite is missing.");
                return null;
            }

            Texture2D spriteTexture = baseSeedSprite.texture;

            Rect spriteRect = baseSeedSprite.rect;
            spriteRect.x /= spriteTexture.width;
            spriteRect.y /= spriteTexture.height;
            spriteRect.width /= spriteTexture.width;
            spriteRect.height /= spriteTexture.height;

            Color[] spritePixels = spriteTexture.GetPixels(
              Mathf.FloorToInt(spriteRect.x * spriteTexture.width),
              Mathf.FloorToInt(spriteRect.y * spriteTexture.height),
              Mathf.FloorToInt(spriteRect.width * spriteTexture.width),
              Mathf.FloorToInt(spriteRect.height * spriteTexture.height)
            );

            ApplyVerticalGradientInRect(
                spritePixels,
                Mathf.FloorToInt(spriteRect.width * spriteTexture.width),
                Mathf.FloorToInt(spriteRect.height * spriteTexture.height),
                gradientArea01,
                topColor,
                bottomColor,
                gradientOpacity,
                blendMode
                );

            int newTextureWidth = Mathf.FloorToInt(spriteRect.width * spriteTexture.width);
            int newTextureHeight = Mathf.FloorToInt(spriteRect.height * spriteTexture.height);
            Texture2D copiedTexture = new Texture2D(newTextureWidth, newTextureHeight, TextureFormat.RGBA32, false);
            copiedTexture.SetPixels(spritePixels);
            copiedTexture.Apply();
            copiedTexture.name = "Copy";

            if (!Mathf.Approximately(rotationDegrees, 0f))
            {
                Texture2D unrotated = copiedTexture;
                copiedTexture = RotateTexture(unrotated, rotationDegrees);
                newTextureWidth = copiedTexture.width;
                newTextureHeight = copiedTexture.height;
                // The pre-rotation copy is garbage now, and Unity textures aren't
                // reclaimed by the GC on their own.
                UnityEngine.Object.Destroy(unrotated);
            }

            var sprite = Sprite.Create(copiedTexture, new Rect(0, 0, newTextureWidth, newTextureHeight), new Vector2(0.5f, 0.5f));

            return sprite;
        }

        /// <summary>
        /// Generates a gradient-filled version of any icon sprite (not just the vial). Unlike
        /// GenerateSpriteWithGradient, this preserves the icon's existing solid/opaque pixels
        /// exactly and only fills the transparent gaps — so the fill shape is whatever silhouette
        /// the icon's alpha channel already describes (leaf, crystal, syringe, etc.), no rect
        /// tuning needed. Runs across the full canvas.
        /// </summary>
        public static Sprite GenerateIconWithGradientFill(Sprite baseIcon, Color topColor, Color bottomColor)
        {
            if (baseIcon == null || baseIcon.texture == null)
            {
                Utility.Error("[GradientIconFill] Base icon sprite is missing.");
                return null;
            }

            Texture2D spriteTexture = baseIcon.texture;

            Rect spriteRect = baseIcon.rect;
            spriteRect.x /= spriteTexture.width;
            spriteRect.y /= spriteTexture.height;
            spriteRect.width /= spriteTexture.width;
            spriteRect.height /= spriteTexture.height;

            int width = Mathf.FloorToInt(spriteRect.width * spriteTexture.width);
            int height = Mathf.FloorToInt(spriteRect.height * spriteTexture.height);

            Color[] spritePixels = spriteTexture.GetPixels(
                Mathf.FloorToInt(spriteRect.x * spriteTexture.width),
                Mathf.FloorToInt(spriteRect.y * spriteTexture.height),
                width,
                height
            );

            FillTransparentGaps(spritePixels, width, height, topColor, bottomColor);

            Texture2D copiedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            copiedTexture.SetPixels(spritePixels);
            copiedTexture.Apply();
            copiedTexture.name = baseIcon.name + "_GradientFill";

            return Sprite.Create(copiedTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Fills transparent pixels with a top-to-bottom gradient while leaving existing
        /// solid/opaque pixels untouched — "draw the existing art over a gradient backdrop,"
        /// as opposed to ApplyVerticalGradientInRect's "tint everything toward the gradient."
        /// Runs across the whole texture; the fill shape comes entirely from each pixel's own
        /// alpha, not from any rect.
        /// </summary>
        private static void FillTransparentGaps(Color[] pixels, int width, int height, Color top, Color bottom)
        {
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (float)(height - 1 <= 0 ? 1 : (height - 1));
                Color grad = Color.Lerp(bottom, top, t);

                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = row + x;
                    var dst = pixels[idx];

                    pixels[idx] = new Color(
                        Mathf.Lerp(grad.r, dst.r, dst.a),
                        Mathf.Lerp(grad.g, dst.g, dst.a),
                        Mathf.Lerp(grad.b, dst.b, dst.a),
                        Mathf.Max(dst.a, grad.a));
                }
            }
        }

        // Placeholder color artists paint into an icon to mark "gradient goes here" — a
        // solid-color match is immune to the anti-aliasing/shadow noise that can live in a
        // source PNG's alpha channel, unlike filling by transparency. Set this to whatever hex
        // value the placeholder is actually painted with in the source art (no leading '#').
        public const string FillKeyColorHex = "FF00FF";
        public static readonly Color FillKeyColor = ParseHexColor(FillKeyColorHex);

        private static Color ParseHexColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out Color color))
                return color;

            Utility.Error($"[SeedVisualsManager] Invalid hex color '{hex}', defaulting to magenta.");
            return new Color(1f, 0f, 1f, 1f);
        }

        /// <summary>
        /// Generates a gradient-filled version of any icon sprite by replacing pixels that
        /// match keyColor (within tolerance) with the gradient, leaving every other pixel —
        /// including transparent background and any noisy/soft alpha — completely untouched.
        /// Use this instead of GenerateIconWithGradientFill when the source art's alpha channel
        /// isn't clean (stray low-alpha halos outside the intended silhouette bleed into a full
        /// gradient fill there, since alpha-based fill only cares whether alpha is nonzero).
        /// Optional rotationDegrees (positive = counter-clockwise) is applied after the fill,
        /// same as GenerateSpriteWithGradient's rotation step — e.g. coca reuses the weed vial
        /// icon at 45 degrees to read as a distinct item at a glance.
        /// </summary>
        public static Sprite GenerateIconWithKeyColorFill(Sprite baseIcon, Color topColor, Color bottomColor, Color keyColor, float tolerance = 0.25f, float rotationDegrees = 0f)
        {
            if (baseIcon == null || baseIcon.texture == null)
            {
                Utility.Error("[GradientIconFill] Base icon sprite is missing.");
                return null;
            }

            Texture2D spriteTexture = baseIcon.texture;

            Rect spriteRect = baseIcon.rect;
            spriteRect.x /= spriteTexture.width;
            spriteRect.y /= spriteTexture.height;
            spriteRect.width /= spriteTexture.width;
            spriteRect.height /= spriteTexture.height;

            int width = Mathf.FloorToInt(spriteRect.width * spriteTexture.width);
            int height = Mathf.FloorToInt(spriteRect.height * spriteTexture.height);

            // ── TEMPORARY PROFILING ────────────────────────────────────────────
            // Profiling put ~295ms in a single icon generation. Split the stages to
            // find whether the cost is the CPU pixel loop (a shader would fix it) or
            // the GetPixels/Apply interop+GPU transfers (a shader would NOT).
            // Remove this block once the hot stage is identified.
            var swGet = System.Diagnostics.Stopwatch.StartNew();
            Color[] spritePixels = spriteTexture.GetPixels(
                Mathf.FloorToInt(spriteRect.x * spriteTexture.width),
                Mathf.FloorToInt(spriteRect.y * spriteTexture.height),
                width,
                height
            );
            swGet.Stop();

            var swFill = System.Diagnostics.Stopwatch.StartNew();
            FillKeyColorGaps(spritePixels, width, height, topColor, bottomColor, keyColor, tolerance);
            swFill.Stop();

            var swWrite = System.Diagnostics.Stopwatch.StartNew();
            Texture2D copiedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            copiedTexture.SetPixels(spritePixels);
            copiedTexture.Apply();
            swWrite.Stop();
            copiedTexture.name = baseIcon.name + "_KeyColorFill";

            Utility.Log($"[PROFILE icon '{baseIcon.name}' {width}x{height}] " +
                        $"GetPixels={swGet.ElapsedMilliseconds}ms | " +
                        $"FillKeyColorGaps={swFill.ElapsedMilliseconds}ms | " +
                        $"newTexture+SetPixels+Apply={swWrite.ElapsedMilliseconds}ms | " +
                        $"rotation={(Mathf.Approximately(rotationDegrees, 0f) ? "none" : rotationDegrees + "deg")}");

            if (!Mathf.Approximately(rotationDegrees, 0f))
            {
                Texture2D unrotated = copiedTexture;
                copiedTexture = RotateTexture(unrotated, rotationDegrees);
                width = copiedTexture.width;
                height = copiedTexture.height;
                // The pre-rotation copy is garbage now, and Unity textures aren't
                // reclaimed by the GC on their own.
                UnityEngine.Object.Destroy(unrotated);
            }

            return Sprite.Create(copiedTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Replaces pixels matching keyColor with a top-to-bottom gradient. Matching is a soft
        /// RGB-distance threshold (not exact-equals) so the placeholder-to-outline edge blends
        /// instead of aliasing; alpha is ignored for the match itself since the placeholder is
        /// always painted fully opaque.
        /// </summary>
        /// <summary>
        /// HOT PATH — runs once per pixel of a full icon (1,048,576 at 1024x1024).
        ///
        /// Every UnityEngine helper called here crosses the IL2CPP interop boundary. The
        /// original version used Color.Lerp, Mathf.Clamp01, Mathf.Sqrt (via
        /// ColorDistanceRgb) and 4x Mathf.Lerp — about 6 interop transitions per pixel, or
        /// ~6 million for one icon. Profiling measured 251ms in this method alone.
        ///
        /// Everything below is therefore plain C# arithmetic. Do NOT reintroduce
        /// Mathf/Color helper calls inside these loops.
        ///
        /// The squared-distance early-out matters as much as the inlining: most pixels are
        /// nowhere near the key colour, so they bail before the sqrt ever runs.
        /// </summary>
        private static void FillKeyColorGaps(Color[] pixels, int width, int height, Color top, Color bottom, Color keyColor, float tolerance)
        {
            if (pixels == null || width <= 0 || height <= 0 || tolerance <= 0f) return;

            float keyR = keyColor.r, keyG = keyColor.g, keyB = keyColor.b;
            float topR = top.r,    topG = top.g,    topB = top.b,    topA = top.a;
            float botR = bottom.r, botG = bottom.g, botB = bottom.b, botA = bottom.a;

            float tolSq = tolerance * tolerance;
            float invTolerance = 1f / tolerance;
            int denom = height - 1 <= 0 ? 1 : height - 1;

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / denom;
                float gradR = botR + (topR - botR) * t;
                float gradG = botG + (topG - botG) * t;
                float gradB = botB + (topB - botB) * t;
                float gradA = botA + (topA - botA) * t;

                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = row + x;
                    Color dst = pixels[idx];

                    float dr = dst.r - keyR;
                    float dg = dst.g - keyG;
                    float db = dst.b - keyB;
                    float distSq = dr * dr + dg * dg + db * db;

                    // Outside the tolerance radius the weight would clamp to <= 0.
                    if (distSq >= tolSq) continue;

                    float weight = 1f - (float)Math.Sqrt(distSq) * invTolerance;
                    if (weight <= 0f) continue;

                    // Mutate the local struct and write it back, rather than calling the
                    // Color constructor, to keep the whole body interop-free.
                    dst.r += (gradR - dst.r) * weight;
                    dst.g += (gradG - dst.g) * weight;
                    dst.b += (gradB - dst.b) * weight;
                    dst.a += (gradA - dst.a) * weight;
                    pixels[idx] = dst;
                }
            }
        }

        /// <summary>
        /// Euclidean RGB distance — 0 for identical colors, up to ~1.73 for opposite corners
        /// of the RGB cube (e.g. black vs. white). Used both for key-color pixel matching and,
        /// more generally, as a "how different do these two colors look" metric.
        /// </summary>
        public static float ColorDistanceRgb(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        /// <summary>
        /// If top/bottom are too close to produce a visible gradient (e.g. cocaine/meth mixes
        /// that land on nearly the same off-white), spreads them apart in HSV space instead of
        /// picking a 3rd stored color (unlike shrooms, coca/meth only have two colors to work
        /// with). Lightens the lighter one toward white and darkens the darker one toward black,
        /// each by half the remaining headroom, so separation happens regardless of how close to
        /// the extremes the original values already are. Also nudges saturation up slightly,
        /// since near-identical colors are usually low-saturation, where hue barely reads at all.
        /// Each color keeps its own hue — this reshades, it doesn't recolor.
        /// </summary>
        public static (Color top, Color bottom) BoostContrastIfSimilar(Color top, Color bottom, float similarityThreshold = 0.15f)
        {
            if (ColorDistanceRgb(top, bottom) >= similarityThreshold)
                return (top, bottom);

            Color.RGBToHSV(top, out float topH, out float topS, out float topV);
            Color.RGBToHSV(bottom, out float botH, out float botS, out float botV);

            topS = Mathf.Clamp01(topS + 0.2f);
            botS = Mathf.Clamp01(botS + 0.2f);

            float boostedTopV = Mathf.Lerp(topV, 1f, 0.5f);
            float boostedBotV = Mathf.Lerp(botV, 0f, 0.5f);

            Color boostedTop = Color.HSVToRGB(topH, topS, boostedTopV);
            boostedTop.a = top.a;
            Color boostedBottom = Color.HSVToRGB(botH, botS, boostedBotV);
            boostedBottom.a = bottom.a;

            return (boostedTop, boostedBottom);
        }

        /// <summary>
        /// Rotates a texture about its center by inverse-mapping each destination pixel back
        /// into source space and sampling bilinearly. The canvas grows to fit the rotated
        /// corners (a 45 degree turn needs ~1.41x per side), staying centered so the sprite's
        /// center pivot still lines up. Pixels that map outside the source become transparent.
        /// </summary>
        private static Texture2D RotateTexture(Texture2D source, float degrees)
        {
            int srcWidth = source.width;
            int srcHeight = source.height;

            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            int dstWidth = Mathf.CeilToInt(Mathf.Abs(srcWidth * cos) + Mathf.Abs(srcHeight * sin));
            int dstHeight = Mathf.CeilToInt(Mathf.Abs(srcWidth * sin) + Mathf.Abs(srcHeight * cos));

            Color[] srcPixels = source.GetPixels();
            Color[] dstPixels = new Color[dstWidth * dstHeight];

            float srcCenterX = srcWidth * 0.5f;
            float srcCenterY = srcHeight * 0.5f;
            float dstCenterX = dstWidth * 0.5f;
            float dstCenterY = dstHeight * 0.5f;

            for (int y = 0; y < dstHeight; y++)
            {
                for (int x = 0; x < dstWidth; x++)
                {
                    float dx = x + 0.5f - dstCenterX;
                    float dy = y + 0.5f - dstCenterY;

                    float sx = dx * cos + dy * sin + srcCenterX;
                    float sy = -dx * sin + dy * cos + srcCenterY;

                    dstPixels[y * dstWidth + x] = SampleBilinear(srcPixels, srcWidth, srcHeight, sx, sy);
                }
            }

            Texture2D rotated = new Texture2D(dstWidth, dstHeight, TextureFormat.RGBA32, false);
            rotated.SetPixels(dstPixels);
            rotated.Apply();
            rotated.name = "Rotated";
            return rotated;
        }

        private static Color SampleBilinear(Color[] pixels, int width, int height, float x, float y)
        {
            // Shift from pixel-center coords to texel-index coords.
            float fx = x - 0.5f;
            float fy = y - 0.5f;

            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;

            Color c00 = GetPixelOrClear(pixels, width, height, x0, y0);
            Color c10 = GetPixelOrClear(pixels, width, height, x0 + 1, y0);
            Color c01 = GetPixelOrClear(pixels, width, height, x0, y0 + 1);
            Color c11 = GetPixelOrClear(pixels, width, height, x0 + 1, y0 + 1);

            float a = Mathf.Lerp(Mathf.Lerp(c00.a, c10.a, tx), Mathf.Lerp(c01.a, c11.a, tx), ty);
            if (a <= 0f) return Color.clear;

            // Interpolate premultiplied, then undo it — otherwise fully transparent texels
            // drag their (usually black) RGB into the vial's edge pixels as a dark halo.
            float r = Mathf.Lerp(Mathf.Lerp(c00.r * c00.a, c10.r * c10.a, tx), Mathf.Lerp(c01.r * c01.a, c11.r * c11.a, tx), ty);
            float g = Mathf.Lerp(Mathf.Lerp(c00.g * c00.a, c10.g * c10.a, tx), Mathf.Lerp(c01.g * c01.a, c11.g * c11.a, tx), ty);
            float b = Mathf.Lerp(Mathf.Lerp(c00.b * c00.a, c10.b * c10.a, tx), Mathf.Lerp(c01.b * c01.a, c11.b * c11.a, tx), ty);

            return new Color(r / a, g / a, b / a, a);
        }

        private static Color GetPixelOrClear(Color[] pixels, int width, int height, int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return Color.clear;
            return pixels[y * width + x];
        }

        private static void ApplyVerticalGradientInRect(
            Color[] pixels, int width, int height,
            Rect rect01, Color top, Color bottom, float opacity,
            BlendMode mode)
        {
            // Convert normalized rect to pixel-space
            int rx = Mathf.RoundToInt(rect01.x * width);
            int ry = Mathf.RoundToInt(rect01.y * height);
            int rw = Mathf.RoundToInt(rect01.width * width);
            int rh = Mathf.RoundToInt(rect01.height * height);

            // Clamp to bounds
            rx = Mathf.Clamp(rx, 0, width);
            ry = Mathf.Clamp(ry, 0, height);
            rw = Mathf.Clamp(rw, 0, width - rx);
            rh = Mathf.Clamp(rh, 0, height - ry);
            if (rw <= 0 || rh <= 0) return;

            // For each y in rect, compute t = (y - ry) / rh; top at rect top
            // NOTE: texture origin is bottom-left; "top" should be at higher y.
            for (int y = 0; y < rh; y++)
            {
                float t = (float)y / (float)(rh - 1 <= 0 ? 1 : (rh - 1));
                // t=0 at bottom of rect → bottom color; we want top color at rect top, so:
                Color grad = Color.Lerp(bottom, top, t);
                grad.a *= opacity;

                int py = ry + y;
                int row = py * width;

                for (int x = 0; x < rw; x++)
                {
                    int px = rx + x;
                    int idx = row + px;

                    var dst = pixels[idx];
                    pixels[idx] = Blend(grad, dst, mode);
                }
            }
        }

        private static Color Blend(Color dst, Color src, BlendMode mode)
        {
            switch (mode)
            {
                case BlendMode.Multiply:
                    return new Color(
                            dst.r * Mathf.Lerp(1f, src.r, src.a),
                            dst.g * Mathf.Lerp(1f, src.g, src.a),
                            dst.b * Mathf.Lerp(1f, src.b, src.a),
                            Mathf.Max(dst.a, src.a)
                    );

                case BlendMode.Add:
                    return new Color(
                            Mathf.Clamp01(dst.r + src.r * src.a),
                            Mathf.Clamp01(dst.g + src.g * src.a),
                            Mathf.Clamp01(dst.b + src.b * src.a),
                            Mathf.Max(dst.a, src.a)
                    );

                case BlendMode.Screen:
                    // screen = 1 - (1 - A) * (1 - B)
                    float r = 1f - (1f - dst.r) * (1f - src.r * src.a);
                    float g = 1f - (1f - dst.g) * (1f - src.g * src.a);
                    float b = 1f - (1f - dst.b) * (1f - src.b * src.a);
                    return new Color(r, g, b, Mathf.Max(dst.a, src.a));

                case BlendMode.Lerp:
                default:
                    // “Normal” over: lerp RGB by src alpha, preserve max alpha
                    return new Color(
                            Mathf.Lerp(dst.r, src.r, src.a),
                            Mathf.Lerp(dst.g, src.g, src.a),
                            Mathf.Lerp(dst.b, src.b, src.a),
                            Mathf.Max(dst.a, src.a)
                    );
            }
        }
    }
}
