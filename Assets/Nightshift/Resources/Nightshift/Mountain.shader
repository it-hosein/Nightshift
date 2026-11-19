Shader "Nightshift/Mountain"
{
    Properties { _MainTex ("Rock", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Lambert
        sampler2D _MainTex;
        struct Input { float2 uv_MainTex; fixed4 color : COLOR; };
        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb * IN.color.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
