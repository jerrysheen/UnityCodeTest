Shader "PBRShading"
{
    Properties
    {
        _DiffuseMap ("DiffuseMap", 2D) = "white" {}
        _BumpMap ("BumpMap", 2D) = "bump" {}
        _MetallicMap ("MetallicMap", 2D) = "white" {}
        _EmissionMap ("EmissionMap", 2D) = "white" {}
        _AoMap ("AoMap", 2D) = "white" {}
    }
  SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // -------------------------------------
            // Render State Commands

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Include/RenderingEquation.hlsl"
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 texcoord     : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv                       : TEXCOORD0;
                float4 positionCS               : SV_POSITION;
                float3 positionWS               : TEXCOORD4;
                float3 normalWS                 : TEXCOORD1;
                float4 tangentWS                : TEXCOORD2;
                float3 bitangentWS                : TEXCOORD3;
            };

            TEXTURE2D(_DiffuseMap);         SAMPLER(sampler_DiffuseMap);
            TEXTURE2D(_BumpMap);         SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicMap);        SAMPLER(sampler_MetallicMap);
            TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_AoMap);        SAMPLER(sampler_AoMap);
            
            Varyings LitPassVertex(Attributes Input)
            {
                Varyings o;
                o.uv = Input.texcoord;
                o.positionCS = TransformObjectToHClip(Input.positionOS);
                o.normalWS = TransformObjectToWorldNormal(Input.normalOS);
                real sign = Input.tangentOS.w * GetOddNegativeScale();
                o.tangentWS = float4(TransformObjectToWorldDir(Input.tangentOS), sign);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * sign;

                o.positionWS = TransformObjectToWorld(Input.positionOS);
                return o;
            }

            void LitPassFragment(Varyings Input, out half4 outColor : SV_Target0)
            {
                half4 diffuseMap = SAMPLE_TEXTURE2D(_DiffuseMap, sampler_DiffuseMap, Input.uv);
                half4 normalMap = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, Input.uv);
                half3 normalTS = UnpackNormal(normalMap);
                half4 metallicMap = SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, Input.uv);
                half4 emissionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, Input.uv);
                half4 aoMap = SAMPLE_TEXTURE2D(_AoMap, sampler_AoMap, Input.uv);
                // crossSign到底在做什么
                float crossSign = (Input.tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
                // ？ 为什么这个地方需要vs 插值一次 ps 插值一次
                float3 bitangent = crossSign * cross(Input.normalWS.xyz, Input.tangentWS.xyz);
                // 这边是rowmajor 还是Clomn major？
                half3x3 tangentToWorld = half3x3(Input.tangentWS.xyz, bitangent.xyz, Input.normalWS.xyz);
                half3 normalWS = TransformTangentToWorld(normalTS , tangentToWorld, true);

                float3 viewDir = normalize(GetWorldSpaceViewDir(Input.positionWS));
                float3 LightDIR = half3(_MainLightPosition.xyz);
                // for(int i = 0; i < ... ; i++)
                // {
                //     
                // }
                // 多灯部分....
                float metallic = metallicMap.r;
                float roughness = 1 - metallicMap.a;
                // NdotV的 V到底是哪里指向哪里？ LightDir到底是指向哪里？
                float NdotV = saturate(dot(normalWS, viewDir));
                float NdotL = saturate(dot(normalWS, LightDIR));
                float3 halfVector = normalize((viewDir + LightDIR));
                float3 surfaceColor = diffuseMap;
                float3 R0 = lerp(F0, surfaceColor, metallic);
                float3 radiance = _MainLightColor.xyz;
                // 注意是视线和 H的夹角，在180度的时候最强烈
                float3 F = BRDFFresnel(R0, saturate(dot(viewDir, halfVector)));
                float G = BRDFGeometrySmith(normalWS, viewDir, LightDIR, roughness);
                float D = BRDFNormalDistribution(normalWS, halfVector, roughness);
                // 金属不贡献diffuse
                float3 Kd = (1 - F) * (1 - metallic);
                float3 diffuse = Kd * surfaceColor.xyz / PI;
                float3 specular = (D * F * G) / (4 * NdotV * NdotL + 0.00001f);
                float3 directLight = (diffuse + specular) * NdotL * radiance;
                outColor = half4(directLight ,1.0f);
                return;
            }

            ENDHLSL
        }
    }
}
