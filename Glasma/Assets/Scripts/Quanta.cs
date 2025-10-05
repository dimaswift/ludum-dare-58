using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Serialization;

public class Quanta : MonoBehaviour, IPoolable
{
    public bool InUse => gameObject.activeSelf;
    
    [SerializeField] private bool update;
    [SerializeField] private FieldConfig field;
    [SerializeField] private Transform flesh;
    [SerializeField] private QuantaConfig config;
    [SerializeField] private MeshFilter meshFilter;
    
    [SerializeField] private ComputeShader compute;
  
    private MarchingCubes marchingCubes;

    private readonly List<SolidContainer> solidContainers = new();
    
    private readonly List<SculptSolid> solids = new();

    private ComputeBuffer photonBuffer;

    private int hash;

    private BoxCollider box;

    
    
    private void Awake()
    {
        Init();
        CollapseWave();
    }
    

    public void Decay()
    {
        gameObject.SetActive(false);

        if (config.tier >= QuantaManager.Instance.MaxTier)
        {
            return;
        }
        
        var pos = transform.position;
       
        var shrinkage = 1.0f;
        
        for (int i = 0; i < config.tier; i++)
        {
            shrinkage /= 2.0f;
        }
        
        void Create(Vector3Int side)
        {
            var part = QuantaManager.Instance.Spawn(config.tier + 1);
            if (part == null) return;
            part.SetSize(Vector3.one * shrinkage);
            part.transform.position = pos + new Vector3(side.x, side.y, side.z) * shrinkage * 0.5f;
            part.CollapseWave();
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

    public void SetSize(Vector3 size)
    {
        box.size = size;
        flesh.localScale = size;
        meshFilter.transform.localScale = size;
    }
    
    public void Init()
    {
        box = GetComponent<BoxCollider>();
        marchingCubes = new MarchingCubes(compute, config.resolution, config.maxTriangleBudget);
        photonBuffer = new ComputeBuffer(1, Marshal.SizeOf<Photon>());
        compute.SetBuffer(0, "Photons", photonBuffer);
        meshFilter.mesh = marchingCubes.Mesh;
    }

    public void OnPicked()
    {
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
        
        var m = new Matrix4x4();
        
        m.SetTRS(flesh.position, flesh.rotation, flesh.localScale);
        
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
            m.GetHashCode()
            );
        
        if (newHash == hash)
        {
            return;
        }
        photonBuffer.SetData(new List<Photon>() {field.photon});
        hash = newHash;
        compute.SetMatrix("PhotonTranform", photonM);
    
        compute.SetMatrix("Transform", m);
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
