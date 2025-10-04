using UnityEngine;
using UnityEngine.Rendering;

public class GeodesicGenerator : MonoBehaviour
{
    [SerializeField] private ComputeShader geodesicShader;
    [SerializeField, Range(0, 8)] private int subdivisions = 3;
    [SerializeField] private float radius = 1f;
    [SerializeField] private Material material;
    
    private Mesh mesh;
    private GraphicsBuffer vertexBuffer;
    private GraphicsBuffer indexBuffer;
    
    private void Start()
    {
        GenerateGeodesicSphere();
    }
    
    private void GenerateGeodesicSphere()
    {
        // Calculate counts: 4^subdivisions triangles per face
        int trianglesPerFace = 1 << (2 * subdivisions);
        int triangleCount = 20 * trianglesPerFace;
        int vertexCount = triangleCount * 3;  // 3 vertices per triangle
        int indexCount = triangleCount * 3;
    
        Debug.Log($"Subdivisions: {subdivisions}, Triangles: {triangleCount}, Vertices: {vertexCount}");
    
        AllocateMesh(vertexCount, indexCount);
    
        if (mesh == null) return;
    
        int kernel = geodesicShader.FindKernel("GenerateGeodesic");
        geodesicShader.SetBuffer(kernel, "VertexBuffer", vertexBuffer);
        geodesicShader.SetBuffer(kernel, "IndexBuffer", indexBuffer);
        geodesicShader.SetInt("Subdivisions", subdivisions);
        geodesicShader.SetFloat("Radius", radius);
    
        int threadGroups = (triangleCount + 63) / 64;
        geodesicShader.Dispatch(kernel, threadGroups, 1, 1);
    
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2.1f);
    }
    
    private void AllocateMesh(int vertexCount, int indexCount)
    {
        if (mesh != null)
        {
            vertexBuffer?.Release();
            indexBuffer?.Release();
            DestroyImmediate(mesh);
        }
        
        mesh = new Mesh();
        mesh.name = $"Geodesic_{subdivisions}";
        
        mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        
        var vp = new VertexAttributeDescriptor
            (VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
        
        var vn = new VertexAttributeDescriptor
            (VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
        
        mesh.SetVertexBufferParams(vertexCount, vp, vn);
        mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
        
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount),
            MeshUpdateFlags.DontRecalculateBounds);
        
        try
        {
            vertexBuffer = mesh.GetVertexBuffer(0);
            indexBuffer = mesh.GetIndexBuffer();
        }
        catch
        {
            DestroyImmediate(mesh);
            mesh = null;
        }
    }
    
    private void Update()
    {
        if (mesh != null)
        {
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0);
        }
    }
    
    private void OnDestroy()
    {
        vertexBuffer?.Release();
        indexBuffer?.Release();
        if (mesh != null)
            DestroyImmediate(mesh);
    }
}