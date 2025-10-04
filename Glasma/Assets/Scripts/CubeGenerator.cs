using UnityEngine;
using UnityEngine.Rendering;

public class CubeGenerator : MonoBehaviour
{
    [SerializeField] private ComputeShader cubeShader;
    [SerializeField] private float size = 1f;
    [SerializeField] private Material material;
    
    private Mesh mesh;
    private GraphicsBuffer vertexBuffer;
    private GraphicsBuffer indexBuffer;
    
    private void Start()
    {
        GenerateCube();
    }
    
    private void GenerateCube()
    {
        // Cube: 24 vertices, 36 indices (12 triangles)
        int vertexCount = 24;
        int indexCount = 36;
        
        AllocateMesh(vertexCount, indexCount);
        
        if (mesh == null)
        {
            Debug.LogError("Failed to allocate mesh!");
            return;
        }
        
        Debug.Log($"Vertex buffer: {vertexBuffer.count} stride: {vertexBuffer.stride}");
        Debug.Log($"Index buffer: {indexBuffer.count} stride: {indexBuffer.stride}");
        
        // Set shader parameters
        int kernel = cubeShader.FindKernel("GenerateCube");
        cubeShader.SetBuffer(kernel, "VertexBuffer", vertexBuffer);
        cubeShader.SetBuffer(kernel, "IndexBuffer", indexBuffer);
        cubeShader.SetFloat("Size", size);
        
        // Dispatch - only need 1 thread group for 24 vertices
        cubeShader.Dispatch(kernel, 1, 1, 1);
        
        // Update mesh bounds
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * size * 2.5f);
        
        Debug.Log("Cube generated!");
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
        mesh.name = "DebugCube";
        
        // Enable raw buffer access
        mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        
        // Define vertex layout: Position (float3) + Normal (float3)
        var vp = new VertexAttributeDescriptor(
            VertexAttribute.Position, 
            VertexAttributeFormat.Float32, 
            3);
        
        var vn = new VertexAttributeDescriptor(
            VertexAttribute.Normal, 
            VertexAttributeFormat.Float32, 
            3);
        
        // Set vertex buffer params
        mesh.SetVertexBufferParams(vertexCount, vp, vn);
        
        // Set index buffer params
        mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
        
        // Set submesh
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount), 
            MeshUpdateFlags.DontRecalculateBounds | 
            MeshUpdateFlags.DontValidateIndices | 
            MeshUpdateFlags.DontNotifyMeshUsers);
        
        try
        {
            vertexBuffer = mesh.GetVertexBuffer(0);
            indexBuffer = mesh.GetIndexBuffer();
            
            Debug.Log($"Successfully allocated buffers - Vertices: {vertexCount}, Indices: {indexCount}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to get buffers: {e.Message}");
            DestroyImmediate(mesh);
            mesh = null;
        }
    }
    
    private void Update()
    {
        if (mesh != null && material != null)
        {
            Graphics.DrawMesh(mesh, transform.localToWorldMatrix, material, 0);
        }
    }
    
    private void OnDestroy()
    {
        vertexBuffer?.Release();
        indexBuffer?.Release();
        if (mesh != null)
            DestroyImmediate(mesh);
    }
    
    private void OnDrawGizmos()
    {
        // Draw mesh bounds
        if (mesh != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + mesh.bounds.center, mesh.bounds.size);
        }
    }
}