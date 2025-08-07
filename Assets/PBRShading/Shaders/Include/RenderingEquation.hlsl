
#define F0 float3(0.04, 0.04, 0.04)

float BRDFNormalDistribution(float3 normal, float3 halfVector, float roughness)
{
    roughness = clamp(roughness, 0.04, 1.0);
    float a2 = roughness * roughness;
    float NdotH = saturate(dot(normal, halfVector));
    float NdotHPow2 = NdotH * NdotH;
    float norm = PI * pow(NdotHPow2 * (a2 - 1) + 1, 2);
    return a2 / norm;
    
}

float GeometrySchlickGGXDirect(float NdotX, float roughness)
{
    float Kdirect = pow(roughness + 1, 2) / 8;
    return NdotX / (NdotX * (1 - Kdirect) + Kdirect);
}

float BRDFGeometrySmith(float3 Normal, float3 View, float3 LightDir, float roughness)
{
    float NdotV = max(dot(Normal, View), 0.0);
    float NdotL = max(dot(Normal, LightDir), 0.0);
    float ggx2  = GeometrySchlickGGXDirect(NdotV, roughness);
    float ggx1  = GeometrySchlickGGXDirect(NdotL, roughness);
    return ggx1 * ggx2;
}

float3 BRDFFresnel(float3 R0, float VdotH)
{
    return R0 + (1 - R0) * pow(clamp(1.0 - VdotH, 0.0, 1.0), 5);
}