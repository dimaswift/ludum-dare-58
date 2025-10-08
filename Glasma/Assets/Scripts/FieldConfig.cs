using System;
using System.Runtime.InteropServices;
using UnityEngine;


[CreateAssetMenu(menuName = "Glasma/Field")]
public class FieldConfig : ScriptableObject
{
    public Photon photon;
    public Vector3 offset;
    public int steps = 1;
    public float radius;
    public float escapeRadius;
    public float soften;
    public float timeStep;
    public float density;
    public float frequency;
    public float size;
    public Vector3 scale;
    public float surface;
  
}


[StructLayout(LayoutKind.Sequential)]  
[System.Serializable]
public struct Photon
{
    public int frequency;
    public float amplitude;
    public float phase;
    public float radius;
    public float density;
    public float scale;

    public override bool Equals(object obj)
    {
        return base.Equals(obj);
    }

    public bool Equals(Photon other)
    {
        return frequency == other.frequency 
               && amplitude.Equals(other.amplitude) 
               && phase.Equals(other.phase) 
               && radius.Equals(other.radius) 
               && density.Equals(other.density)
               && scale.Equals(other.scale);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(frequency);
        hashCode.Add(amplitude);
        hashCode.Add(phase);
        hashCode.Add(radius);
        hashCode.Add(density);
        hashCode.Add(scale);
        return hashCode.ToHashCode();
    }
}


