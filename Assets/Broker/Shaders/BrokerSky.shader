// Golden-hour sky of the city: a warm horizon equal to the haze, a cooler zenith, a halo around the low sun
// and soft clouds lit from the sun side. Mirrors Core/SkyLook.cs line for line (the offline preview evaluates that);
// the scene writes every value from SkyLook into the material, the defaults below are the same numbers.
Shader "Broker/Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.42, 0.52, 0.70, 1)
        _Horizon ("Horizon (= haze)", Color) = (0.90, 0.68, 0.52, 1)
        _Ground ("Below the horizon", Color) = (0.62, 0.50, 0.42, 1)
        _Glow ("Sun glow", Color) = (1, 0.64, 0.36, 1)
        _CloudLit ("Lit cloud", Color) = (1, 0.87, 0.76, 1)
        _CloudShade ("Cloud shade", Color) = (0.66, 0.58, 0.64, 1)
        _SunDirection ("Direction toward the sun", Vector) = (-0.875, 0.376, -0.305, 0)
        _Sun ("Halo, halo power, glow, glow power", Vector) = (0.55, 5, 1.4, 160)
        _Disk ("Disk, disk cos, soft cos, horizon falloff", Vector) = (16, 0.99955, 0.99975, 4.5)
        _Cloud ("Cover, softness, scale, lift", Vector) = (0.42, 0.2, 1.5, 0.18)
        _CloudShape ("Stretch, drift, fade low, fade high", Vector) = (0.5, 0.004, 0.015, 0.22)
        _CloudLight ("Opacity, light step, light gain, silver", Vector) = (0.85, 0.16, 3.5, 0.8)
        _CloudHaze ("Haze over far clouds", Float) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            half4 _Zenith, _Horizon, _Ground, _Glow, _CloudLit, _CloudShade;
            float4 _SunDirection, _Sun, _Disk, _Cloud, _CloudShape, _CloudLight;
            float _CloudHaze;
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 ray : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.ray = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }
            // Value noise over a sine-free hash (D. Hoskins, hash12), four octaves.
            float Hash(float2 p)
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = p - i, u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            float Fbm(float2 p)
            {
                float sum = 0, amplitude = 0.5;
                for (int octave = 0; octave < 4; octave++) { sum += amplitude * Noise(p); p = p * 2.02 + float2(5.3, 1.7); amplitude *= 0.5; }
                return sum / 0.9375;
            }
            float Power(float x, float p) { return x <= 0 ? 0 : pow(max(x, 1e-6), p); }
            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.ray), s = normalize(_SunDirection.xyz);
                float band = exp(-max(d.y, 0) * _Disk.w);
                float3 color = lerp(_Zenith.rgb, _Horizon.rgb, band);
                if (d.y < 0) color = lerp(_Horizon.rgb, _Ground.rgb, saturate(-d.y * 6));
                float cosSun = dot(d, s), c = max(cosSun, 0);
                color += _Glow.rgb * (_Sun.x * Power(c, _Sun.y) * band + _Sun.z * Power(c, _Sun.w));
                float cloud = 0;
                if (d.y > 0)
                {
                    float inv = _Cloud.z / (d.y + _Cloud.w);
                    float2 uv = float2(d.x * inv * _CloudShape.x + _Time.y * _CloudShape.y, d.z * inv);
                    float n = Fbm(uv), toward = Fbm(uv + s.xz * _CloudLight.y);
                    cloud = smoothstep(1 - _Cloud.x - _Cloud.y, 1 - _Cloud.x + _Cloud.y, n) * smoothstep(_CloudShape.z, _CloudShape.w, d.y) * _CloudLight.x;
                    float lit = saturate(0.5 + (n - toward) * _CloudLight.z);
                    float3 cloudColor = lerp(_CloudShade.rgb, _CloudLit.rgb, lit) + _Glow.rgb * (_CloudLight.w * Power(c, 8));
                    cloudColor = lerp(cloudColor, _Horizon.rgb, band * _CloudHaze);
                    color = lerp(color, cloudColor, cloud);
                }
                color += _Glow.rgb * (_Disk.x * smoothstep(_Disk.y, _Disk.z, cosSun) * (1 - cloud));
                return half4(color, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
