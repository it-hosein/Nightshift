Shader "Nightshift/Ghost Car"
{
    Properties
    {
        _Color ("Cyan", Color) = (0.28, 0.81, 1, 0.38)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float3 view : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 world : TEXCOORD2;
            };

            Varyings vert(appdata_base v)
            {
                Varyings o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = _WorldSpaceCameraPos - o.world;
                return o;
            }

            fixed4 frag(Varyings i) : SV_Target
            {
                float rim = pow(1 - saturate(dot(normalize(i.view), normalize(i.normal))), 2);
                float scan = 0.92 + 0.08 * sin(i.world.y * 48 - _Time.y * 7);
                return fixed4(_Color.rgb * (0.75 + rim * 0.65), _Color.a * (0.6 + rim * 0.85) * scan);
            }
            ENDCG
        }
    }
}
