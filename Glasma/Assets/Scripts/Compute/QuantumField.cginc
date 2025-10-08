#define QUARK_COUNT 4
#define PAIR_COUNT 6

#define PI 3.14159265358979323846

struct Particle
{
    float3x3 spin;
    float3 position;
    float3 velocity;
    float3 core;
    float radius;
    float density;
    float horizon;
    float phase;
    float frequency;
    int depth;
    float size;
    float3 colors[QUARK_COUNT];
    float soften;
    float deltaTime;
};

struct Quark { float3 position; float3 velocity;  float3 color; };
struct Trajectory { int step; float energy; };
struct FieldConfig
{
    float density;
    float radius;
    float frequency;
    float phase;
    float dt;
    float soften;
};

const static float h = sqrt(3.0f) / 2.0f;

const static  float3 VERTICES[QUARK_COUNT] = {
    // float3(0, 0, 0),
    // float3(-0.5f, -h/3.0f, 0.0f),  
    // float3( 0.5f, -h/3.0f, 0.0f),  
    // float3( 0.0f,  2.0f*h/3.0f, 0.0f)
    float3(0.35355339, 0.35355339, 0.35355339),  
float3(0.35355339, -0.35355339, -0.35355339), 
float3(-0.35355339, 0.35355339, -0.35355339),
float3(-0.35355339, -0.35355339, 0.35355339),
};
static const int2 PAIRS[PAIR_COUNT] = {
    int2(0,1), int2(0,2), int2(0,3),
    int2(1,2), int2(1,3), 
    int2(2,3), 
};

inline void computeAccelerations(
    in Quark bodies[QUARK_COUNT], 
    out float3 acc[QUARK_COUNT],
    float soften
) {
    
    [unroll]
    for(int i=0; i<QUARK_COUNT; ++i) {
        acc[i] = float3(0,0,0);
    }
    
    [unroll]
    for (int p=0; p<PAIR_COUNT; ++p) {
        int i = PAIRS[p].x, j = PAIRS[p].y;
        float3 r = bodies[j].position - bodies[i].position;
        
        float r2 = dot(r, r) + soften;
        float inv  = rsqrt(r2);
        float inv3 = inv * inv * inv;
        
        float3 aij = r * inv3;
        float3 aji = -r * inv3;

       // aij = mul(bodies[j].color, aij);
       // aji = mul(bodies[i].color, aji);
        
        acc[i] += aij;
        acc[j] += aji;
    }
}

const static float3 TETRAHEDRON[4] = {
    float3(0.35355339, 0.35355339, 0.35355339),  
    float3(0.35355339, -0.35355339, -0.35355339), 
    float3(-0.35355339, 0.35355339, -0.35355339),
    float3(-0.35355339, -0.35355339, 0.35355339),
};

inline void integrateLeapfrog(
    inout Quark bodies[QUARK_COUNT],
    float3 accel[QUARK_COUNT],
    float dt,
    float soften
) {
    [unroll]
    for (int i = 0; i < QUARK_COUNT; ++i) {
        bodies[i].position += bodies[i].velocity * dt;
    }
    computeAccelerations(bodies, accel, soften);
    [unroll]
    for (int i = 0; i < QUARK_COUNT; ++i) {
        bodies[i].velocity += accel[i] * dt;
    }
}

struct Photon 
{
    int frequency;
    float amplitude;
    float phase;
    float radius;
    float density;
    float scale;
};

inline float sampleTetrahedral(const float3 pos, Photon photon)
{
    float d = 0.0;
    const float3 p0 = float3(0.35355339, 0.35355339, 0.35355339);
    const float3 p1 = float3(0.35355339, -0.35355339, -0.35355339);
    const float3 p2 = float3(-0.35355339, 0.35355339, -0.35355339);
    const float3 p3 = float3(-0.35355339, -0.35355339, 0.35355339);

    float rad = photon.radius;
    
    float dist = saturate(distance(pos * photon.scale, p0 * photon.scale) * rad);
    d += sin(dist * photon.frequency + photon.phase) * photon.amplitude;

    dist = saturate(distance(pos * photon.scale, p1 * photon.scale) * rad);
    d += sin(dist * photon.frequency + photon.phase) * photon.amplitude;

    dist = saturate(distance(pos * photon.scale, p2 * photon.scale) * rad);
    d += sin(dist * photon.frequency + photon.phase) * photon.amplitude;
    
    dist = saturate(distance(pos * photon.scale, p3 * photon.scale) * rad);
    d += sin(dist * photon.frequency + photon.phase) * photon.amplitude;
    
    return d;
}


inline float sampleMatrix(const float3 pos, Photon photon)
{
    float d = 0.0;
    float rad = photon.radius;
    [unroll]
    for (int x = -1; x <= 1; x++)
    {
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            [unroll]
            for (int z = -1; z <= 1; z++)
            {
                const float3 p = float3(x,y,z);
                float3 transformed = p;
                float dist = saturate(distance(pos * photon.scale, transformed * photon.scale) * rad);
                d += sin(dist * photon.frequency + photon.phase) * photon.amplitude;
            }
        }
    }
    return d;
}

Trajectory SampleField(
    int steps,
    float3 position, 
    float3 corePosition,
    float escapeR2,
    FieldConfig cfg,
    Photon photon
){
    Quark quarks[QUARK_COUNT];

    float waveFunction = sampleTetrahedral(position, photon);
    
    [unroll]
    for (int i=0; i<QUARK_COUNT; ++i)
    {
        quarks[i].position = sin(-length(position - (VERTICES[i]) * cfg.radius) * cfg.frequency + cfg.phase) * cfg.density;
        quarks[i].velocity = float3(0,0,0);
        quarks[i].color = float3(1,1,1);
    }

    //higgs?
    quarks[0].position = corePosition;
    
    float3 acc[QUARK_COUNT];
    
    int esc=steps;
    bool alive=true;
    float energy=0.0;
    
    integrateLeapfrog(quarks, acc, cfg.dt, cfg.soften);
        
    float maxR2=0.0;

    for (int s = 0; s < steps; ++s)
    {
        if (alive)
        {
          [unroll]
          for (int i=0;i<QUARK_COUNT;++i)
          {
              float v = dot(quarks[i].velocity, quarks[i].velocity);
              energy += v;
              maxR2 = max(maxR2, dot(quarks[i].velocity, quarks[i].velocity));

              if(maxR2 > escapeR2)
              {
                  esc = s;
                  alive = false;
              }
          }
        }
    }
    
    Trajectory r;
    r.step=esc;
    r.energy=energy  * waveFunction;
    return r;
}
