Shader "Nightshift/Exhaust Flame"
{
    Properties { _Clock ("Animation time", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Clock;
            struct Vertex { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct Varying { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };

            Varying vert(Vertex v)
            {
                Varying o;
                o.position = UnityObjectToClipPos(v.position);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varying i) : SV_Target
            {
                float t = i.uv.y;
                float taper = (.77 + .23 * smoothstep(0, .16, t)) * pow(max(0, 1-t), .56);
                float ripple = .09 * sin(t * 23 - _Clock * 35) + .04 * sin(t * 51 + _Clock * 63);
                float profile = abs(i.uv.x * 2 - 1 - ripple * t);
                float edge = 1 - smoothstep(.12, max(.15, taper), profile);
                float core = (1 - smoothstep(.01, max(.12, taper * .48), profile)) * (1 - smoothstep(.22, .69, t));
                half3 color = lerp(half3(.28, .54, .92), half3(1.65, .38, .047), smoothstep(.20, .68, t));
                color = lerp(color, half3(2.05, 2.02, 1.94), core);
                return half4(color, edge * (1 - smoothstep(.45, 1, t)) * smoothstep(0, .07, t));
            }
            ENDCG
        }
    }
}
