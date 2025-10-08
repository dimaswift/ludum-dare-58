using System;
using System.Collections.Generic;
using UnityEngine;

public class QuantaManager : MonoBehaviour
{
    public int MaxTier { get; private set; }
    private static QuantaManager instance;
    
    public static QuantaManager Instance
    {
        get
        {
            if (!instance) instance = FindFirstObjectByType<QuantaManager>();
            return instance;
        }
    }
    
    private readonly Dictionary<int,Pool<Quanta>> pools = new();
    
    void Start()
    {
        var volumes = Resources.LoadAll<QuantaConfig>("Quanta");
    
        for (int i = 0; i < volumes.Length; i++)
        {
            var cfg = volumes[i];
            MaxTier = Mathf.Max(cfg.gen, MaxTier);
            if(!pools.TryAdd(cfg.gen, new Pool<Quanta>(cfg.poolCapacity, cfg.prefab.gameObject)))
            {
                Debug.LogError($"Volume tier {cfg.gen} already taken");
            }
        }
    }


    private async void SaveAllQuanta()
    {
        int progress = 0;
        var targets = FindObjectsByType<Quanta>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var q in targets)
        {
            try
            {
                await STLExporter.ExportMeshToSTLAsync(
                    name: q.GetInstanceID().ToString(),
                    mesh: q.Mesh,
                    binary: true,
                    optimizeVertices: true
                );
                Debug.Log($"Saved {++progress}/{targets.Length}");
             
            }
            catch (Exception ex)
            {
                Debug.LogError($"Export failed: {ex.Message}");
            }
        }
       
    }
    
    public Quanta Spawn(int tier)
    {
        if (pools.TryGetValue(tier, out var v))
        {
            return v.Pick();
        }
        Debug.LogWarning($"Volume tier {tier} doesn't exist");
        return null;
    }
    
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.S))
        {
            SaveAllQuanta();
        }
        
        if(!Input.GetKey(KeyCode.Space)) return;
        if (Input.GetMouseButtonDown(0))
        {
            var ground = Mouse.CastGround();
            var vol = Spawn(1);
            vol.transform.position = ground + new Vector3(0,1,0);
            vol.CollapseWave();
        }
        
        if (Input.GetMouseButtonDown(1))
        {

            if (Mouse.IsHitting<Quanta>(out var volume, out var hit))
            {
                volume.Decay();
            } 
            
            
        }
    }
}
