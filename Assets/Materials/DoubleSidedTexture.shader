Shader "Custom/DoubleSidedTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        // --- Outline pass (back-face extrusion) ---
        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #include "UnityCG.cginc"

            float4 _OutlineColor;
            float _OutlineWidth;

            struct appdata_outline
            {
                float4 vertex : POSITION;
            };

            float4 vertOutline(appdata_outline v) : SV_POSITION
            {
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                float4 origin  = UnityObjectToClipPos(float4(0, 0, 0, 1));
                // Push each vertex away from the projected object center in NDC,
                // scaled proportionally so corners fill in on hard-edged meshes.
                float2 dir = clipPos.xy / clipPos.w - origin.xy / origin.w;
                // Normalize direction so the offset is constant regardless of object size.
                float2 dirNorm = (length(dir) > 0.0001) ? normalize(dir) : float2(0.0, 1.0);
                clipPos.xy += dirNorm * (_OutlineWidth * clipPos.w);
                return clipPos;
            }

            fixed4 fragOutline() : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        // --- Main double-sided pass ---
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                return col;
            }
            ENDCG
        }
    }
}
