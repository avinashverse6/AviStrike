using UnityEngine;

namespace CageDuel.UI
{
    /// <summary>
    /// Generates crisp anti-aliased circle and ring sprites in memory at runtime
    /// so mobile touch controls need zero binary image assets in the repository.
    /// </summary>
    public static class ProceduralUISprites
    {
        private static Sprite _filledCircle;
        private static Sprite _ring;

        public static Sprite GetFilledCircleSprite()
        {
            if (_filledCircle == null)
            {
                _filledCircle = CreateRadialSprite("ProceduralFilledCircle", 64, 0f, 0.96f);
            }
            return _filledCircle;
        }

        public static Sprite GetRingSprite()
        {
            if (_ring == null)
            {
                _ring = CreateRadialSprite("ProceduralRing", 96, 0.76f, 0.96f, 0.22f);
            }
            return _ring;
        }

        private static Sprite CreateRadialSprite(
            string spriteName,
            int size,
            float innerRadiusNorm,
            float outerRadiusNorm,
            float interiorFillAlpha = 1f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = spriteName + "_Tex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            float half = (size - 1) * 0.5f;
            float feather = 2.0f / size;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float outerAlpha = 1f - Mathf.SmoothStep(outerRadiusNorm - feather, outerRadiusNorm + feather, dist);
                    float alpha;

                    if (innerRadiusNorm <= 0.001f)
                    {
                        alpha = outerAlpha;
                    }
                    else
                    {
                        float ringBand = Mathf.SmoothStep(innerRadiusNorm - feather, innerRadiusNorm + feather, dist);
                        float blendedInterior = Mathf.Lerp(interiorFillAlpha, 1f, ringBand);
                        alpha = outerAlpha * blendedInterior;
                    }

                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
