Shader "GameMain/Presentation/PlayerTurnOrbFlow"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.4, 0.85, 1.0, 0.65)
        _Intensity ("Intensity", Float) = 1
        _FlowSpeed ("Flow Speed", Float) = 5
        _BandFreq ("Band Frequency", Float) = 8
        _RimPower ("Rim Power", Float) = 2
        _TwistSign ("Twist Sign (-L / +R)", Float) = 1
        _SpinAmount ("Spin Amount (yaw rate norm)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OrbFlow"
            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Intensity;
                float _FlowSpeed;
                float _BandFreq;
                float _RimPower;
                float _TwistSign;
                float _SpinAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posInputs.positionCS;
                output.normalWS = normInputs.normalWS;
                output.positionWS = posInputs.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(_Intensity - 0.001h);

                float3 N = normalize(input.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);

                float lat = atan2(N.z, N.x);
                float belt = sin(N.y * _BandFreq + _Time.y * _FlowSpeed + lat * 3.0 + _TwistSign * 2.5);
                belt = belt * 0.5 + 0.5;

                float spinBoost = lerp(1.0h, 1.85h, saturate(_SpinAmount));
                float rim = pow(1.0 - saturate(dot(N, V)), _RimPower);

                float pulse = saturate(rim * 0.55 + belt * 0.65) * spinBoost;
                half3 rgb = _BaseColor.rgb * pulse * _Intensity;
                half a = _BaseColor.a * saturate(_Intensity) * saturate(pulse + rim * 0.35);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
