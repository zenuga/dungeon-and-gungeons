#ifndef STOCHASTIC_SAMPLING_INCLUDED
#define STOCHASTIC_SAMPLING_INCLUDED

// Hash function to derive pseudo-random values per grid cell
float2 StochasticHash(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453123);
}

// Rotates UV coordinates around Vector2(0.5, 0.5) in 90-degree increments
float2 RotateUVSteps(float2 uv, float angleStep)
{
    uv -= 0.5;
    float angle = angleStep * 1.57079632679;
    float s = sin(angle);
    float c = cos(angle);
    float2 rotated = float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
    return rotated + 0.5;
}

// Primary Float Precision Implementation
void StochasticSample_float(
    UnityTexture2D TileTexture,
    UnitySamplerState TileSampler,
    float2 UV,
    float TileScale,
    out float4 OutColor)
{
    // Guaranteed immediate assignment to prevent uninitialized HLSL error
    OutColor = float4(0.0, 0.0, 0.0, 0.0);

    float2 scaledUV = UV * TileScale;
    float2 gridCell = floor(scaledUV);
    float2 fracUV = frac(scaledUV);

    float4 accumulatedColor = float4(0.0, 0.0, 0.0, 0.0);
    float totalWeight = 0.0;

    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            float2 neighborOffset = float2((float)x, (float)y);
            float2 cellID = gridCell + neighborOffset;
            float2 cellHash = StochasticHash(cellID);
            
            float rotStep = floor(cellHash.x * 4.0);
            float2 cellUV = fracUV - neighborOffset;
            float2 rotatedUV = RotateUVSteps(cellUV, rotStep) + cellHash;

            float dist = length(cellUV - float2(0.5, 0.5));
            float weight = exp(-8.0 * dist * dist);

            float4 sampledColor = TileTexture.Sample(TileSampler, rotatedUV);

            accumulatedColor += sampledColor * weight;
            totalWeight += weight;
        }
    }

    OutColor = accumulatedColor / max(totalWeight, 0.0001);
}

// Half Precision Overload
void StochasticSample_half(
    UnityTexture2D TileTexture,
    UnitySamplerState TileSampler,
    half2 UV,
    half TileScale,
    out half4 OutColor)
{
    float4 tempColor = float4(0.0, 0.0, 0.0, 0.0);
    StochasticSample_float(TileTexture, TileSampler, (float2)UV, (float)TileScale, tempColor);
    OutColor = (half4)tempColor;
}

#endif