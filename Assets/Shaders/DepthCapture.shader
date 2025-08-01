Shader "Custom/DepthCapture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "DepthCapture"

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
                float depth : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // 计算线性深度
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                float4 viewPos = mul(UNITY_MATRIX_V, worldPos);
                o.depth = -viewPos.z;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 将深度值归一化到0-1范围
                float normalizedDepth = (i.depth - _ProjectionParams.y) / (_ProjectionParams.z - _ProjectionParams.y);
                normalizedDepth = saturate(normalizedDepth);

                // 增强对比度
                normalizedDepth = pow(normalizedDepth, 0.5);

                return fixed4(normalizedDepth, normalizedDepth, normalizedDepth, 1.0);
            }
            ENDCG
        }
    }

    // 备用方案 - 使用内置深度渲染
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f {
                float4 pos : SV_POSITION;
                float2 depth : TEXCOORD0;
            };

            v2f vert (appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.depth = o.pos.zw;
                return o;
            }

            half4 frag(v2f i) : SV_Target {
                float depth = i.depth.x / i.depth.y;
                return half4(depth, depth, depth, 1);
            }
            ENDCG
        }
    }
}
