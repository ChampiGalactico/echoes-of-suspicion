Shader "Echoes/ScreenEffects"
{
    Properties
    {
        // Controlled from C# via global shader properties.
        // Listed here for reference only.
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "ScreenEffects"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // ── Global properties set from C# ───────────────────

            // Damage / heal flash.
            float4 _ScreenFX_FlashColor;   // rgb = color, a = intensity
            float  _ScreenFX_FlashAmount;  // 0..1

            // Persistent low-health vignette.
            float  _ScreenFX_LowHealthAmount; // 0..1

            // Detection pulse.
            float3 _ScreenFX_DetectColor;     // rgb
            float  _ScreenFX_DetectAmount;    // 0..1

            // ── Helpers ─────────────────────────────────────────

            // Smooth vignette mask: 1 at edges, 0 at center.
            // Higher start = thinner border (only at screen edges).
            float VignetteMask(float2 uv, float softness)
            {
                float2 centered = uv - 0.5;
                float dist = length(centered) * 2.0; // 0 center, ~1.41 corners
                return smoothstep(0.6, 1.4 - softness, dist);
            }

            // Sharper pulse mask for detection — covers most of the screen.
            float PulseMask(float2 uv)
            {
                float2 centered = uv - 0.5;
                float dist = length(centered) * 2.0;
                return smoothstep(0.0, 1.1, dist);
            }

            // ── Fragment ────────────────────────────────────────

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 scene = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                float3 result = scene.rgb;

                // 1. Damage / heal flash vignette.
                if (_ScreenFX_FlashAmount > 0.001)
                {
                    float mask = VignetteMask(uv, 0.2);
                    float alpha = mask * _ScreenFX_FlashAmount * _ScreenFX_FlashColor.a;
                    result = lerp(result, _ScreenFX_FlashColor.rgb, alpha);
                }

                // 2. Persistent low-health red vignette.
                if (_ScreenFX_LowHealthAmount > 0.001)
                {
                    float mask = VignetteMask(uv, 0.0);
                    float alpha = mask * _ScreenFX_LowHealthAmount;
                    result = lerp(result, float3(0.6, 0.0, 0.0), alpha);
                }

                // 3. Detection pulse — color driven from C#.
                if (_ScreenFX_DetectAmount > 0.001)
                {
                    float mask = PulseMask(uv);
                    float alpha = mask * _ScreenFX_DetectAmount;
                    result = lerp(result, _ScreenFX_DetectColor, alpha);
                }

                return float4(result, 1.0);
            }
            ENDHLSL
        }
    }
}
