Shader "Glasma/Particle"  
{  
    Properties  
    {  
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.5
        _Size ("Size", Range(0.0, 1.0)) = 0.5
    }  
    SubShader  
    {  
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }  
        Blend SrcAlpha OneMinusDstColor  
        
        Pass  
        {  
            CGPROGRAM  

            #pragma vertex vert   
            #pragma fragment frag  
            #include "Assets/Scripts/Compute/QuantumField.cginc"
   
            struct appdata_t  
            {  
                float4 vertex : POSITION;  
                float3 normal : NORMAL;
                
            };  

            struct v2f  
            {  
                float4 vertex : SV_POSITION;  
                float3 world_pos : TEXCOORD0;
                float3 color : TEXCOORD1;
             //   float3 world_normal : TEXCOORD2;
            };
            
            
            StructuredBuffer<Particle> Particles;
           
            float _Size;

            v2f vert(appdata_t i, uint instanceID: SV_InstanceID)  
            {  
                v2f o;
                const Particle p = Particles[instanceID];
                float size = _Size;
                const float4x4 m = float4x4(
                    size,0,0,p.position.x,
                    0,size,0,p.position.y,
                    0,0,size,p.position.z,
                    1,1,1,1);
                const float4 pos = mul(m, i.vertex);  
                o.vertex = UnityObjectToClipPos(pos);
                o.world_pos = pos;
                o.color = float3(1,1,1);
                return o;  
            }   

            fixed4 frag(v2f i) : SV_Target  
            {
              //  const float3 diffuse_color = applyCustomLighting(i.color, i.world_pos, i.world_normal);
                return fixed4(i.color, 1.0);
            }   
            ENDCG  
        }
    }
}