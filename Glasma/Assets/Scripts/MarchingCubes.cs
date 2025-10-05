using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public enum SculptSolidType
{
    Box = 0,
    Sphere = 1,
    Cylinder = 2,
    Capsule = 3,
    Tetrahedron = 4
}

public enum SculptOperation
{
    Add = 0,
    Multiply = 1,
    Intersect = 2,
    Interpolate = 3,
    Cut = 4
}

[StructLayout(LayoutKind.Sequential)]
[Serializable]
public struct SculptSolid
{
    [HideInInspector] public Matrix4x4 invTransform; 
    public float scale;
    public SculptSolidType type;  
    public SculptOperation op;
    [Range(0f, 1f)] public float feather;
    public float exp;
    public float lerp;
    public int priority;

    public static SculptSolid Default => new()
    {
        scale = 1f,
        feather = 0.1f
    };

    public override bool Equals(object obj)
    {
        return base.Equals(obj);
    }

    public bool Equals(SculptSolid other)
    {
        return invTransform.Equals(other.invTransform) && scale.Equals(other.scale) 
                                                       && type == other.type 
                                                       && op == other.op && feather.Equals(other.feather) && exp.Equals(other.exp) 
                                                       && lerp.Equals(other.lerp) && priority == other.priority;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(invTransform, scale, (int) type, (int) op, feather, exp, lerp, priority);
    }
}

public class MarchingCubes : System.IDisposable
{
    public int Budget => triangleBudget;
    public Mesh Mesh => mesh;

    public MarchingCubes(ComputeShader compute, int maxBudget, Vector3Int holeResolution)
    {
        Initialize(compute, maxBudget, holeResolution);
    } 

    public void Dispose()
      => ReleaseAll();
    
    int triangleBudget;

    private ComputeShader compute;
    
    private ComputeBuffer triangleTable;
    private ComputeBuffer counterBuffer; 
    private ComputeBuffer solidsBuffer;

    private Mesh mesh;
    private GraphicsBuffer vertexBuffer;
    private GraphicsBuffer indexBuffer;
    private int constructKernel, clearKernel, initHolesKernel, addHoleKernel, addHoleSoftKernel;
    public RenderTexture holeMaskTexture;
    private Vector3Int holeMaskResolution;
    

    private int CalculateBudget(int maxBudget)
    {
        return maxBudget;
    }
    
    private void Initialize(ComputeShader computeSource, int maxBudget, Vector3Int holeMaskRes)
    {
        holeMaskResolution = holeMaskRes;
        triangleBudget = CalculateBudget(maxBudget);
        compute = computeSource;
        constructKernel = compute.FindKernel("Construct");
        clearKernel = compute.FindKernel("Clear");
    
        AllocateBuffers();
        AllocateMesh(3 * triangleBudget);
        if (holeMaskRes.sqrMagnitude > 0)
        {
            CreateHoleMask();
        }
    }

    private void CreateHoleMask()
    {
        holeMaskTexture = new RenderTexture(
            holeMaskResolution.x,
            holeMaskResolution.y,
            0, // depth
            RenderTextureFormat.RFloat
        );
        holeMaskTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        holeMaskTexture.volumeDepth = holeMaskResolution.z;
        holeMaskTexture.enableRandomWrite = true;
        holeMaskTexture.filterMode = FilterMode.Bilinear;
        holeMaskTexture.wrapMode = TextureWrapMode.Clamp;
        holeMaskTexture.Create();
        
        // Get kernel indices
        initHolesKernel = compute.FindKernel("InitHoleMask");
        addHoleKernel = compute.FindKernel("AddHole");
        addHoleSoftKernel = compute.FindKernel("AddHoleSoft");
        
        // Initialize texture to all solid (1.0)
        compute.SetTexture(initHolesKernel, "HoleMask", holeMaskTexture);
        
        compute.SetInts("HoleMaskDims", 
            holeMaskResolution.x, holeMaskResolution.y, holeMaskResolution.z);

        compute.Dispatch(initHolesKernel, holeMaskResolution.x, holeMaskResolution.y, holeMaskResolution.z);
    }
    
