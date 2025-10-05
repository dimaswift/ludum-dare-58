using UnityEngine;

[CreateAssetMenu(menuName = "Glasma/Volume")]
public class QuantaConfig : ScriptableObject
{
    public Quanta prefab;
    public int poolCapacity;
    public int tier;
    public Vector3Int resolution = new (64,64,64);
    public Vector3Int holeResolution = new (16,16,16);
    public int maxTriangleBudget; 
}