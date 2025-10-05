using UnityEngine;

public static class Mouse
{
    private static Camera cam;

    private static readonly RaycastHit[] hitBuff = new RaycastHit[8];
    
    public static Vector3 CastGround(float height = 0f)
    {
        if (!cam) cam = Camera.main;
        if (cam != null)
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (new Plane(Vector3.up,new Vector3(0, height, 0)).Raycast(ray, out var d))
            {
                return ray.GetPoint(d);
            }
        }
        return Vector3.zero;
    }
    
    public static bool IsHitting<T>(out T component) where T: Component
    {
        if (!cam) cam = Camera.main;
        if (cam != null)
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var hitCount = Physics.RaycastNonAlloc(ray, hitBuff, float.MaxValue);
            float closets = float.MaxValue;
            RaycastHit? res = null;
            for (int i = 0; i < hitCount; i++)
            {
                var hit = hitBuff[i];
                if (hit.distance < closets)
                {
                    closets = hit.distance;
                    res = hit;
                }
            }
            component = res?.transform?.GetComponent<T>();
            return component != null;
        }

        component = null;
        return false;
    }
}