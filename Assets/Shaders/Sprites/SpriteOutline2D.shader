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
        _OuterOutlineWidth ("Outer Outline Width (Pixels)", Range(0, 64)) = 1
        [HideInInspector] _OuterMeshCenter ("Outer Mesh Center", Vector) = (0, 0, 0, 0)
        [HideInInspector] _OuterMeshScale ("Outer Mesh Scale", Vector) = (1, 1, 0, 0)
        _InnerOutlineColor ("Inner Outline Color", Color) = (1, 1, 1, 0)
        _InnerOutlineWidth ("Inner Outline Width (Pixels)", Range(0, 64)) = 0
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
            Name "OuterOutline"

            CGPROGRAM
            #pragma vertex vertOuterOutline
            #pragma fragment fragOuterOutline
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnitySprites.cginc"

            fixed4 _OuterOutlineColor;
            float _OuterOutlineWidth;
            float4 _OuterMeshCenter;
            float4 _OuterMeshScale;

            v2f vertOuterOutline(appdata_t input)
            {
                input.vertex.xy = _OuterMeshCenter.xy
                    + (input.vertex.xy - _OuterMeshCenter.xy) * _OuterMeshScale.xy;
                return SpriteVert(input);
            }

            fixed4 fragOuterOutline(v2f input) : SV_Target
            {
                if (_OuterOutlineWidth <= 0.0)
                {
                    discard;
                }

                fixed sourceAlpha = SampleSpriteTexture(input.texcoord).a * input.color.a;
                fixed4 outlineColor = _OuterOutlineColor;
                outlineColor.a *= sourceAlpha;
                outlineColor.rgb *= outlineColor.a;
                return outlineColor;
            }
            ENDCG
        }

        Pass
        {
            Name "SpriteAndInnerOutline"

            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnitySprites.cginc"

            fixed4 _InnerOutlineColor;
            float _InnerOutlineWidth;
            float4 _MainTex_TexelSize;

            float SampleAlpha(v2f input, float2 offset)
            {
                return SampleSpriteTexture(input.texcoord + offset * _MainTex_TexelSize.xy).a * input.color.a;
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
                float innerMask = 0.0;

                if (_InnerOutlineWidth > 0.0)
                {
                    innerMask = spriteAlpha * (1.0 - MinNeighbourAlpha(input, _InnerOutlineWidth));
                }

                fixed4 result = Premultiply(spriteColor);
                fixed4 innerColor = _InnerOutlineColor;
                innerColor.a *= input.color.a;
                innerColor = Premultiply(innerColor);

                result = BlendPremultiplied(result, innerColor, innerMask);
                return result;
            }
            ENDCG
        }
    }
}
