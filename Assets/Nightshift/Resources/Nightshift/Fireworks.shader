Shader "Nightshift/Fireworks"
{
    Properties { _Billboard ("Billboard", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Billboard;

            struct Vertex
            {
                float4 position : POSITION;
                float2 corner : TEXCOORD0;
                float2 spark : TEXCOORD1;
                float4 color : COLOR;
            };

            struct Varying
            {
                float4 position : SV_POSITION;
                float2 corner : TEXCOORD0;
                half3 color : COLOR;
            };

            Varying vert(Vertex v)
            {
                Varying o;
                float3 view = UnityObjectToViewPos(v.position);
                view.xy += (v.corner - .5) * (v.spark.x * _Billboard);
                o.position = mul(UNITY_MATRIX_P, float4(view, 1));
                o.corner = v.corner;
                o.color = v.color.rgb * v.spark.y;
                return o;
            }

            half4 frag(Varying i) : SV_Target
            {
                float radius = saturate(1 - length(i.corner * 2 - 1));
                float glow = lerp(1, radius * radius, _Billboard);
                return half4(i.color * glow, 0);
            }
            ENDCG
        }
    }
}
