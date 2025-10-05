using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class Quanta : MonoBehaviour, IPoolable
{
    public bool InUse => gameObject.activeSelf;
    
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
    
    private Matrix4x4 gene;

    [SerializeField] private List<Matrix4x4> dnaSequence = new(); 
    
    private void Awake()
    {
        Init();
        CollapseWave();
    }
    
    public void CopyDNA(List<Matrix4x4> sequence)
    {
        dnaSequence.AddRange(sequence);
    }
    
    public void Decay()
    {
        gameObject.SetActive(false);

        if (config.tier >= QuantaManager.Instance.MaxTier)
        {
            return;
        }
        
        var pos = transform.position;
        var rot = transform.rotation;

        var shrinkage = 2.0f / Mathf.Pow(2, config.tier + 1);
        
        void Create(Vector3Int side)
        {
            var part = QuantaManager.Instance.Spawn(config.tier + 1);
            if (part == null) return;
            Vector3 offset = new Vector3(side.x, side.y, side.z) * shrinkage * 0.5f;
            part.transform.rotation = rot;
            var dir = part.transform.TransformDirection(offset);
            part.transform.position = pos + dir;
            Matrix4x4 dnaToKeep = new Matrix4x4();
            dnaToKeep.SetTRS(flesh.position,flesh.rotation,Vector3.one);
            dnaSequence.Add(dnaToKeep);
            part.CopyDNA(dnaSequence);
            
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
        if (config.tier == 1)
        {
            body.mass = density;
            return;
        }
        var size = Vector3.one * (2.0f / Mathf.Pow(2, config.tier));
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
        dnaSequence.Clear();
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
        
        Matrix4x4 worldToFractal = gene.inverse;
        //TODO: doesn't work right (yet)
        foreach (var pair in dnaSequence)
        {
            worldToFractal *= pair;
        }
        gene = flesh.localToWorldMatrix;
        var photonM = new Matrix4x4();
        
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
            field.photon.GetHashCode(),
            solidHash),
            worldToFractal.GetHashCode(),
            gene
            );
        
        if (newHash == hash)
        {
            return;
        }
        
        photonBuffer.SetData(new List<Photon>() {field.photon});
        hash = newHash;
        compute.SetMatrix("PhotonTranform", photonM);
        compute.SetMatrix("WorldToFractalMatrix", worldToFractal);
        
        // You also need to tell the shader where this cube currently IS in the world
        compute.SetMatrix("LocalToWorldMatrix", gene);

        compute.SetInt("Steps", field.steps);
        compute.SetFloat("Radius", field.radius);
        compute.SetFloat("EscapeRadius", field.escapeRadius);
        compute.SetFloat("Soften", field.soften);
        compute.SetFloat("Density", field.density);
        compute.SetFloat("Frequency", field.frequency);
        compute.SetFloat("Size", field.size);
        compute.SetFloat("Scale", field.scale);
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
