Shader "Hidden/ToonRP/HexBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass // 0
        {
            HLSLPROGRAM
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
            float4 _MainTex_TexelSize;

            float _BlurPower;

            v2f vert(appdata v)
            {
	            v2f o;
	            o.vertex = UnityObjectToClipPos(v.vertex);
	            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
	            return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                const float2 blurDir = float2(0.0, 1.0);
                const float2 blurVec = normalize(blurDir);
                const int SAMPLE_NUM = 16;

                float2 uv = i.uv;

                float2 p0 = uv - 0.5 * _BlurPower * blurVec;
                float2 p1 = uv + 0.5 * _BlurPower * blurVec;
                float2 stepVec = (p1 - p0) / float(SAMPLE_NUM);

                float2 p = p0;

                float3 sumCol = 0;

                for(int i = 0; i < SAMPLE_NUM; ++i)
                {
                    float4 tex = tex2D(_MainTex, p);
                	float3 sample = (tex - 0.1) / 0.9;
                    sumCol += tex.rgb;

                    p += stepVec * 2;
                }

                sumCol /= float(SAMPLE_NUM);
                sumCol = max(sumCol, 0.0);

	            return fixed4(sumCol, 1.0);
            }
            ENDHLSL
        }

        Pass // 1
        {
            HLSLPROGRAM
            
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
            float4 _MainTex_TexelSize;

            float _BlurPower;

            v2f vert(appdata v)
            {
	            v2f o;
	            o.vertex = UnityObjectToClipPos(v.vertex);
	            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
	            return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
            	const int SAMPLE_NUM = 16;

            	float2 uv = i.uv;

            	float3 sumCol0 = 0;
				{
					const float2 blurDir = float2(1.0, 1);
					const float2 blurVec = normalize(blurDir);

					float2 p0 = uv - 0.5 * _BlurPower * blurVec;
					float2 p1 = uv + 0.5 * _BlurPower * blurVec;

					float2 stepVec = (p1 - p0) / float(SAMPLE_NUM);

					float2 p = p0;
            		for(int i = 0; i < SAMPLE_NUM; i++)
            		{
            			float4 tex = tex2D(_MainTex, p);
            			sumCol0 += tex.rgb;
            			p += stepVec;
            		}
            		sumCol0 /= float(SAMPLE_NUM);
				}

            	float3 sumCol1 = 0;
            	{
            		const float2 blurDir = float2(-1.0, 1);
            		const float2 blurVec = normalize(blurDir);

            		float2 p0 = uv - 0.5 * _BlurPower * blurVec;
					float2 p1 = uv + 0.5 * _BlurPower * blurVec;

					float2 stepVec = (p1 - p0) / float(SAMPLE_NUM);

					float2 p = p0;

            		for(int i = 0; i < SAMPLE_NUM; i++)
            		{
            			float4 tex = tex2D(_MainTex, p);
            			sumCol1 += tex.rgb;
            			p += stepVec;
            		}
            		sumCol1 /= float(SAMPLE_NUM);
            	}

            	float3 sumCol = min(sumCol0, sumCol1);
            	// return tex2D(_MainTex, uv);
            	return fixed4(sumCol, 1.0);
            }

            ENDHLSL
        }
        
    }
}
