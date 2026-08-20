Shader "Liz/SimpleInstanced"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Color;

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Without this unity_ObjectToWorld stays at instance 0 and every
                // cube draws on top of the first one.
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 worldPos = TransformObjectToWorld(IN.positionOS);
                OUT.positionCS = TransformWorldToHClip(worldPos);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                return _Color;
            }

            ENDHLSL
        }
    }
}
