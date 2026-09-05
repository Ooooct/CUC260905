using System;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 用户节点配色工具（纯静态、无状态）。
    /// 四套底色（#1B9FD6 / #16BB79 / #EF2847 / #FFCC00）构成候选池；
    /// 节点生成时随机取其一作底色，边缘轮廓在 HSL 空间保持色相不变、
    /// 将明度 L 降低固定量（默认 0.15）得到，从而呈现"同色系固定加深"的轮廓。
    /// </summary>
    public static class UserNodePalette
    {
        /// <summary>四套底色候选池（用户节点生成时随机取其一）。</summary>
        public static readonly Color[] BaseColors =
        {
            new Color(0.106f, 0.624f, 0.839f, 1f), // #1B9FD6
            new Color(0.086f, 0.733f, 0.475f, 1f), // #16BB79
            new Color(0.937f, 0.157f, 0.278f, 1f), // #EF2847
            new Color(1f, 0.8f, 0f, 1f)            // #FFCC00
        };

        /// <summary>默认边缘轮廓 HSL 明度固定加深量。</summary>
        public const float DefaultOutlineLightnessStep = 0.15f;

        /// <summary>从候选池均匀随机取一套底色。</summary>
        public static Color RandomBaseColor(System.Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            return BaseColors[random.Next(BaseColors.Length)];
        }

        /// <summary>
        /// 在 HSL 空间将明度 L 降低固定量，得到同色系更深的边缘轮廓色。
        /// 色相 H 与饱和度 S 保持不变；明度下限为 0，透明度沿用底色。
        /// </summary>
        public static Color DarkenOutline(Color baseColor, float lightnessStep)
        {
            RgbToHsl(baseColor, out float hue, out float saturation, out float lightness);
            float darkened = Mathf.Max(0f, lightness - Mathf.Max(0f, lightnessStep));
            Color outline = HslToRgb(hue, saturation, darkened);
            outline.a = baseColor.a;
            return outline;
        }

        /// <summary>RGB → HSL（h 单位为度，取值 [0, 360)）。</summary>
        private static void RgbToHsl(Color c, out float h, out float s, out float l)
        {
            float r = c.r;
            float g = c.g;
            float b = c.b;

            float max = Mathf.Max(r, Mathf.Max(g, b));
            float min = Mathf.Min(r, Mathf.Min(g, b));
            l = (max + min) * 0.5f;

            if (Mathf.Approximately(max, min))
            {
                h = 0f;
                s = 0f;
                return;
            }

            float delta = max - min;
            s = l > 0.5f ? delta / (2f - max - min) : delta / (max + min);

            if (Mathf.Approximately(max, r))
            {
                h = (g - b) / delta + (g < b ? 6f : 0f);
            }
            else if (Mathf.Approximately(max, g))
            {
                h = (b - r) / delta + 2f;
            }
            else
            {
                h = (r - g) / delta + 4f;
            }

            h *= 60f;
        }

        /// <summary>HSL → RGB（h 单位为度）。</summary>
        private static Color HslToRgb(float h, float s, float l)
        {
            float chroma = (1f - Mathf.Abs(2f * l - 1f)) * s;
            float hp = h / 60f;
            float x = chroma * (1f - Mathf.Abs(hp % 2f - 1f));
            float m = l - chroma * 0.5f;

            float r;
            float g;
            float b;
            if (hp < 1f)
            {
                r = chroma;
                g = x;
                b = 0f;
            }
            else if (hp < 2f)
            {
                r = x;
                g = chroma;
                b = 0f;
            }
            else if (hp < 3f)
            {
                r = 0f;
                g = chroma;
                b = x;
            }
            else if (hp < 4f)
            {
                r = 0f;
                g = x;
                b = chroma;
            }
            else if (hp < 5f)
            {
                r = x;
                g = 0f;
                b = chroma;
            }
            else
            {
                r = chroma;
                g = 0f;
                b = x;
            }

            return new Color(r + m, g + m, b + m, 1f);
        }
    }
}
