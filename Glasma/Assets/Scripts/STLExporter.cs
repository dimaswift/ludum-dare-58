using System.Globalization;
using UnityEngine.Rendering;



using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;

public static class STLExporter
{
    private const string SAVE_DIR = "Export/STL";
    // Event for progress reporting
    public static event Action<float> OnExportProgress;
    public static event Action<string, int, int> OnExportCompleted; // filePath, originalTriangles, finalTriangles
    
    private static readonly Vector3 UNUSED_VERTEX_MARKER = new Vector3(999f, 999f, 999f);
    private const float VERTEX_TOLERANCE = 0.0001f;
    
    public static async Task ExportMeshToSTLAsync(Mesh mesh, string name, bool binary = true, bool optimizeVertices = true)
    {
        if (mesh == null)
            throw new ArgumentNullException(nameof(mesh));
        
        var meshData = await ExtractMeshData(mesh);
        await Task.Run(() => ExportMeshToSTL(name, meshData, optimizeVertices));
    }
    
    public static void ExportMeshToSTL(string name, MeshData meshData, bool optimizeVertices = true)
    {
        try
        {
            OnExportProgress?.Invoke(0f);
            
            // Step 1: Extract mesh data
           
            OnExportProgress?.Invoke(0.2f);
            
            // Step 2: Strip unused triangles
            var strippedData = StripUnusedTriangles(meshData);
            OnExportProgress?.Invoke(0.4f);
            
            // Step 4: Write to file
            WriteBinarySTL(name, strippedData);
            
            
           
        }
        catch (Exception ex)
        {
            Debug.LogError($"STL Export failed: {ex.Message}");
            throw;
        }
    }

    private static int GetVertexAttributeSize(VertexAttributeDescriptor attr)
    {
        int componentSize = attr.format switch
        {
            VertexAttributeFormat.Float32 => 4,
            VertexAttributeFormat.Float16 => 2,
            VertexAttributeFormat.UNorm8 => 1,
            VertexAttributeFormat.SNorm8 => 1,
            VertexAttributeFormat.UNorm16 => 2,
            VertexAttributeFormat.SNorm16 => 2,
            VertexAttributeFormat.UInt8 => 1,
            VertexAttributeFormat.SInt8 => 1,
            VertexAttributeFormat.UInt16 => 2,
            VertexAttributeFormat.SInt16 => 2,
            VertexAttributeFormat.UInt32 => 4,
            VertexAttributeFormat.SInt32 => 4,
            _ => 4
        };

        return componentSize * attr.dimension;
    }
    private static async Task<MeshData> ExtractMeshData(Mesh mesh)
    {
        var vertexBuffer = mesh.GetVertexBuffer(0);
        var indexBuffer = mesh.GetIndexBuffer();
        
        if (vertexBuffer == null || indexBuffer == null)
        {
            throw new InvalidOperationException("Mesh buffers are null");
        }
        
        int vertexCount = mesh.vertexCount;
        int indexCount = (int)mesh.GetIndexCount(0);
        
        var vertexAttributes = mesh.GetVertexAttributes();
        int positionOffset = -1;
        int normalOffset = -1;
        int stride = 0;
        
        foreach (var attr in vertexAttributes)
        {
            var offset = mesh.GetVertexAttributeOffset(attr.attribute);
            if (attr.attribute == VertexAttribute.Position)
            {
                positionOffset = offset;
            }
            else if (attr.attribute == VertexAttribute.Normal)
            {
                normalOffset = offset;
            }
            stride = Math.Max(stride, offset + GetVertexAttributeSize(attr));
        }
        
        Debug.Log($"Vertex buffer - Count: {vertexCount}, Stride: {stride}, " +
                 $"Position offset: {positionOffset}, Normal offset: {normalOffset}");

        byte[] vertexData = new byte[vertexCount * stride];
        vertexBuffer.GetData(vertexData);

        uint[] indices = new uint[indexCount];
        indexBuffer.GetData(indices);

        Task<(Vector3[] positions, Vector3[] normals, int[] triangles)> Fill()
        {
            Vector3[] positions = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                int baseOffset = i * stride;

                if (positionOffset >= 0)
                {
                    positions[i] = new Vector3(
                        BitConverter.ToSingle(vertexData, baseOffset + positionOffset),
                        BitConverter.ToSingle(vertexData, baseOffset + positionOffset + 4),
                        BitConverter.ToSingle(vertexData, baseOffset + positionOffset + 8)
                    );
                }

                if (normalOffset >= 0)
                {
                    normals[i] = new Vector3(
                        BitConverter.ToSingle(vertexData, baseOffset + normalOffset),
                        BitConverter.ToSingle(vertexData, baseOffset + normalOffset + 4),
                        BitConverter.ToSingle(vertexData, baseOffset + normalOffset + 8)
                    );
                }
            }

            int[] triangles = new int[indexCount];

            for (int i = 0; i < indexCount; i++)
            {
                triangles[i] = (int)indices[i];
            }
            return Task.FromResult((positions, normals, triangles));
        }

