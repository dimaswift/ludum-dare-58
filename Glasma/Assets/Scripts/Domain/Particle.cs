using System.Numerics;
using Unity.Mathematics;

namespace Glasma.Domain
{
    public struct Particle
    {
        public float3x3 spin;       
        public float3 position;  
        public float3 velocity;     
        public float3 core;    
        public float radius;   
        public float density;  
        public float horizon;      
        public float phase;     
        public float frequency;   
        public int depth;   
        public float size;   
        public float3 color0;
        public float3 color1;
        public float3 color2;
        public float3 color3;
        public float deltaTime;
        public float soften;
    }
}