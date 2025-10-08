using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class Quanta : MonoBehaviour, IPoolable
{
    public bool InUse => gameObject.activeSelf;
    public Mesh Mesh => meshFilter.sharedMesh;

    [SerializeField] private bool update;
    [SerializeField] private FieldConfig field;
    [SerializeField] private Transform flesh;
    [SerializeField] private QuantaConfig config;
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private float density = 1f;
    [SerializeField] private ComputeShader compute;
  
    private MarchingCubes marchingCubes;

    private readonly List<SolidContainer> solidContainers = new();
    
    private readonly List<SculptSolid> solids = new();

    private ComputeBuffer photonBuffer;

    private int hash;

    private BoxCollider box;

    private Rigidbody body;

    private Matrix4x4 originalFractalPos;

    private Vector3 birthPlace;
    
    [SerializeField]  private Vector3 zoom;
    [SerializeField]  private Vector3 fractalRotation;
    [SerializeField]  private Vector3 fractalPosition;
    
    
    
    private Vector3 fractalScale = Vector3.one;
    
    
    private void Awake()
    {
        fractalPosition = transform.position;
        fractalRotation = transform.eulerAngles;
        Init();
        CollapseWave();
        transform.position += new Vector3(0, 1, 0);
    }
    
    
    public void Decay()
    {
        gameObject.SetActive(false);

        if (config.gen >= QuantaManager.Instance.MaxTier)
        {
            return;
        }
        
        var pos = transform.position;
        var rot = transform.rotation;

        var contaction = (1.0f / Mathf.Pow(2, config.gen + 1));
       
        void Create(Vector3Int side)
        {
            var part = QuantaManager.Instance.Spawn(config.gen + 1);
           
            if (part == null) return;
            
            Vector3 offset = new Vector3(side.x, side.y, side.z) * contaction;
    
            part.transform.rotation = rot;
            var dir = part.transform.TransformDirection(offset);
            part.transform.position = pos + dir;
            part.originalFractalPos = originalFractalPos;
            part.fractalScale = fractalScale * 2;
            part.fractalRotation = fractalRotation;
            part.fractalPosition = (part.originalFractalPos * (part.transform.position)) * -part.fractalScale.x;
            part.zoom = zoom;
            part.birthPlace = part.transform.position;
            part.CollapseWave();
          //  part.Nudge(dir.normalized);
        }
        
        //SU(3)
        Create(new Vector3Int(-1, -1, -1));
        Create(new Vector3Int(1, 1, 1));
        Create(new Vector3Int(-1, -1, 1));
        Create(new Vector3Int(-1, 1, 1));
        Create(new Vector3Int(1, -1, -1));
        Create(new Vector3Int(1, 1, -1));
        Create(new Vector3Int(-1, 1, -1));
        Create(new Vector3Int(1, -1, 1));
    }
    
    public void Nudge(Vector3 direction)
    {
        body.linearVelocity += direction * Random.Range(1f,3f);
        body.angularVelocity = Random.insideUnitSphere * Random.Range(1f,3f);;
    }

    public void Resize()
    {
        if (config.gen == 1)
        {
            body.mass = density;
            return;
        }
        var size = Vector3.one * (2.0f / Mathf.Pow(2, config.gen));
        box.size = size;
        flesh.localScale = size;
        meshFilter.transform.localScale = size;
        body.mass = (size.x * size.y * size.z) * density;
    }

    public void Punch(Vector3 point)
    {
        var closest = box.ClosestPoint(point);
        var local = (transform.InverseTransformPoint(closest) + new Vector3(0.5f, 0.5f,0.5f));
        
        marchingCubes.AddHole(new Vector3Int(
            1,
            1,
            1), 14);

        CollapseWave();
    }
    
    public void Init()
    {
        originalFractalPos = transform.worldToLocalMatrix.inverse;
        fractalPosition = (originalFractalPos * transform.position) * -fractalScale.x;
        birthPlace = transform.position;
        body = GetComponent<Rigidbody>();
        box = GetComponent<BoxCollider>();
        marchingCubes = new MarchingCubes(compute, config.maxTriangleBudget, config.holeResolution);
        photonBuffer = new ComputeBuffer(1, Marshal.SizeOf<Photon>());
        compute.SetBuffer(0, "Photons", photonBuffer);
        meshFilter.mesh = marchingCubes.Mesh;
        Resize();
    }
    
    public void OnPicked()
    {
      //  dnaSequence.Clear();
        gameObject.SetActive(true);
    }

    public void CollapseWave()
    {
        solids.Clear();
        GetComponentsInChildren(solidContainers);
        int solidHash = 0;
        foreach (var container in solidContainers)
        {
            var s = container.GetSolid();
            solidHash += s.GetHashCode();
            solids.Add(s);
        }
      
        var photonM = new Matrix4x4();
        
        Matrix4x4 fractalMatrix = new Matrix4x4();

        //fractalPosition = flesh.position;

        fractalMatrix.SetTRS(fractalPosition, Quaternion.Euler(fractalRotation), new Vector3(fractalScale.x * zoom.x, fractalScale.y * zoom.y, fractalScale.z * zoom.z));

        var newHash = HashCode.Combine(HashCode.Combine(HashCode.Combine(field.frequency,
            field.steps,
            field.density,
            field.soften,
            field.size,
            field.radius,
            field.escapeRadius,
            field.scale), 
            field.timeStep,
            config.resolution,
            field.surface,
            field.offset,
            field.photon,
            solidHash),
            fractalMatrix
            );
        
        if (newHash == hash)
        {
            return;
        }

        compute.SetBuffer(0, "Photons", photonBuffer);
        photonBuffer.SetData(new List<Photon>() {field.photon});
        hash = newHash;
        compute.SetMatrix("PhotonTranform", photonM);
        compute.SetMatrix("ParentMatrix", fractalMatrix.inverse);
        
        // You also need to tell the shader where this cube currently IS in the world
  
        compute.SetInt("Steps", field.steps);
        compute.SetFloat("Radius", field.radius);
        compute.SetFloat("EscapeRadius", field.escapeRadius);
        compute.SetFloat("Soften", field.soften);
        compute.SetFloat("Density", field.density);
        compute.SetFloat("Frequency", field.frequency);
        compute.SetFloat("Size", field.size);
        compute.SetVector("Scale", field.scale);
        compute.SetFloat("Surface", field.surface);
        compute.SetFloat("TimeStep", field.timeStep);
        compute.SetVector("Offset",field.offset);
        marchingCubes.SetSculptSolids(solids); 
        marchingCubes.Run(config.resolution);

    }

    private void Update()
    {
        if (!update)
        {
            return;
        }
        CollapseWave();
    }

    private void OnDestroy()
    {
        marchingCubes?.Dispose();
        photonBuffer?.Release();
    }
}
