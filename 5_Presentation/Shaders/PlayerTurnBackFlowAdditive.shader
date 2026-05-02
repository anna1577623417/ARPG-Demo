Shader "GameMain/Presentation/PlayerTurnBackFlowAdditive"
{
    Properties
    {
        [Header(Turn control)]
        _TurnIntensity ("Turn Intensity", Float) = 0
        _TurnColor ("Turn Rim Color", Color) = (0.35, 0.85, 1.0, 1)
        _TurnForwardWS ("Turn Forward (WS)", Vector) = (0, 0, 1, 0)
        _TurnSign ("Turn Sign (+R / -L)", Float) = 1
        [Header(Back anchor)]
        _BiasRight ("Bias Right (char space)", Float) = 0
        _BiasUp ("Bias Up (world)", Float) = 0
        _BackSharpness ("Back Mask Sharpness", Float) = 2
        [Header(Flow)]
        _FlowFrequency ("Flow Frequency", Float) = 4
        _FlowSpeed ("Flow Speed", Float) = 6
        _RimPower ("View Rim Power", Float) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "TurnBackFlowAdditive"
            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _TurnIntensity;
                float4 _TurnColor;
                float4 _TurnForwardWS;
                float _TurnSign;
                float _BiasRight;
                float _BiasUp;
                float _BackSharpness;
                float _FlowFrequency;
                float _FlowSpeed;
                float _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posInputs.positionCS;
                output.normalWS = normInputs.normalWS;
                output.positionWS = posInputs.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_TurnIntensity - 0.0001h);

                float3 N = normalize(input.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);

                float3 F = _TurnForwardWS.xyz;
                F.y = 0;
                float fl = length(F);
                F = fl > 1e-4 ? F * (1.0 / fl) : float3(0, 0, 1);

                float3 upW = float3(0, 1, 0);
                float3 Rch = normalize(cross(upW, F));
                float3 backDir = -F + Rch * _BiasRight + upW * _BiasUp;
                float bl = length(backDir);
                backDir = bl > 1e-4 ? backDir * (1.0 / bl) : -F;

                float ndb = saturate(dot(N, backDir));
                float backMask = pow(ndb, _BackSharpness);
                float rim = pow(1.0 - saturate(dot(N, V)), _RimPower);

                float lateral = dot(N, Rch);
                float flow = sin(lateral * _FlowFrequency + _Time.y * _FlowSpeed + _TurnSign * 3.14159265) * 0.5 + 0.5;
                float pulse = lerp(0.35h, 1.0h, flow);

                float mask = backMask * rim * _TurnIntensity;
                half3 emit = _TurnColor.rgb * mask * pulse;
                return half4(emit, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
