#define F0 float3(0.04, 0.04, 0.04)
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Macros.hlsl"
// Xi.xy ∈ 0 ~ 1
float3 ImportanceSampleGGX(float2 Xi, float3 N, float roughness)
{
    float a = roughness*roughness;

    // phi ∈ 0~ 2PI, Xi.y ∈ 0 ~ 1 a ∈ 0 ~ 1
    // cosTheta， 看看值域， a = 1的时候， cosTheta 0~1
    // a = 1的时候，cosTheata = 1;
    // 这个地方可以理解为生成了一个斜向量，根据a来确定斜度，然后把斜向量拆成竖直水平两个分量。
    float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a*a - 1.0) * Xi.y));
    float sinTheta = sqrt(1.0 - cosTheta*cosTheta);

    // from spherical coordinates to cartesian coordinates
    // 再把水平分量做旋转，旋转沿着2PI正好一圈。
    // 这个地方就形成了一个围绕着z轴的一个圆锥形。
    // 或者说，这个地方是沿着 上方向（z）方向采样得到一个圆锥矩形。
    float phi = 2.0 * PI * Xi.x;
    float3 H;
    H.x = cos(phi) * sinTheta;
    H.y = sin(phi) * sinTheta;
    H.z = cosTheta;

    // from tangent-space floattor to world-space sample floattor
    // 将这个向量往 法线方向进行拆分
    float3 up        = abs(N.z) < 0.999 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
    float3 tangent   = normalize(cross(up, N));
    float3 bitangent = cross(N, tangent);

    // 这一步咋理解？我理解为一个空间旋转？相当于把这个H,本来是沿着Z轴的一个圆锥，旋转到沿着Normal方向
    float3 samplefloat = tangent * H.x + bitangent * H.y + N * H.z;
    return normalize(samplefloat);
}

float RadicalInverse_VdC(uint bits) 
{
    bits = (bits << 16u) | (bits >> 16u);
    bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
    bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
    bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
    bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
    return float(bits) * 2.3283064365386963e-10; // / 0x100000000
}
// ----------------------------------------------------------------------------
// 低差差异序列，是指随机的更加均匀的序列，帮助我们加速蒙特卡洛收敛
// 给定一个总数N，和当前indexi，生成一个均匀的xy在 0~1的float2
float2 Hammersley(uint i, uint N)
{
    return float2(float(i)/float(N), RadicalInverse_VdC(i));
}  

float BRDFNormalDistribution(float3 normal, float3 halffloattor, float roughness)
{
    roughness = clamp(roughness, 0.04, 1.0);
    float a2 = roughness * roughness;
    float NdotH = saturate(dot(normal, halffloattor));
    float NdotHPow2 = NdotH * NdotH;
    float norm = PI * pow(NdotHPow2 * (a2 - 1) + 1, 2);
    return a2 / norm;
    
}

float GeometrySchlickGGXDirect(float NdotX, float roughness)
{
    float Kdirect = pow(roughness + 1, 2) / 8;
    return NdotX / (NdotX * (1 - Kdirect) + Kdirect);
}

float BRDFGeometrySmithDirect(float3 Normal, float3 View, float3 LightDir, float roughness)
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


float GeometrySchlickGGXIBL(float NdotX, float roughness)
{
    float Kdirect = pow(roughness, 2) / 2;
    return NdotX / (NdotX * (1 - Kdirect) + Kdirect);
}

float BRDFGeometrySmithIBL(float3 Normal, float3 View, float3 LightDir, float roughness)
{
    float NdotV = max(dot(Normal, View), 0.0);
    float NdotL = max(dot(Normal, LightDir), 0.0);
    float ggx2  = GeometrySchlickGGXIBL(NdotV, roughness);
    float ggx1  = GeometrySchlickGGXIBL(NdotL, roughness);
    return ggx1 * ggx2;
}
