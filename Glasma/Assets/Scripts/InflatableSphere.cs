using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public class InflatableSphereShared : MonoBehaviour
{
    [Header("Mesh Generation")]
    [SerializeField] private ComputeShader physicsShader;
    [SerializeField, Range(0, 14)] private int subdivisions = 2;
    [SerializeField] private float initialRadius = 1f;
    [SerializeField] private float radius;
    [SerializeField] private float escapeRadius;
    [SerializeField] private float soften;
    [SerializeField] private float density;
    [SerializeField] private float frequency;
    [SerializeField] private float size;
    [SerializeField] private float scale;
    [SerializeField] private float surface;
    [Header("Physics")]
    [SerializeField, Range(0f, 100f)] private float inflationPressure = 10f;
    [SerializeField, Range(0f, 1f)] private float damping = 0.1f;
    [SerializeField] private float targetRadius = 2f;
    [SerializeField, Range(0f, 100f)] private float radialStiffness = 50f;
    [SerializeField] private bool simulatePhysics = true;
    
    [Header("Rendering")]
    [SerializeField] private Material material;
    
    private Mesh mesh;
    private GraphicsBuffer vertexBuffer;
    private GraphicsBuffer indexBuffer;
    private GraphicsBuffer dynamicsBuffer;
    private GraphicsBuffer centroidBuffer;
    
    private int vertexCount;
    private int triangleCount;
    
    private void Start()
    {
        GenerateSharedGeodesicSphere();
        InitializePhysics();
    }
    
    // ========================================================================
    // CPU-side generation with vertex sharing
    // ========================================================================
    
    private void GenerateSharedGeodesicSphere()
    {
        // Dictionary to track unique vertices
        Dictionary<Vector3, int> vertexMap = new Dictionary<Vector3, int>(new Vector3Comparer());
        List<Vector3> positions = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> indices = new List<int>();
        
        // Generate base icosahedron and subdivide
        for (int faceIdx = 0; faceIdx < 20; faceIdx++)
        {
            Vector3 v0 = GetIcoVertex(faceIdx, 0) * initialRadius;
            Vector3 v1 = GetIcoVertex(faceIdx, 1) * initialRadius;
            Vector3 v2 = GetIcoVertex(faceIdx, 2) * initialRadius;
            
            SubdivideTriangleShared(v0, v1, v2, subdivisions, 
                positions, normals, indices, vertexMap);
        }
        
        vertexCount = positions.Count;
        triangleCount = indices.Count / 3;
        
        Debug.Log($"Generated shared mesh: {vertexCount} vertices, {triangleCount} triangles");
        Debug.Log($"Vertex reuse: {(1f - vertexCount / (float)(triangleCount * 3)) * 100f:F1}%");
        
        // Create mesh
        CreateMeshFromData(positions, normals, indices);
    }
    
    private void SubdivideTriangleShared(Vector3 v0, Vector3 v1, Vector3 v2, int depth,
        List<Vector3> positions, List<Vector3> normals, List<int> indices,
        Dictionary<Vector3, int> vertexMap)
    {
        if (depth == 0)
        {
            // Add triangle (vertices may already exist)
            int i0 = GetOrAddVertex(v0, positions, normals, vertexMap);
            int i1 = GetOrAddVertex(v1, positions, normals, vertexMap);
            int i2 = GetOrAddVertex(v2, positions, normals, vertexMap);
            
            indices.Add(i0);
            indices.Add(i1);
            indices.Add(i2);
        }
        else
        {
            // Subdivide recursively
            Vector3 m01 = GetMidpoint(v0, v1);
            Vector3 m12 = GetMidpoint(v1, v2);
            Vector3 m20 = GetMidpoint(v2, v0);
            
            SubdivideTriangleShared(v0, m01, m20, depth - 1, positions, normals, indices, vertexMap);
            SubdivideTriangleShared(v1, m12, m01, depth - 1, positions, normals, indices, vertexMap);
            SubdivideTriangleShared(v2, m20, m12, depth - 1, positions, normals, indices, vertexMap);
            SubdivideTriangleShared(m01, m12, m20, depth - 1, positions, normals, indices, vertexMap);
        }
    }
    
    private int GetOrAddVertex(Vector3 position, List<Vector3> positions, 
        List<Vector3> normals, Dictionary<Vector3, int> vertexMap)
    {
        // Round to avoid floating point issues
        Vector3 rounded = RoundVector(position, 10000f);
        
        if (vertexMap.TryGetValue(rounded, out int index))
        {
            return index;
        }
        
        // Add new vertex
        index = positions.Count;
        positions.Add(position);
        normals.Add(position.normalized); // Normal = radial direction
        vertexMap[rounded] = index;
        return index;
    }
    
    private Vector3 GetMidpoint(Vector3 v0, Vector3 v1)
    {
        Vector3 mid = (v0 + v1) * 0.5f;
        return mid.normalized * initialRadius;
    }
    
    private Vector3 RoundVector(Vector3 v, float precision)
    {
        return new Vector3(
            Mathf.Round(v.x * precision) / precision,
            Mathf.Round(v.y * precision) / precision,
            Mathf.Round(v.z * precision) / precision
        );
    }
    
    private Vector3 GetIcoVertex(int faceIdx, int vertIdx)
    {
        int[,] faces = new int[,] {
            {0, 11, 5}, {0, 5, 1}, {0, 1, 7}, {0, 7, 10}, {0, 10, 11},
            {1, 5, 9}, {5, 11, 4}, {11, 10, 2}, {10, 7, 6}, {7, 1, 8},
            {3, 9, 4}, {3, 4, 2}, {3, 2, 6}, {3, 6, 8}, {3, 8, 9},
            {4, 9, 5}, {2, 4, 11}, {6, 2, 10}, {8, 6, 7}, {9, 8, 1}
        };
        
        float phi = 1.618033988749895f;
        Vector3[] verts = new Vector3[] {
            new Vector3(-1, phi, 0).normalized,
            new Vector3(1, phi, 0).normalized,
            new Vector3(-1, -phi, 0).normalized,
            new Vector3(1, -phi, 0).normalized,
            new Vector3(0, -1, phi).normalized,
            new Vector3(0, 1, phi).normalized,
            new Vector3(0, -1, -phi).normalized,
            new Vector3(0, 1, -phi).normalized,
            new Vector3(phi, 0, -1).normalized,
            new Vector3(phi, 0, 1).normalized,
            new Vector3(-phi, 0, -1).normalized,
            new Vector3(-phi, 0, 1).normalized
        };
        
        return verts[faces[faceIdx, vertIdx]];
    }
    
    private void CreateMeshFromData(List<Vector3> positions, List<Vector3> normals, List<int> indices)
    {
        mesh = new Mesh();
        mesh.name = "SharedGeodesicSphere";
        
        mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        
        var vp = new VertexAttributeDescriptor(
            VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
        var vn = new VertexAttributeDescriptor(
            VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
        
        mesh.SetVertexBufferParams(vertexCount, vp, vn);
        mesh.SetIndexBufferParams(indices.Count, IndexFormat.UInt32);
        
        try
        {
            vertexBuffer = mesh.GetVertexBuffer(0);
            indexBuffer = mesh.GetIndexBuffer();
            
            // Upload vertex data
            var vertexData = new VertexData[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                vertexData[i] = new VertexData {
                    position = positions[i],
                    normal = normals[i]
                };
            }
            vertexBuffer.SetData(vertexData);
            
            // Upload index data
            indexBuffer.SetData(indices.ToArray());
            
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Count),
                MeshUpdateFlags.DontRecalculateBounds);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * targetRadius * 3f);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create mesh: {e.Message}");
            DestroyImmediate(mesh);
            mesh = null;
        }
    }
    
    private void InitializePhysics()
    {
        // Allocate dynamics buffer (velocity + mass per vertex)
        dynamicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 
            vertexCount, sizeof(float) * 4); // float3 velocity + float mass
        
        // Initialize with zero velocity and unit mass
        var dynamics = new VertexDynamicsData[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            dynamics[i] = new VertexDynamicsData {
                velocity = Vector3.zero,
                mass = 1f
            };
        }
        dynamicsBuffer.SetData(dynamics);
        
        // Centroid buffer
        centroidBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(float) * 3);
        centroidBuffer.SetData(new Vector3[] { Vector3.zero });
    }
    
    private void Update()
    {
        if (simulatePhysics && mesh != null)
        {
            SimulatePhysics();
        }
        
        if (mesh != null && material != null)
        {
            Graphics.DrawMesh(mesh, transform.localToWorldMatrix, material, 0);
        }
    }
    
    private void SimulatePhysics()
    {
        physicsShader.SetFloat("Radius", radius);
        physicsShader.SetFloat("EscapeRadius", escapeRadius);
        physicsShader.SetFloat("Soften", soften);
        physicsShader.SetFloat("Density", density);
        physicsShader.SetFloat("Frequency", frequency);
        physicsShader.SetFloat("Size", size);
        physicsShader.SetFloat("Scale", scale);
        physicsShader.SetFloat("Surface", surface);
        // Update centroid
        int centroidKernel = physicsShader.FindKernel("UpdateCentroid");
        physicsShader.SetBuffer(centroidKernel, "VertexBuffer", vertexBuffer);
        physicsShader.SetBuffer(centroidKernel, "Centroid", centroidBuffer);
        physicsShader.SetInt("VertexCount", vertexCount);
        
        int centroidThreads = (vertexCount + 63) / 64;
        physicsShader.Dispatch(centroidKernel, centroidThreads, 1, 1);
        
        // Relax vertices
        int relaxKernel = physicsShader.FindKernel("RelaxVertices");
        physicsShader.SetBuffer(relaxKernel, "VertexBuffer", vertexBuffer);
        physicsShader.SetBuffer(relaxKernel, "DynamicsBuffer", dynamicsBuffer);
        physicsShader.SetBuffer(relaxKernel, "Centroid", centroidBuffer);
        
        physicsShader.SetFloat("DeltaTime", Time.deltaTime);
        physicsShader.SetFloat("InflationPressure", inflationPressure);
        physicsShader.SetFloat("Damping", damping);
        physicsShader.SetFloat("TargetRadius", targetRadius);
        physicsShader.SetFloat("RadialStiffness", radialStiffness);
        physicsShader.SetInt("VertexCount", vertexCount);
        
        int relaxThreads = (vertexCount + 63) / 64;
        physicsShader.Dispatch(relaxKernel, relaxThreads, 1, 1);
        
        // Recalculate normals
        int normalKernel = physicsShader.FindKernel("RecalculateNormals");
        physicsShader.SetBuffer(normalKernel, "VertexBuffer", vertexBuffer);
        physicsShader.SetBuffer(normalKernel, "IndexBuffer", indexBuffer);
        physicsShader.SetInt("TriangleCount", triangleCount);
        
        int normalThreads = (triangleCount + 63) / 64;
        physicsShader.Dispatch(normalKernel, normalThreads, 1, 1);
    }
    
    private void OnDestroy()
    {
        vertexBuffer?.Release();
        indexBuffer?.Release();
        dynamicsBuffer?.Release();
        centroidBuffer?.Release();
        
        if (mesh != null)
            DestroyImmediate(mesh);
    }
    
    // Helper structs for data upload
    struct VertexData
    {
        public Vector3 position;
        public Vector3 normal;
    }
    
    struct VertexDynamicsData
    {
        public Vector3 velocity;
        public float mass;
    }
    
    // Custom comparer for Vector3 dictionary
    class Vector3Comparer : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b)
        {
            return a == b;
        }
        
        public int GetHashCode(Vector3 v)
        {
            return v.GetHashCode();
        }
    }
}