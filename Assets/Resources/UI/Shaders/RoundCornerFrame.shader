﻿Shader "CornShader/UI/RoundCornerFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BorderColor ("Border Color", Color) = (1, 1, 1, 1)
        _FillColor ("Fill Color", Color) = (0.2, 0.5, 1, 1)
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 size : TEXCOORD1;
                float4 cornerRadii : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 size : TEXCOORD1;
                float4 cornerRadii : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv * 2.0 - 1.0;
                output.color = input.color;
                output.size = input.size;
                output.cornerRadii = input.cornerRadii;
                
                return output;
            }

            CBUFFER_START(UnityPerMaterial)
            
            half4 _BorderColor;
            half4 _FillColor;
            
            CBUFFER_END

            // ref: iquilezles.org/articles/distfunctions2d
            // b.x = width
            // b.y = height
            // r.x = roundness top-right  
            // r.y = roundness bottom-right
            // r.z = roundness top-left
            // r.w = roundness bottom-left
            float sdRoundBox(float2 p, float2 b, float4 r)
            {
                r.xy = p.x > 0.0 ? r.xy : r.zw;
                r.x = p.y > 0.0 ? r.x : r.y;
                float2 q = abs(p) - b + r.x;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 size = input.size.xy;
                float borderThickness = input.size.z;
                float4 cornerRadii = input.cornerRadii;
                
                float borderDist = sdRoundBox(input.uv * size, size, cornerRadii);
                float borderMask = borderDist + borderThickness * 2.0;

                // Draw border
                half4 color = -borderThickness < borderDist && borderDist < 0 ? _BorderColor : _BorderColor * 0;

                // Draw value
                float2 fillSize = float2(size.x, size.y);
                float fillDist = sdRoundBox(input.uv * size, fillSize, cornerRadii);

                color = borderMask < 0 && fillDist < 0 ? _FillColor : color;
                color *= input.color;

                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Unlit/Transparent"
}