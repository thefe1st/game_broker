// Voxel props: palette albedo darkened by the per-vertex corner shade from VoxelMesher (ambient occlusion in the vertex colour),
// Standard lighting, shadows and fog. The glowing submesh uses the same shader with an emission colour.
// OfficeScene falls back to the Standard shader (without corner shade) when this one is missing.
Shader "Broker/Voxel"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Palette", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.08
        _Metallic ("Metallic", Range(0, 1)) = 0
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _EmissionMap ("Emission map", 2D) = "white" {}
        _CornerShade ("Corner shade on direct light", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _EmissionMap;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        half4 _EmissionColor;
        half _CornerShade;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            // Vertex colour red = corner shade (1 = open corner). Ambient light gets all of it, direct light a share.
            half shade = saturate(IN.color.r);
            o.Albedo = albedo.rgb * lerp(1.0, shade, _CornerShade);
            o.Occlusion = shade;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = tex2D(_EmissionMap, IN.uv_MainTex).rgb * _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
