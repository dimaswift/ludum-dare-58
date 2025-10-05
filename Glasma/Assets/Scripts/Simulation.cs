using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class Simulation : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private Material mat;
    [SerializeField] private Mesh mesh;
    
    [Header("Particle Configuration")]
    [SerializeField] private int particleCount = 1024;
    [Range(0f, 0.1f)] [SerializeField] private float particleSize = 0.1f;
    [SerializeField] private int fieldDepth = 100;
    [SerializeField] private float deltaTime = 1;
    [SerializeField] private float soften = 1;

    
    [Header("Field Configuration")]
    [SerializeField] private float fieldRadius = 1.0f;
    [SerializeField] private float fieldDensity = 1.0f;
    [SerializeField] private float fieldHorizon = 10.0f;
    [SerializeField] private float fieldPhase = 0.0f;
    [SerializeField] private float fieldFrequency = 1.0f;
    [SerializeField] private float speed = 1.0f;
    [SerializeField] private float radius = 1.0f;
    [SerializeField] private float force = 1.0f;
    
    [Header("Initial Conditions")]
    [SerializeField] private Vector3 spawnCenter = Vector3.zero;
    [SerializeField] private float spawnRadius = 5.0f;
    [SerializeField] private Vector3 corePosition = Vector3.zero;
    
    [Header("Colors (RGB for each quark)")]
    [SerializeField] private Color[] quarkColors = new Color[4];
    
    private ComputeBuffer particleBuffer,meshBuffer;
    private int kernelHandle;
    private const int THREAD_GROUP_SIZE = 64;

    private Particle[] particles;
    // Particle structure matching HLSL

    private void Start()
    {
        InitializeColors();
        InitializeComputeShader();
        particles = CreateRandomSphere();
        InitializeParticles(particles);
        
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)particleCount;
        meshBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        meshBuffer.SetData(args);
       
        
    }

    private Particle[] CreateRandomSphere()
    {
        Particle[] arr = new Particle[particleCount];
        
        for (int i = 0; i < particleCount; i++)
        {
            // Random position in spawn sphere
            Vector3 randomPos = spawnCenter + Random.insideUnitSphere * spawnRadius;
            
            arr[i] = new Particle
            {
                spin = float3x3.identity,
                position = randomPos,
                velocity = Vector3.zero,
                core = corePosition,
                radius = fieldRadius,
                density = fieldDensity,
                horizon = fieldHorizon,
                phase = fieldPhase,
                frequency = fieldFrequency,
                depth = fieldDepth,
                size = particleSize,
                color0 = new Vector3(quarkColors[0].r, quarkColors[0].g, quarkColors[0].b),
                color1 = new Vector3(quarkColors[1].r, quarkColors[1].g, quarkColors[1].b),
                color2 = new Vector3(quarkColors[2].r, quarkColors[2].g, quarkColors[2].b),
                color3 = new Vector3(quarkColors[3].r, quarkColors[3].g, quarkColors[3].b),
                deltaTime = deltaTime,
                soften = soften
            };
        }

        return arr;
    }
    
    public void Draw()
    {
        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat,
            new Bounds(transform.position, Vector3.one * (float.MaxValue)), meshBuffer);
    }

    
    
    private void InitializeColors()
    {
        if (quarkColors.Length < 4)
        {
            quarkColors = new Color[4];
        }
        
        if (quarkColors[0] == Color.clear) quarkColors[0] = Color.red;
        if (quarkColors[1] == Color.clear) quarkColors[1] = Color.green;
        if (quarkColors[2] == Color.clear) quarkColors[2] = Color.blue;
        if (quarkColors[3] == Color.clear) quarkColors[3] = Color.yellow;
    }
    
    private void InitializeComputeShader()
    {
        if (computeShader == null)
        {
            Debug.LogError("Compute Shader not assigned!");
            enabled = false;
            return;
        }
        
        kernelHandle = computeShader.FindKernel("Run");
    }
    
    private void InitializeParticles(Particle[] list)
    {
        if (particleBuffer == null || particleBuffer.count != list.Length)
        {
            particleBuffer?.Release();
            particleBuffer = new ComputeBuffer(list.Length, Marshal.SizeOf<Particle>());
            computeShader.SetBuffer(kernelHandle, "Particles", particleBuffer);
            mat.SetBuffer("Particles", particleBuffer);
        }
        particleBuffer.SetData(list);
    }

    private void UpdateParticles()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].position = spawnCenter + Random.insideUnitSphere * spawnRadius;;
            particles[i].core = corePosition;
            particles[i].radius = fieldRadius;
            particles[i].density = fieldDensity;
            particles[i].horizon = fieldHorizon;
            particles[i].phase = fieldPhase;
            particles[i].frequency = fieldFrequency;
            particles[i].depth = fieldDepth;
            particles[i].size = particleSize;
            particles[i].color0 = new Vector3(quarkColors[0].r, quarkColors[0].g, quarkColors[0].b);
            particles[i].color1 = new Vector3(quarkColors[1].r, quarkColors[1].g, quarkColors[1].b);
            particles[i].color2 = new Vector3(quarkColors[2].r, quarkColors[2].g, quarkColors[2].b);
            particles[i].color3 = new Vector3(quarkColors[3].r, quarkColors[3].g, quarkColors[3].b);
            particles[i].soften = soften;
            particles[i].deltaTime = deltaTime;

        }
    }
    
    private void Update()
    {
        if (particleBuffer == null) return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            UpdateParticles();
            InitializeParticles(particles);
        }
        computeShader.SetFloat("Force", force);
        computeShader.SetFloat("DeltaTime", speed);
        computeShader.SetFloat("Radius", radius);
        int threadGroups = Mathf.CeilToInt(particleCount / (float)THREAD_GROUP_SIZE);
        computeShader.Dispatch(kernelHandle, threadGroups, 1, 1);

        Draw();
    }
    
    private void OnDestroy()
    {
        ReleaseBuffers();
    }
    
    private void OnDisable()
    {
        ReleaseBuffers();
    }
    
    private void ReleaseBuffers()
    {
        particleBuffer?.Release();
        meshBuffer?.Release();
    }
}       
