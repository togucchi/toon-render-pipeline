Shader "ToonRP/Specular"
{
    Properties
    {
        _Color("Color", COLOR) = (1, 1, 1, 1)
        _Specular("Specular", Range(0, 1)) = 1
        _MainTex("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "Unlit"
            Tags {"LightMode" = "ToonForward"}

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
                float4 pos : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            float4 _Color;
            float _Specular;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _ReflectionTex;
            float4 _ReflectionTex_ST;

#if ENABLE_DIRECTIONAL_LIGHT
            float4 _LightColor;
            float4 _LightVector;
#endif

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.pos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                col *= _Color;

                fixed4 reflection = tex2Dproj(_ReflectionTex, i.pos);
                col = lerp(col, reflection, _Specular);

#if ENABLE_DIRECTIONAL_LIGHT
                col *= _LightColor;
#endif
                return col;
            }
            ENDHLSL
        }
    }
}
