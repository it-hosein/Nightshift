Shader "Nightshift/Headlamp Lens"
{
    Properties
    {
        [HDR] _EmissionColor ("Lens emission", Color) = (1.30, 1.58, 1.95, 1)
    }
    SubShader
    {
        Tags { "Queue"="Geometry+4" "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            half4 _EmissionColor;
            struct Varyings
            {
                float4 vertex : SV_POSITION;
                UNITY_FOG_COORDS(0)
            };
            Varyings vert(appdata_base v)
            {
                Varyings o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 color = _EmissionColor;
                UNITY_APPLY_FOG(i.fogCoord,color);
                return color;
            }
            ENDCG
        }
    }
}
