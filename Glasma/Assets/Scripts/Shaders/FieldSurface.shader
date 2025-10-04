Shader "Glasma/FieldSurface"  
{  
    Properties  
    {  
        _Steps("Steps", Range(1.0, 128.0)) = 16.0
        _EscapeRadius("Escape Radius", Float) = 1.0
        _Speed("Speed", Float) = 0.000
        _CorePosition ("Core Position", Vector) = (0,0,0)
        _Offset ("Offset", Vector) = (0,0,0)
        _Density("Density", Float) = 0
        _Radius("Radius", Float) = 1.0
        _Scale("Scale", Float) = 1.0
        _Frequency("Frequency", Float) = 6.28318530718
         _DeltaTime("Delta Time", Float) = 0.5
         _Soften("Soften", Float) = 0.5
        _UpFirst("Up First", Vector) = (0,0,0)
        _UpSecond("Up Second", Vector) = (0,0,0)
        _Down("Down", Vector) = (0,0,0)
        _Higgs("Higgs", Vector) = (0,0,0)

        _ColorFrequency("Color Frequency", Range(0.0, 1.0)) = 0.01
        _ColorPhase("Color Phase", Range(0, 6.28)) = 0.0
        _ColorAmplitude("Color Amplitude", Float) = 0.5
         
    }  
    SubShader  
    {  
        Tags { "RenderType" = "Opaque" }  
        Cull Off
        Pass  
        {  
            CGPROGRAM  
            #pragma vertex vert  
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Assets/Scripts/Compute/QuantumField.cginc"
     
            struct appdata_t  
            {  
                float4 vertex : POSITION;
                float3 normal : NORMAL;  
                float4 color : COLOR;
            };  

            struct v2f  
            {  
                float4 vertex : SV_POSITION;  
                float3 world_normal : TEXCOORD0;
                float3 world_pos : TEXCOORD1; 
            };
            
            
            float3 _UpFirst;
            float3 _UpSecond;
            float3 _Down;
            float3 _Higgs;
            
            float3 _CorePosition;
            
            float _Frequency;
     
            int _Steps;
            float _Scale;
            float _Speed;
            float _EscapeRadius;
            float _ColorFrequency;
            float _ColorPhase;
            float _ColorAmplitude;
    
            float _Density;
            float3 _Offset;
            float _Radius;
            float _DeltaTime;
            float _Soften;
            
            v2f vert(const appdata_t input)  
            {  
                v2f o;
                o.world_normal = UnityObjectToWorldNormal(input.normal);
                o.world_pos = mul(unity_ObjectToWorld, input.vertex);
                o.vertex = UnityObjectToClipPos(input.vertex); 
                return o;   
            }
            
            fixed4 frag(const v2f input) : SV_Target  
            {
                FieldConfig cfg;
              
               // cfg.colors[0] = _UpFirst;
              //  cfg.colors[1] = _UpSecond;
              //  cfg.colors[2] = _Down;
             //   cfg.colors[3] = _Higgs;
                cfg.density = _Density;
                cfg.frequency = _Frequency;
                cfg.radius = _Radius;
                cfg.dt = _DeltaTime;
                cfg.phase = _Time * _Speed;
                cfg.soften = _Soften;
                
                float3 position = (input.world_pos + _Offset) * exp2(_Scale);
               
                Trajectory value = SampleFieldN(_Steps, position, _CorePosition, _EscapeRadius, cfg);
                
                float v = value.energy / _ColorAmplitude;
                
                float3 base_color = float3(v,v,v);
                
                return float4(base_color, 1);  
            }  
            
            ENDCG   
        }  
    }  
}
