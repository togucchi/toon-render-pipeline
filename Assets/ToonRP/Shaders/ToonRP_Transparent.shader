Shader "ToonRP/UnlitTransparent"
{
    Properties
    {
        _Color("Color", COLOR) = (1, 1, 1, 1)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "Transparent"
            Tags {"LightMode" = "ToonForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ ENABLE_DIRECTIONAL_LIGHT   

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

            float4 _Color;

            sampler2D _MainTex;
            float4 _MainTex_ST;

#if ENABLE_DIRECTIONAL_LIGHT
            float4 _LightColor;
            float4 _LightVector;
#endif

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                col *= _Color;

#if ENABLE_DIRECTIONAL_LIGHT
                col *= _LightColor;
#endif
                return col;
            }
            ENDHLSL
        }
    }
}