    private void ReleaseAll()
    {
        ReleaseBuffers();
        ReleaseMesh();
    }
    
    public void Run(Vector3Int dimensions)
    {
        if (mesh == null)
        {
            AllocateMesh(triangleBudget * 3);
        }
        
        counterBuffer.SetCounterValue(0);

        compute.SetInts("Dims", dimensions.x, dimensions.y, dimensions.z);
        compute.SetInt("MaxTriangle", triangleBudget);
        
        compute.SetBuffer(constructKernel, "TriangleTable", triangleTable);
        compute.SetBuffer(constructKernel, "VertexBuffer", vertexBuffer);
        compute.SetBuffer(constructKernel, "IndexBuffer", indexBuffer);
        compute.SetBuffer(constructKernel, "Counter", counterBuffer);
        
        compute.SetTexture(constructKernel, "HoleMask", holeMaskTexture);
        
        compute.SetInts("HoleMaskDims", 
            holeMaskResolution.x, holeMaskResolution.y, holeMaskResolution.z);
        
        compute.DispatchThreads(constructKernel, dimensions.x, dimensions.y, dimensions.z);
        compute.SetBuffer(clearKernel, "VertexBuffer", vertexBuffer);
        compute.SetBuffer(clearKernel, "IndexBuffer", indexBuffer);
        compute.SetBuffer(clearKernel, "Counter", counterBuffer);
        compute.DispatchThreads(clearKernel, 1024, 1, 1);

        var ext = new Vector3(1f / dimensions.x, 1f  / dimensions.y, 1f  / dimensions.z) * dimensions.x;
        mesh.bounds = new Bounds(Vector3.zero, ext);
    }
    
    public void SetSculptSolids(List<SculptSolid> solids)
    {
        if (solids.Count == 0)
        {
            solids.Add(SculptSolid.Default);
        }

        if (solidsBuffer != null && solidsBuffer.count != solids.Count)
        {
            solidsBuffer.Release();
            solidsBuffer = null;
        }

        if (solidsBuffer != null)
        {
            solidsBuffer.SetData(solids);
            return;
        }

        compute.SetInt("SolidCount", solids.Count);
        
      
        solidsBuffer = new ComputeBuffer(solids.Count, Marshal.SizeOf(typeof(SculptSolid)));
        
        compute.SetBuffer(constructKernel, "Solids", solidsBuffer);
      
        solidsBuffer.SetData(solids);
    }
    
    public void AddHole(Vector3Int position, Vector3Int size, bool softEdges = false)
    {
        // Clamp position and size to valid ranges
        position = Vector3Int.Max(Vector3Int.zero, position);
        position = Vector3Int.Min(position, holeMaskResolution - Vector3Int.one);
        
        size = Vector3Int.Max(Vector3Int.one, size);
        size = Vector3Int.Min(size, holeMaskResolution - position);
        
        int kernel = softEdges ? addHoleSoftKernel : addHoleKernel;
        
        compute.SetTexture(kernel, "HoleMask", holeMaskTexture);
        compute.SetInts("HoleMaskDims", 
            holeMaskResolution.x, holeMaskResolution.y, holeMaskResolution.z);
        compute.SetInts("HolePosition", position.x, position.y, position.z);
        
        compute.SetInts("HoleSize", size.x, size.y, size.z);
        compute.SetFloat("HoleValue", 0.0f); // 0 = hole
        
        compute.Dispatch(kernel, size.x, size.y, size.z);
    }
    
    public void AddHole(Vector3Int position, int cubeSize, bool softEdges = false)
    {
        AddHole(position, new Vector3Int(cubeSize, cubeSize, cubeSize), softEdges);
    }
    
