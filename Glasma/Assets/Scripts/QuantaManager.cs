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
            MaxTier = Mathf.Max(cfg.tier, MaxTier);
            if(!pools.TryAdd(cfg.tier, new Pool<Quanta>(cfg.poolCapacity, cfg.prefab.gameObject)))
            {
                Debug.LogError($"Volume tier {cfg.tier} already taken");
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
