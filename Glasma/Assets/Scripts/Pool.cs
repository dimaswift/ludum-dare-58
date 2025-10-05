using System.Collections.Generic;
using UnityEngine;


public interface IPoolable
{
    void OnPicked();
    bool InUse { get; }
}

public class Pool<T> where T : Component, IPoolable
{
    private readonly List<IPoolable> objects = new();

    private GameObject prefab;
    private Transform container;

    private IPoolable Create()
    {
        var item = Object.Instantiate(prefab.gameObject, container, false);
        var poolable = item.GetComponent<IPoolable>();
        objects.Add(poolable);
        item.SetActive(false);
        return poolable;
    }
    
    public Pool(int capacity, GameObject prefab)
    {
        this.prefab = prefab;
        container = new GameObject($"{typeof(T).Name}_Pool").transform;
        for (int i = 0; i < capacity; i++)
        {
            Create();
        }
    }

    public T Pick()
    {
        foreach (var item in objects)
        {
            if (!item.InUse)
            {
                item.OnPicked();
                return item as T;
            }
        }

        var newItem = Create();
        newItem.OnPicked();
        return newItem as T;
    }
}
