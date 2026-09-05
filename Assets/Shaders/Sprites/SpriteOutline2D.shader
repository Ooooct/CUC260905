Shader "CUC260905/Sprite Outline 2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip ("Flip", Vector) = (1, 1, 1, 1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        _OuterOutlineColor ("Outer Outline Color", Color) = (1, 0.82, 0.18, 1)
        _OuterOutlineWidth ("Outer Outline Width (Pixels)", Range(0, 16)) = 1
        _InnerOutlineColor ("Inner Outline Color", Color) = (1, 1, 1, 0)
        _InnerOutlineWidth ("Inner Outline Width (Pixels)", Range(0, 16)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnitySprites.cginc"

            fixed4 _OuterOutlineColor;
            float _OuterOutlineWidth;
            fixed4 _InnerOutlineColor;
            float _InnerOutlineWidth;
            float4 _MainTex_TexelSize;

            float SampleAlpha(v2f input, float2 offset)
            {
                return SampleSpriteTexture(input.texcoord + offset * _MainTex_TexelSize.xy).a * input.color.a;
            }

            float MaxNeighbourAlpha(v2f input, float width)
            {
                float2 diagonal = float2(width, width);
                float maxAlpha = 0.0;

                maxAlpha = max(maxAlpha, SampleAlpha(input, float2(width, 0.0)));
                maxAlpha = max(maxAlpha, SampleAlpha(input, float2(-width, 0.0)));
                maxAlpha = max(maxAlpha, SampleAlpha(input, float2(0.0, width)));
                maxAlpha = max(maxAlpha, SampleAlpha(input, float2(0.0, -width)));
                maxAlpha = max(maxAlpha, SampleAlpha(input, diagonal));
                maxAlpha = max(maxAlpha, SampleAlpha(input, float2(-diagonal.x, diagonal.y)));
                maxAlpha = max(maxAlpha, SampleAlpha(input, float2(diagonal.x, -diagonal.y)));
                maxAlpha = max(maxAlpha, SampleAlpha(input, -diagonal));
                return maxAlpha;
            }

            float MinNeighbourAlpha(v2f input, float width)
            {
                float2 diagonal = float2(width, width);
                float minAlpha = 1.0;

                minAlpha = min(minAlpha, SampleAlpha(input, float2(width, 0.0)));
                minAlpha = min(minAlpha, SampleAlpha(input, float2(-width, 0.0)));
                minAlpha = min(minAlpha, SampleAlpha(input, float2(0.0, width)));
                minAlpha = min(minAlpha, SampleAlpha(input, float2(0.0, -width)));
                minAlpha = min(minAlpha, SampleAlpha(input, diagonal));
                minAlpha = min(minAlpha, SampleAlpha(input, float2(-diagonal.x, diagonal.y)));
                minAlpha = min(minAlpha, SampleAlpha(input, float2(diagonal.x, -diagonal.y)));
                minAlpha = min(minAlpha, SampleAlpha(input, -diagonal));
                return minAlpha;
            }

            fixed4 Premultiply(fixed4 color)
            {
                color.rgb *= color.a;
                return color;
            }

            fixed4 BlendPremultiplied(fixed4 background, fixed4 foreground, float coverage)
            {
                fixed4 coveredForeground = foreground * saturate(coverage);
                return coveredForeground + background * (1.0 - coveredForeground.a);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 spriteColor = SampleSpriteTexture(input.texcoord) * input.color;
                float spriteAlpha = spriteColor.a;
                float outerMask = 0.0;
                float innerMask = 0.0;

                if (_OuterOutlineWidth > 0.0)
                {
                    outerMask = (1.0 - spriteAlpha) * MaxNeighbourAlpha(input, _OuterOutlineWidth);
                }

                if (_InnerOutlineWidth > 0.0)
                {
                    innerMask = spriteAlpha * (1.0 - MinNeighbourAlpha(input, _InnerOutlineWidth));
                }

                fixed4 result = Premultiply(spriteColor);
                fixed4 outerColor = _OuterOutlineColor;
                fixed4 innerColor = _InnerOutlineColor;
                outerColor.a *= input.color.a;
                innerColor.a *= input.color.a;
                outerColor = Premultiply(outerColor);
                innerColor = Premultiply(innerColor);

                result = BlendPremultiplied(result, outerColor, outerMask);
                result = BlendPremultiplied(result, innerColor, innerMask);
                return result;
            }
            ENDCG
        }
    }
}
