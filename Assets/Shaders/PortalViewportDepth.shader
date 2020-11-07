Shader "Unlit/PortalViewportDepth"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+101"
        }

        Pass
        {
            Name "ForwardPortalViewportDepth"
            Tags{"LightMode" = "UniversalForward"}

            ZWrite On
            ZTest Always
            Cull [_PortalCullMode]
            
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
