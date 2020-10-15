Shader "Unlit/PortalViewport"
{
    Properties
    {
        [HideInInspector] _CullMode ("__cullMode", Float) = 0 // Off
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Offset -0.1, 0
            Cull [_CullMode]

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "PortalCommon.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : COLOR
            {
                ClipPlane(i.worldPos);
                return fixed4(0, 0, 0, 1);
            }
            ENDCG
        }

        Pass
        {
            ZWrite On
            ZTest Always
            Cull [_CullMode]
            
            Stencil
            {
                Ref 1
                Comp Equal
                Pass Keep
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float frag (v2f i) : DEPTH
            {
                return 0.0f;
            }
            ENDCG
        }
    }
}
