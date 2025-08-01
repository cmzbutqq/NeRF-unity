Shader "Hidden/DepthVisualization"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
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
            sampler2D _CameraDepthTexture;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 读取深度值
                float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                // 转换为线性深度
                depth = Linear01Depth(depth);
                // 增强对比度
                depth = pow(depth, 0.5);
                // 反转深度（近处亮，远处暗）
                depth = 1.0 - depth;

                return fixed4(depth, depth, depth, 1.0);
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