        var data = await Task.Run(Fill);

        return new MeshData
        {
            vertices = data.positions,
            normals = data.normals,
            triangles = data.triangles,
            triangleCount = data.triangles.Length / 3
        };
    }
    
    private static MeshData StripUnusedTriangles(MeshData input)
    {
        var validTriangles = new List<int>();
        var validVertices = new List<Vector3>();
        var validNormals = new List<Vector3>();
        var vertexRemapping = new Dictionary<int, int>();
        
        int usedTriangles = 0;

        // Process triangles
        for (int i = 0; i < input.triangles.Length; i += 3)
        {
            int idx1 = input.triangles[i];
            int idx2 = input.triangles[i + 1];
            int idx3 = input.triangles[i + 2];

            Vector3 v1 = input.vertices[idx1];
            Vector3 v2 = input.vertices[idx2];
            Vector3 v3 = input.vertices[idx3];
            
            // Check if any vertex in triangle is marked as unused
            bool isUnused = (v1 - v2).sqrMagnitude <= float.Epsilon && 
                            (v1 - v3).sqrMagnitude <= float.Epsilon &&
                            (v2 - v3).sqrMagnitude <= float.Epsilon;
            
            if (!isUnused)
            {
                // Add vertices if not already added
                int newIdx1 = AddVertexIfNew(idx1, input.vertices, input.normals, 
                                            validVertices, validNormals, vertexRemapping);
                int newIdx2 = AddVertexIfNew(idx2, input.vertices, input.normals, 
                                            validVertices, validNormals, vertexRemapping);
                int newIdx3 = AddVertexIfNew(idx3, input.vertices, input.normals, 
                                            validVertices, validNormals, vertexRemapping);
                
                validTriangles.Add(newIdx1);
                validTriangles.Add(newIdx2);
                validTriangles.Add(newIdx3);
                usedTriangles++;
            }
        }
        
        Debug.Log($"STL Strip: {input.triangleCount} -> {usedTriangles} triangles " +
                 $"({(float)usedTriangles / input.triangleCount * 100:F1}% used)");
        
        return new MeshData
        {
            vertices = validVertices.ToArray(),
            normals = validNormals.ToArray(),
            triangles = validTriangles.ToArray(),
            triangleCount = usedTriangles
        };
    }
    
    private static bool IsVertexUnused(Vector3 vertex)
    {
        return Vector3.Distance(vertex, UNUSED_VERTEX_MARKER) < VERTEX_TOLERANCE;
    }
    
    private static int AddVertexIfNew(int originalIndex, Vector3[] originalVertices, Vector3[] originalNormals,
                                     List<Vector3> newVertices, List<Vector3> newNormals, 
                                     Dictionary<int, int> remapping)
    {
        if (remapping.TryGetValue(originalIndex, out int newIndex))
        {
            return newIndex;
        }
        
        newIndex = newVertices.Count;
        newVertices.Add(originalVertices[originalIndex]);
        newNormals.Add(originalNormals[originalIndex]);
        remapping[originalIndex] = newIndex;
        
        return newIndex;
    }
    
    private static MeshData OptimizeVertices(MeshData input)
    {
        var uniqueVertices = new List<Vector3>();
        var uniqueNormals = new List<Vector3>();
        var newTriangles = new List<int>();
        var vertexMap = new Dictionary<VertexKey, int>();
        
        // Process each triangle
        for (int i = 0; i < input.triangles.Length; i += 3)
        {
            for (int j = 0; j < 3; j++)
            {
                int vertIdx = input.triangles[i + j];
                Vector3 vertex = input.vertices[vertIdx];
                Vector3 normal = input.normals[vertIdx];
                
                var key = new VertexKey(vertex, normal, VERTEX_TOLERANCE);
                
                if (vertexMap.TryGetValue(key, out int existingIndex))
                {
                    newTriangles.Add(existingIndex);
                    
                    // Average normals for smoother shading
                    uniqueNormals[existingIndex] = 
                        ((uniqueNormals[existingIndex] + normal) * 0.5f).normalized;
                }
                else
                {
                    int newIndex = uniqueVertices.Count;
                    uniqueVertices.Add(vertex);
                    uniqueNormals.Add(normal);
                    vertexMap[key] = newIndex;
                    newTriangles.Add(newIndex);
                }
            }
        }
        
        Debug.Log($"STL Optimize: {input.vertices.Length} -> {uniqueVertices.Count} vertices " +
                 $"({(float)uniqueVertices.Count / input.vertices.Length * 100:F1}% unique)");
        
        return new MeshData
        {
            vertices = uniqueVertices.ToArray(),
            normals = uniqueNormals.ToArray(),
            triangles = newTriangles.ToArray(),
            triangleCount = input.triangleCount
        };
    }

    private static void WriteAsciiSTL(string name, MeshData data)
    {
        string directory = Path.Combine(Application.dataPath, SAVE_DIR);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
       
        var path = Path.Combine(directory, name + ".stl");
        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine($"solid Unity_STL_Export_{DateTime.Now:yyyyMMdd_HHmmss}");

            for (int i = 0; i < data.triangles.Length; i += 3)
            {
                Vector3 v1 = data.vertices[data.triangles[i]];
                Vector3 v2 = data.vertices[data.triangles[i + 1]];
                Vector3 v3 = data.vertices[data.triangles[i + 2]];

                // Calculate face normal
                Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;
     
                writer.WriteLine($"  facet normal {normal.x:F6} {normal.y:F6} {normal.z:F6}");
                writer.WriteLine("    outer loop");
                writer.WriteLine($"      vertex {v1.x:F6} {v1.y:F6} {v1.z:F6}");
                writer.WriteLine($"      vertex {v2.x:F6} {v2.y:F6} {v2.z:F6}");
                writer.WriteLine($"      vertex {v3.x:F6} {v3.y:F6} {v3.z:F6}");
                writer.WriteLine("    endloop");
                writer.WriteLine("  endfacet");
            }

            writer.WriteLine("endsolid");
        }

        OnExportProgress?.Invoke(1f);
        OnExportCompleted?.Invoke(path, data.triangleCount, data.triangleCount);
    }
    
    private static void WriteBinarySTL(string name, MeshData data)
    {
        string directory = Path.Combine(Application.dataPath, SAVE_DIR);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var path = Path.Combine(directory, name + ".stl");
        long size = 0;
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            // Header (80 bytes)
            var header = new byte[80];
            var headerText = Encoding.ASCII.GetBytes($"Unity STL Export - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Array.Copy(headerText, header, Math.Min(headerText.Length, 80));
            writer.Write(header);
            
            // Triangle count
            writer.Write((uint)data.triangleCount);
            
            // Write triangles
            for (int i = 0; i < data.triangles.Length; i += 3)
            {
                Vector3 v1 = data.vertices[data.triangles[i]];
                Vector3 v2 = data.vertices[data.triangles[i + 1]];
                Vector3 v3 = data.vertices[data.triangles[i + 2]];
                
                // Calculate face normal
                Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;
  
                // Write normal
                writer.Write(normal.x);
                writer.Write(normal.y);
                writer.Write(normal.z);
                
                // Write vertices
                WriteVertex(writer, v1);
                WriteVertex(writer, v2);
                WriteVertex(writer, v3);
                
                // Attribute byte count (unused)
                writer.Write((ushort)0);
            }
            writer.Flush();
            
        }

        size = File.OpenRead(path).Length;
        OnExportProgress?.Invoke(1f);
        OnExportCompleted?.Invoke(path, data.triangleCount, (int)size);
    }
    
    
    private static void WriteVertex(BinaryWriter writer, Vector3 vertex)
    {
        writer.Write(vertex.x);
        writer.Write(vertex.y);
        writer.Write(vertex.z);
    }
    
    public struct MeshData
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public int[] triangles;
        public int triangleCount;
    }
    
    private struct VertexKey : IEquatable<VertexKey>
    {
        private readonly Vector3 position;
        private readonly Vector3 normal;
        private readonly float tolerance;
        
        public VertexKey(Vector3 pos, Vector3 norm, float tol)
        {
            position = pos;
            normal = norm;
            tolerance = tol;
        }
        
        public bool Equals(VertexKey other)
        {
            return Vector3.Distance(position, other.position) < tolerance &&
                   Vector3.Distance(normal, other.normal) < tolerance;
        }
        
        public override bool Equals(object obj)
        {
            return obj is VertexKey other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            // Quantize position for consistent hashing
            int x = Mathf.RoundToInt(position.x / tolerance);
            int y = Mathf.RoundToInt(position.y / tolerance);
            int z = Mathf.RoundToInt(position.z / tolerance);
            return HashCode.Combine(x, y, z);
        }
    }
}
