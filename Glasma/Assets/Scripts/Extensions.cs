using UnityEngine;

namespace DefaultNamespace
{
    public static class Extensions
    {
        public static void DispatchThreads
            (this ComputeShader compute, int kernel, int x, int y, int z)
        {
            uint xc, yc, zc;
            compute.GetKernelThreadGroupSizes(kernel, out xc, out yc, out zc);

            x = (x + (int)xc - 1) / (int)xc;
            y = (y + (int)yc - 1) / (int)yc;
            z = (z + (int)zc - 1) / (int)zc;

            compute.Dispatch(kernel, x, y, z);
        }
    }
}