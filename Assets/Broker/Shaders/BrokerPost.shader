// Screen pass of the eye cameras (Runtime/Office/CameraPost.cs): screen-space occlusion from the depth texture,
// a bloom pyramid, the ACES curve with a warm grade, vignette and dither. Values come from Broker.Core.ScreenLook;
// the offline preview (Tools/ScenePreview) applies the same maths on the CPU.
// Passes: 0 bloom prefilter, 1 downsample, 2 upsample, 3 occlusion, 4 occlusion blur, 5 composite.
Shader "Hidden/Broker/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    #define OCCLUSION_SAMPLES 12

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BaseTex;
    sampler2D _BloomTex;
    float4 _BloomTex_TexelSize;
    sampler2D _OcclusionTex;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

    float4 _Bloom;     // threshold, knee, intensity
    float4 _Occlusion; // radius (m), strength, fade start (m), fade end (m)
    float4 _Grade;     // exposure, saturation, contrast, vignette
    float4 _Warm;      // white balance rgb, mid grey
    float4 _BlurStep;  // uv step of one blur tap

    struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 depthUV : TEXCOORD1; };

    v2f vert (appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        o.depthUV = v.uv;
        // The source can arrive upside down (anti-aliasing on Direct3D); the depth texture never does.
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0) o.depthUV.y = 1 - o.depthUV.y;
        #endif
        return o;
    }

    float Noise (float2 pixel) { return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))); }

    float3 Box4 (sampler2D tex, float2 uv, float2 texel)
    {
        float4 d = texel.xyxy * float4(-1, -1, 1, 1);
        float3 s = tex2D(tex, uv + d.xy).rgb + tex2D(tex, uv + d.zy).rgb + tex2D(tex, uv + d.xw).rgb + tex2D(tex, uv + d.zw).rgb;
        return s * 0.25;
    }

    float3 Tent9 (sampler2D tex, float2 uv, float2 texel)
    {
        float4 d = texel.xyxy * float4(1, 1, -1, 0);
        float3 s = tex2D(tex, uv - d.xy).rgb;
        s += tex2D(tex, uv - d.wy).rgb * 2;
        s += tex2D(tex, uv - d.zy).rgb;
        s += tex2D(tex, uv + d.zw).rgb * 2;
        s += tex2D(tex, uv).rgb * 4;
        s += tex2D(tex, uv + d.xw).rgb * 2;
        s += tex2D(tex, uv + d.zy).rgb;
        s += tex2D(tex, uv + d.wy).rgb * 2;
        s += tex2D(tex, uv + d.xy).rgb;
        return s * (1.0 / 16);
    }

    float4 fragPrefilter (v2f i) : SV_Target
    {
        float3 c = Box4(_MainTex, i.uv, _MainTex_TexelSize.xy);
        #ifdef UNITY_COLORSPACE_GAMMA
        c = GammaToLinearSpace(c);
        #endif
        c = min(c, 64.0);
        float brightest = max(c.r, max(c.g, c.b));
        float soft = clamp(brightest - _Bloom.x + _Bloom.y, 0, 2 * _Bloom.y);
        soft = soft * soft / (4 * _Bloom.y + 1e-5);
        c *= max(soft, brightest - _Bloom.x) / max(brightest, 1e-5);
        return float4(c, 1);
    }

    float4 fragDown (v2f i) : SV_Target
    {
        return float4(Box4(_MainTex, i.uv, _MainTex_TexelSize.xy), 1);
    }

    float4 fragUp (v2f i) : SV_Target
    {
        return float4(tex2D(_BaseTex, i.uv).rgb + Tent9(_MainTex, i.uv, _MainTex_TexelSize.xy), 1);
    }

    float EyeDepth (float2 uv)
    {
        return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(uv, 0, 0)));
    }
    // View-space position with +z forward from a depth-texture uv and eye depth, and back.
    float3 ViewPosition (float2 uv, float depth)
    {
        float2 scale = float2(unity_CameraProjection._11, unity_CameraProjection._22);
        float2 shift = float2(unity_CameraProjection._13, unity_CameraProjection._23);
        return float3((uv * 2 - 1 - shift) / scale * depth, depth);
    }
    float2 DepthUV (float3 p)
    {
        float2 scale = float2(unity_CameraProjection._11, unity_CameraProjection._22);
        float2 shift = float2(unity_CameraProjection._13, unity_CameraProjection._23);
        return (p.xy / p.z * scale + shift + 1) * 0.5;
    }

    // Hemisphere samples around the face normal (rebuilt from depth); a sample counts when the scene in front of it
    // is closer than the sample, weighted down for depth gaps wider than the radius. Output: occlusion, depth / far.
    float4 fragOcclusion (v2f i) : SV_Target
    {
        float far = _ProjectionParams.z;
        float depth = EyeDepth(i.depthUV);
        if (depth >= far * 0.99) return float4(1, 1, 0, 1);
        float3 p = ViewPosition(i.depthUV, depth);
        float3 n = normalize(cross(ddy(p), ddx(p)));
        if (dot(n, p) > 0) n = -n;
        float noise = Noise(i.pos.xy);
        float radius = _Occlusion.x, occluded = 0;
        [unroll]
        for (int k = 0; k < OCCLUSION_SAMPLES; k++)
        {
            float z = 1 - (k + 0.5) * (2.0 / OCCLUSION_SAMPLES);
            float r = sqrt(saturate(1 - z * z));
            float phi = k * 2.39996323 + noise * 6.28318531;
            float3 dir = float3(r * cos(phi), r * sin(phi), z);
            if (dot(dir, n) < 0) dir = -dir;
            float scale = frac((k + 0.5) / OCCLUSION_SAMPLES + noise * 0.618034);
            scale = lerp(0.15, 1.0, scale * scale);
            float3 s = p + dir * (radius * scale);
            float sceneDepth = EyeDepth(DepthUV(s));
            float range = saturate(radius / max(abs(depth - sceneDepth), 1e-4));
            occluded += sceneDepth < s.z - 0.03 - depth * 0.002 ? range : 0;
        }
        float fade = saturate((depth - _Occlusion.z) / max(_Occlusion.w - _Occlusion.z, 1e-3));
        float ao = 1 - _Occlusion.y * (1 - fade) * occluded / OCCLUSION_SAMPLES;
        return float4(ao, depth / far, 0, 1);
    }

    // Separable blur that keeps occlusion from bleeding across depth edges.
    float4 fragBlur (v2f i) : SV_Target
    {
        float2 centre = tex2D(_MainTex, i.uv).rg;
        float sum = centre.r, weights = 1;
        [unroll]
        for (int k = 1; k <= 3; k++)
        {
            float2 offset = _BlurStep.xy * k;
            float2 a = tex2D(_MainTex, i.uv + offset).rg;
            float2 b = tex2D(_MainTex, i.uv - offset).rg;
            float g = exp(-k * k / 4.5);
            float tolerance = centre.g * 0.05 + 1e-4;
            float wa = g * saturate(1 - abs(a.g - centre.g) / tolerance);
            float wb = g * saturate(1 - abs(b.g - centre.g) / tolerance);
            sum += a.r * wa + b.r * wb;
            weights += wa + wb;
        }
        return float4(sum / weights, centre.g, 0, 1);
    }

    float3 Aces (float3 x)
    {
        x = max(x, 0);
        return saturate(x * (2.51 * x + 0.03) / (x * (2.43 * x + 0.59) + 0.14));
    }

    float4 fragComposite (v2f i) : SV_Target
    {
        float3 c = tex2D(_MainTex, i.uv).rgb;
        #ifdef UNITY_COLORSPACE_GAMMA
        c = GammaToLinearSpace(c);
        #endif
        c *= tex2D(_OcclusionTex, i.uv).r;
        c += Tent9(_BloomTex, i.uv, _BloomTex_TexelSize.xy) * _Bloom.z;
        c = Aces(c * _Grade.x * _Warm.rgb);
        float luma = dot(c, float3(0.2126, 0.7152, 0.0722));
        c = luma + (c - luma) * _Grade.y;
        c = min(1, _Warm.w * pow(max(c, 1e-6) / _Warm.w, _Grade.z));
        float2 v = (i.uv - 0.5) * 2;
        float aspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1);
        float d = (v.x * v.x * aspect * aspect + v.y * v.y) / (aspect * aspect + 1);
        c *= 1 - _Grade.w * d * d;
        // Dither in display space against banding in the haze and the sky.
        c = LinearToGammaSpace(c) + (Noise(i.pos.xy) - 0.5) / 255.0;
        #ifndef UNITY_COLORSPACE_GAMMA
        c = GammaToLinearSpace(c);
        #endif
        return float4(c, 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUp
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragOcclusion
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            #pragma target 3.0
            ENDCG
        }
    }
    FallBack Off
}
