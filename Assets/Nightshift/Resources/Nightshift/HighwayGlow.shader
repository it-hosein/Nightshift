Shader "Nightshift/Highway Glow"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Mask", 2D) = "white" {}
        _Shadow ("Shadow", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct Vert { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Vary { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };
            sampler2D _MainTex;
            fixed4 _Color;
            float _Shadow;
            Vary vert(Vert v)
            {
                Vary o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            half4 frag(Vary i) : SV_Target
            {
                half4 mask = tex2D(_MainTex,i.uv);
                half4 color = half4(_Color.rgb, _Color.a * lerp(mask.a, 1-mask.r, _Shadow));
                UNITY_APPLY_FOG(i.fogCoord,color);
                return color;
            }
            ENDCG
        }
    }
}
