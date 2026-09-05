using CUC260905.Network;
using NUnit.Framework;
using UnityEngine;

namespace CUC260905.Tests
{
    /// <summary>
    /// UserNodePalette：锁定用户节点四套底色与 HSL 加深轮廓的行为契约。
    /// 覆盖：候选池内容、随机取色来源、固定量加深（明度严格下降且差值精确）、
    /// 色相保持、明度下限钳制、透明度沿用底色。
    /// </summary>
    public sealed class UserNodePaletteTests
    {
        [Test]
        public void BaseColors_ContainsExactlyTheFourCanonicalColors()
        {
            Assert.That(UserNodePalette.BaseColors.Length, Is.EqualTo(4));
        }

        [Test]
        public void RandomBaseColor_AlwaysReturnsOneOfTheFourBaseColors()
        {
            System.Random random = new System.Random(12345);
            for (int i = 0; i < 256; i++)
            {
                Color color = UserNodePalette.RandomBaseColor(random);
                Assert.That(ContainsColor(UserNodePalette.BaseColors, color), Is.True,
                    "随机取色必须来自四套底色候选池。");
            }
        }

        [Test]
        public void RandomBaseColor_WithFixedSeed_CoversMultipleDistinctColors()
        {
            // 固定种子下大量抽取应覆盖多种底色，证明是随机选取而非固定色。
            System.Random random = new System.Random(987);
            int distinct = 0;
            foreach (Color candidate in UserNodePalette.BaseColors)
            {
                for (int i = 0; i < 64; i++)
                {
                    if (NearlyEqual(UserNodePalette.RandomBaseColor(random), candidate))
                    {
                        distinct++;
                        break;
                    }
                }
            }

            Assert.That(distinct, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void DarkenOutline_ReducesLightnessByTheFixedStep()
        {
            const float Step = 0.15f;
            foreach (Color baseColor in UserNodePalette.BaseColors)
            {
                Color outline = UserNodePalette.DarkenOutline(baseColor, Step);

                float baseLightness = GetLightness(baseColor);
                float outlineLightness = GetLightness(outline);

                Assert.That(outlineLightness, Is.LessThan(baseLightness),
                    $"{baseColor} 的轮廓明度必须低于底色。");
                Assert.That(outlineLightness, Is.EqualTo(baseLightness - Step).Within(1e-3f),
                    $"{baseColor} 的轮廓明度应精确降低固定量 {Step}。");
            }
        }

        [Test]
        public void DarkenOutline_PreservesHue()
        {
            foreach (Color baseColor in UserNodePalette.BaseColors)
            {
                Color outline = UserNodePalette.DarkenOutline(baseColor, 0.15f);

                float baseHue = GetHue(baseColor);
                float outlineHue = GetHue(outline);

                Assert.That(Mathf.Abs(baseHue - outlineHue), Is.LessThan(1e-3f),
                    $"{baseColor} 的轮廓必须保持同一色相。");
            }
        }

        [Test]
        public void DarkenOutline_ClampsLightnessAtZero_WhenStepExceedsLightness()
        {
            Color outline = UserNodePalette.DarkenOutline(new Color(0f, 0f, 0f, 1f), 0.5f);
            Assert.That(GetLightness(outline), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void DarkenOutline_KeepsAlphaOfTheBaseColor()
        {
            Color baseColor = new Color(0.106f, 0.624f, 0.839f, 0.8f);
            Color outline = UserNodePalette.DarkenOutline(baseColor, 0.15f);
            Assert.That(outline.a, Is.EqualTo(baseColor.a).Within(1e-3f));
        }

        private static bool ContainsColor(Color[] colors, Color target)
        {
            foreach (Color color in colors)
            {
                if (NearlyEqual(color, target))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NearlyEqual(Color first, Color second)
        {
            const float Epsilon = 1e-3f;
            return Mathf.Abs(first.r - second.r) < Epsilon
                && Mathf.Abs(first.g - second.g) < Epsilon
                && Mathf.Abs(first.b - second.b) < Epsilon;
        }

        private static float GetLightness(Color color)
        {
            float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            float min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            return (max + min) * 0.5f;
        }

        private static float GetHue(Color color)
        {
            float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            float min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            float delta = max - min;
            if (Mathf.Approximately(delta, 0f))
            {
                return 0f;
            }

            float hue;
            if (Mathf.Approximately(max, color.r))
            {
                hue = (color.g - color.b) / delta + (color.g < color.b ? 6f : 0f);
            }
            else if (Mathf.Approximately(max, color.g))
            {
                hue = (color.b - color.r) / delta + 2f;
            }
            else
            {
                hue = (color.r - color.g) / delta + 4f;
            }

            return hue * 60f;
        }
    }
}
