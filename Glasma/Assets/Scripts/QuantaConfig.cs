using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Glasma/Volume")]
public class QuantaConfig : ScriptableObject
{
    public Quanta prefab;
    public int poolCapacity;
    [FormerlySerializedAs("tier")] public int gen;
    public Vector3Int resolution = new (64,64,64);
    public Vector3Int holeResolution = new (16,16,16);
    public int maxTriangleBudget; 
}