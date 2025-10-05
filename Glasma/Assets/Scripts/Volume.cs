using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Uriel.Behaviours;

namespace DefaultNamespace
{
    public class Volume : MonoBehaviour
    {
        [SerializeField] private bool update;
      
        [SerializeField] private FieldConfig field;
        [SerializeField] private Transform flesh;
        
        [SerializeField] private MeshFilter meshFilter;
      
        [SerializeField] private MarchingCubesConfig config;
      
        [SerializeField] private ComputeShader compute;
      
        private MarchingCubes marchingCubes;

        private readonly List<SolidContainer> solidContainers = new();
        
        private readonly List<SculptSolid> solids = new();

        private ComputeBuffer photonBuffer;

        private int hash;
        
        private void Awake()
        {
            marchingCubes = new MarchingCubes(config.budget, compute);
            photonBuffer = new ComputeBuffer(1, Marshal.SizeOf<Photon>());
            compute.SetBuffer(0, "Photons", photonBuffer);
            meshFilter.mesh = marchingCubes.Mesh;
            Construct();
        }

        private void Construct()
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
          //  photonM.SetTRS(globalSolid.transform.position, globalSolid.transform.rotation, globalSolid.transform.localScale);
            
          //  var gs = globalSolid.GetSolid();
         //   solids.Add(gs);
            
            var newHash = HashCode.Combine(HashCode.Combine(HashCode.Combine(field.frequency,
                field.steps,
                field.density,
                field.soften,
                field.size,
                field.radius,
                field.escapeRadius,
                field.scale), 
                field.timeStep,
                field.resolution,
                field.surface,
                config.budget,
                field.offset,
                field.photon.GetHashCode(),
             //   gs.GetHashCode(),
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
            marchingCubes.Run(config, field.resolution);
        }

        private void Update()
        {
            if (!update)
            {
                return;
            }
            Construct();
        }

        private void OnDestroy()
        {
            marchingCubes?.Dispose();
            photonBuffer?.Release();
        }
    }
}