    public void ClearAllHoles()
    {
        // Re-initialize to all solid
        int threadGroups = Mathf.CeilToInt(holeMaskResolution.x / 4.0f);
        compute.SetTexture(initHolesKernel, "HoleMask", holeMaskTexture);
        compute.Dispatch(initHolesKernel, threadGroups, threadGroups, threadGroups);
    }
    
    private void AllocateBuffers()
    {
        triangleTable = new ComputeBuffer(256, sizeof(ulong));
        triangleTable.SetData(TriangleTable);
        counterBuffer = new ComputeBuffer(1, 4, ComputeBufferType.Counter);
    }

    private void ReleaseBuffers()
    {
        triangleTable?.Dispose();
        counterBuffer?.Dispose();
        solidsBuffer?.Dispose();
    }
    
    private void AllocateMesh(int vertexCount)
    {
        mesh = new Mesh();

        mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

        var vp = new VertexAttributeDescriptor
          (VertexAttribute.Position, VertexAttributeFormat.Float32, 3);

        var vn = new VertexAttributeDescriptor
          (VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
        
        mesh.SetVertexBufferParams(vertexCount, vp, vn);
        mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);
        
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexCount),
                         MeshUpdateFlags.DontRecalculateBounds);

        try
        {
            vertexBuffer = mesh.GetVertexBuffer(0);
            indexBuffer = mesh.GetIndexBuffer();
        }
        catch
        {
            Object.DestroyImmediate(mesh);
            mesh = null;
        }
    }

    private void ReleaseMesh()
    {
        vertexBuffer?.Dispose();
        indexBuffer?.Dispose();
        Object.Destroy(mesh);
        holeMaskTexture?.Release();
    }
    
    private static readonly ulong [] TriangleTable =
    {
        0xffffffffffffffffUL,
        0xfffffffffffff380UL,
        0xfffffffffffff910UL,
        0xffffffffff189381UL,
        0xfffffffffffffa21UL,
        0xffffffffffa21380UL,
        0xffffffffff920a29UL,
        0xfffffff89a8a2382UL,
        0xfffffffffffff2b3UL,
        0xffffffffff0b82b0UL,
        0xffffffffffb32091UL,
        0xfffffffb89b912b1UL,
        0xffffffffff3ab1a3UL,
        0xfffffffab8a801a0UL,
        0xfffffff9ab9b3093UL,
        0xffffffffffb8aa89UL,
        0xfffffffffffff874UL,
        0xffffffffff437034UL,
        0xffffffffff748910UL,
        0xfffffff137174914UL,
        0xffffffffff748a21UL,
        0xfffffffa21403743UL,
        0xfffffff748209a29UL,
        0xffff4973727929a2UL,
        0xffffffffff2b3748UL,
        0xfffffff40242b74bUL,
        0xfffffffb32748109UL,
        0xffff1292b9b49b74UL,
        0xfffffff487ab31a3UL,
        0xffff4b7401b41ab1UL,
        0xffff30bab9b09874UL,
        0xfffffffab99b4b74UL,
        0xfffffffffffff459UL,
        0xffffffffff380459UL,
        0xffffffffff051450UL,
        0xfffffff513538458UL,
        0xffffffffff459a21UL,
        0xfffffff594a21803UL,
        0xfffffff204245a25UL,
        0xffff8434535235a2UL,
        0xffffffffffb32459UL,
        0xfffffff594b802b0UL,
        0xfffffffb32510450UL,
        0xffff584b82852512UL,
        0xfffffff45931ab3aUL,
        0xffffab81a8180594UL,
        0xffff30bab5b05045UL,
        0xfffffffb8aa85845UL,
        0xffffffffff975879UL,
        0xfffffff375359039UL,
        0xfffffff751710870UL,
        0xffffffffff753351UL,
        0xfffffff21a759879UL,
        0xffff37503505921aUL,
        0xffff25a758528208UL,
        0xfffffff7533525a2UL,
        0xfffffff2b3987597UL,
        0xffffb72029279759UL,
        0xffff751871810b32UL,
        0xfffffff51771b12bUL,
        0xffffb3a31a758859UL,
        0xf0aba010b7905075UL,
        0xf07570805a30b0abUL,
        0xffffffffff5b75abUL,
        0xfffffffffffff56aUL,
        0xffffffffff6a5380UL,
        0xffffffffff6a5109UL,
        0xfffffff6a5891381UL,
        0xffffffffff162561UL,
        0xfffffff803621561UL,
        0xfffffff620609569UL,
        0xffff823625285895UL,
        0xffffffffff56ab32UL,
        0xfffffff56a02b80bUL,
        0xfffffff6a5b32910UL,
        0xffffb892b92916a5UL,
        0xfffffff315356b36UL,
        0xffff6b51505b0b80UL,
        0xffff9505606306b3UL,
        0xfffffff89bb96956UL,
        0xffffffffff8746a5UL,
        0xfffffffa56374034UL,
        0xfffffff7486a5091UL,
        0xffff49737179156aUL,
        0xfffffff874156216UL,
        0xffff743403625521UL,
        0xffff620560509748UL,
        0xf962695923497937UL,
        0xfffffff56a4872b3UL,
        0xffffb720242746a5UL,
        0xffff6a5b32874910UL,
        0xf6a54b7b492b9129UL,
        0xffff6b51535b3748UL,
        0xfb404b7b016b5b15UL,
        0xf74836b630560950UL,
        0xffff9b7974b96956UL,
        0xffffffffffa4694aUL,
        0xfffffff380a946a4UL,
        0xfffffff04606a10aUL,
        0xffffa16468618138UL,
        0xfffffff462421941UL,
        0xffff462942921803UL,
        0xffffffffff624420UL,
        0xfffffff624428238UL,
        0xfffffff32b46a94aUL,
        0xffff6a4a94b82280UL,
        0xffffa164606102b3UL,
        0xf1b8b12184a16146UL,
        0xffff36b319639469UL,
        0xf14641916b0181b8UL,
        0xfffffff4600636b3UL,
        0xffffffffff86b846UL,
        0xfffffffa98a876a7UL,
        0xffffa76a907a0370UL,
        0xffff0818717a176aUL,
        0xfffffff37117a76aUL,
        0xffff768981861621UL,
        0xf937390976192962UL,
        0xfffffff206607087UL,
        0xffffffffff276237UL,
        0xffff76898a86ab32UL,
        0xf7a9a76790b72702UL,
        0xfb32a767a1871081UL,
        0xffff17616a71b12bUL,
        0xf63136b619768698UL,
        0xffffffffff76b190UL,
        0xffff06b0b3607087UL,
        0xfffffffffffff6b7UL,
        0xfffffffffffffb67UL,
        0xffffffffff67b803UL,
        0xffffffffff67b910UL,
        0xfffffff67b138918UL,
        0xffffffffff7b621aUL,
        0xfffffff7b6803a21UL,
        0xfffffff7b69a2092UL,
        0xffff89a38a3a27b6UL,
        0xffffffffff726327UL,
        0xfffffff026067807UL,
        0xfffffff910732672UL,
        0xffff678891681261UL,
        0xfffffff73171a67aUL,
        0xffff801781a7167aUL,
        0xffff7a69a0a70730UL,
        0xfffffff9a88a7a67UL,
        0xffffffffff68b486UL,
        0xfffffff640603b63UL,
        0xfffffff109648b68UL,
        0xffff63b139369649UL,
        0xfffffff1a28b6486UL,
        0xffff640b60b03a21UL,
        0xffff9a2920b648b4UL,
        0xf36463b34923a39aUL,
        0xfffffff264248328UL,
        0xffffffffff264240UL,
        0xffff834642432091UL,
        0xfffffff642241491UL,
        0xffff1a6648168318UL,
        0xfffffff40660a01aUL,
        0xf39a9303a6834364UL,
        0xffffffffff4a649aUL,
        0xffffffffffb67594UL,
        0xfffffff67b594380UL,
        0xfffffffb67045105UL,
        0xffff51345343867bUL,
        0xfffffffb6721a459UL,
        0xffff594380a217b6UL,
        0xffff204a24a45b67UL,
        0xf67b25a523453843UL,
        0xfffffff945267327UL,
        0xffff786260680459UL,
        0xffff045051673263UL,
        0xf851584812786826UL,
        0xffff73167161a459UL,
        0xf459078701671a61UL,
        0xfa737a6a305a4a04UL,
        0xffffa84a458a7a67UL,
        0xfffffff98b9b6596UL,
        0xffff590650360b63UL,
        0xffffb65510b508b0UL,
        0xfffffff1355363b6UL,
        0xffff65b8b9b59a21UL,
        0xfa21965690b603b0UL,
        0xf52025a50865b58bUL,
        0xffff35a3a25363b6UL,
        0xffff283265825985UL,
        0xfffffff260069659UL,
        0xf826283865081851UL,
        0xffffffffff612651UL,
        0xf698965683a61631UL,
        0xffff06505960a01aUL,
        0xffffffffffa65830UL,
        0xfffffffffffff65aUL,
        0xffffffffffb57a5bUL,
        0xfffffff03857ba5bUL,
        0xfffffff091ba57b5UL,
        0xffff1381897ba57aUL,
        0xfffffff15717b21bUL,
        0xffffb27571721380UL,
        0xffff7b2209729579UL,
        0xf289823295b27257UL,
        0xfffffff573532a52UL,
        0xffff52a578258028UL,
        0xffff2a37353a5109UL,
        0xf25752a278129289UL,
        0xffffffffff573531UL,
        0xfffffff571170780UL,
        0xfffffff735539309UL,
        0xffffffffff795789UL,
        0xfffffff8ba8a5485UL,
        0xffff03bba50b5405UL,
        0xffff54aba8a48910UL,
        0xf41314943b54a4baUL,
        0xffff8548b2582152UL,
        0xfb151b2b543b0b40UL,
        0xf58b8545b2950520UL,
        0xffffffffff3b2549UL,
        0xffff483543253a52UL,
        0xfffffff0244252a5UL,
        0xf910854583a532a3UL,
        0xffff2492914252a5UL,
        0xfffffff153358548UL,
        0xffffffffff501540UL,
        0xffff530509358548UL,
        0xfffffffffffff549UL,
        0xfffffffba9b947b4UL,
        0xffffba97b9794380UL,
        0xffffb470414b1ba1UL,
        0xf4bab474a1843413UL,
        0xffff219b294b97b4UL,
        0xf3801b2b197b9479UL,
        0xfffffff04224b47bUL,
        0xffff42343824b47bUL,
        0xffff947732972a92UL,
        0xf70207872a4797a9UL,
        0xfa040a1a472a3a73UL,
        0xffffffffff4782a1UL,
        0xfffffff317714194UL,
        0xffff178180714194UL,
        0xffffffffff347304UL,
        0xfffffffffffff784UL,
        0xffffffffff8ba8a9UL,
        0xfffffffa9bb93903UL,
        0xfffffffba88a0a10UL,
        0xffffffffffa3ba13UL,
        0xfffffff8b99b1b21UL,
        0xffff9b2921b93903UL,
        0xffffffffffb08b20UL,
        0xfffffffffffffb23UL,
        0xfffffff98aa82832UL,
        0xffffffffff2902a9UL,
        0xffff8a1810a82832UL,
        0xfffffffffffff2a1UL,
        0xffffffffff819831UL,
        0xfffffffffffff190UL,
        0xfffffffffffff830UL,
        0xffffffffffffffffUL 
         };
}
