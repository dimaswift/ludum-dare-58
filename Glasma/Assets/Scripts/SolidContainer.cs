using UnityEngine;


public class SolidContainer : MonoBehaviour
{
    [SerializeField] private SculptSolid solid;
    
    public SculptSolid GetSolid()
    {
        var m = new Matrix4x4();
        m.SetTRS(transform.localPosition, transform.localRotation, transform.localScale);
        solid.invTransform = m.inverse;
        return solid;
    } 
}
