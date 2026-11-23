Shader "Nightshift/Moon Sky"
{
    Properties
    {
        _MainTex ("HDR Panorama", 2D) = "grey" {}
        _Exposure ("Exposure", Range(0, 8)) = 0.2
        _Rotation ("Rotation", Range(0, 360)) = 157
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _MainTex_HDR;
            half _Exposure;
            float _Rotation;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(float4 vertex : POSITION)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(vertex);
                o.direction = vertex.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 direction = normalize(i.direction);
                float longitude = atan2(direction.z, direction.x) / (2 * UNITY_PI);
                float latitude = asin(clamp(direction.y, -1, 1)) / UNITY_PI;
                float2 uv = float2(0.5 - longitude + _Rotation / 360.0, latitude + 0.5);
                half3 sky = DecodeHDR(tex2D(_MainTex, uv), _MainTex_HDR);
                return half4(sky * _Exposure, 1);
            }
            ENDCG
        }
    }
}
