#define SHAPE_CUBE 0
#define SHAPE_SPHERE 1
#define SHAPE_CYLINDER 2
#define SHAPE_CAPSULE 3

struct SculptSolid
{
    float4x4 invTransform;
    float scale;    
    int type; 
    int op;
    float feather;
    float exp;
    float lerp;
    int priority;
};

float3 transformPoint(float4x4 m, float3 p)
{
    float4 worldPoint = float4(p, 1.0);
    float4 localPoint = mul(m, worldPoint);
    return localPoint.xyz;
}

// Distance functions for different shapes
float cubeDistance(float3 localPos)
{
    // For a unit cube centered at origin with extents [-0.5, 0.5]
    float3 absPos = abs(localPos);
    float3 outsideDistance = max(absPos - 0.5, 0.0);
    float outsideDist = length(outsideDistance);
    
    // Inside distance (negative when inside)
    float insideDist = max(max(absPos.x, absPos.y), absPos.z) - 0.5;
    
    return max(outsideDist, insideDist);
}

float sphereDistance(float3 localPos)
{
    // For a unit sphere centered at origin with radius 0.5
    return length(localPos) - 0.5;
}

float cylinderDistance(float3 localPos)
{
    // For a unit cylinder along Y axis, radius 0.5, height 1.0 (from -0.5 to 0.5)
    float2 xzPos = localPos.xz;
    float radialDist = length(xzPos) - 0.5;
    float heightDist = abs(localPos.y) - 0.5;
    
    float outsideDist = length(max(float2(radialDist, heightDist), 0.0));
    float insideDist = max(radialDist, heightDist);
    
    return max(outsideDist, insideDist);
}

float capsuleDistance(float3 localPos)
{
    // For a unit capsule along Y axis, radius 0.5, height 1.0
    float yPos = localPos.y;
    yPos = max(abs(yPos) - 0.25, 0.0) * sign(yPos); // Clamp to cylinder part
    
    float2 offset = float2(length(localPos.xz), yPos);
    return length(offset) - 0.5;
}

// Get distance based on shape type
float getShapeDistance(float3 localPos, int shapeType)
{
    switch(shapeType)
    {
    case SHAPE_CUBE:
        return cubeDistance(localPos);
    case SHAPE_SPHERE:
        return sphereDistance(localPos);
    case SHAPE_CYLINDER:
        return cylinderDistance(localPos);
    case SHAPE_CAPSULE:
        return capsuleDistance(localPos);
    default:
        return cubeDistance(localPos);
    }
}

float calculateMask(float distance, float thickness)
{
    if (distance <= 0.0)
        return 1.0;
    if (distance >= thickness)
        return 0.0;
    return smoothstep(1.0, 0.0, distance / thickness);
}

float calculateMaskLinear(float distance, float thickness)
{
    return saturate(1.0 - distance / thickness);
}

float calculateMaskExponential(float distance, float thickness, float falloffPower)
{
    if (distance <= 0.0)
        return 1.0;
    
    float normalizedDist = saturate(distance / thickness);
    return pow(1.0 - normalizedDist, falloffPower);
}

float combineMasks(float mask1, float mask2, int combineMode)
{
    switch(combineMode)
    {
    case 0: return max(mask1, mask2);           // Union
    case 1: return min(mask1, mask2);           // Intersection
    case 2: return saturate(mask1 - mask2);     // Subtract
    case 3: return saturate(mask1 + mask2);     // Additive
    default: return max(mask1, mask2);
    }
}

float evaluateSolidField(float3 p, SculptSolid solid)
{
    float4x4 worldToLocal = solid.invTransform;

    float3 localPos = transformPoint(worldToLocal, p);

    float distance = getShapeDistance(localPos, solid.type);

    float shapeMask = calculateMask(distance, solid.feather);
    
    return shapeMask * solid.scale;
}
