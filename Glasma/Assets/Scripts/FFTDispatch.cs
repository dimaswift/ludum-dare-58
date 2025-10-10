using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public class FFT : IDisposable
{
    private ComputeShader shader;
    const int GROUP_SIZE_X = 8;   // must match shader - reduced for 3D
    const int GROUP_SIZE_Y = 8;   // must match shader
    const int GROUP_SIZE_Z = 8;   // must match shader
    private int width;
    private int height;
    private int depth;

    private int computeBitRevIndicesKernel;
    private int computeTwiddleFactorsKernel;
    private int convertTex3DToComplexKernel;
    private int convertComplexMagToTex3DKernel;
    private int convertComplexMagToTex3DScaledKernel;
    private int convertComplexPhaseToTex3DKernel;
    private int centerComplexKernel;
    private int conjugateComplexKernel;
    private int divideComplexByDimensionsKernel;
    private int bitRevByRowKernel;
    private int bitRevByColKernel;
    private int bitRevByDepthKernel;
    private int butterflyByRowKernel;
    private int butterflyByColKernel;
    private int butterflyByDepthKernel;
    private int clearBufferKernel;

    private ComputeBuffer bitRevRow;
    private ComputeBuffer bitRevCol;
    private ComputeBuffer bitRevDepth;
    private ComputeBuffer twiddleRow;
    private ComputeBuffer twiddleCol;
    private ComputeBuffer twiddleDepth;

    // these 2 are to be swapped back and forth
    private ComputeBuffer bufferA;
    private ComputeBuffer bufferB;
    

    
    [StructLayout(LayoutKind.Sequential)]
    public struct ComplexF
    {
        public float real;
        public float imag;

        public ComplexF(float r, float i = 0)
        {
            real = r;
            imag = i;
        }

        public static implicit operator ComplexF(int r)
        {
            return new ComplexF(r);
        }

        public override string ToString()
        {
            return string.Format("({0}, {1})", real, imag);
        }
    }


    public FFT(ComputeShader fftShader)
    {
        shader = Object.Instantiate(fftShader);

        GetKernelHandles();
    }

    public void Init(int width, int height, int depth)
    {
        this.width = width;
        this.height = height;
        this.depth = depth;

        shader.SetInt("WIDTH", this.width);
        shader.SetInt("HEIGHT", this.height);
        shader.SetInt("DEPTH", this.depth);

        InitBitRevBuffers();
        InitTwiddleBuffers();
        InitTempBuffers();
    }

    public void Dispose()
    {
        ReleaseBitRevBuffers();
        ReleaseTwiddleBuffers();
        ReleaseTempBuffers();
        Object.Destroy(shader);
    }

    public void Load(RenderTexture source)
    {
      
        ConvertTex3DToComplex(source, bufferA);

    }

    public void RecenterData()
    {
        CenterComplex(bufferA, bufferB);
        SwapBuffers(ref bufferB, ref bufferA);
        // _bufferA contains data
    }

    public void GetMagnitudeSpectrumScaled(RenderTexture tex)
    {
        ConvertComplexMagToTex3DScaled(bufferA, tex);
    }

    public void GetPhaseAngle(RenderTexture tex)
    {
        ConvertComplexPhaseToTex3D(bufferA, tex);
    }

    public void Forward()
    {
      //  if(!dataInA) SwapBuffers(ref bufferB, ref bufferA);
        
      
        
        // _bufferA should contain data
        // X direction (rows)
        BitRevByRow(bufferA, bufferB);
        ButterflyByRow(ref bufferB, ref bufferA);

        // Y direction (columns)
        BitRevByCol(bufferA, bufferB);
        ButterflyByCol(ref bufferB, ref bufferA);
        
        // Z direction (depth)
        BitRevByDepth(bufferA, bufferB);
        ButterflyByDepth(ref bufferB, ref bufferA);

        // _bufferA contains data
    }

    public void Inverse()
    {
        // _bufferA should contain data

        ConjugateComplex(bufferA, bufferB);
        SwapBuffers(ref bufferA, ref bufferB);

        Forward();

        ConjugateComplex(bufferA, bufferB);
        DivideComplexByDimensions(bufferB, bufferA);

        // _bufferA contains data
    }

    private void GetKernelHandles()
    {
        computeTwiddleFactorsKernel = shader.FindKernel("ComputeTwiddleFactors");
        computeBitRevIndicesKernel = shader.FindKernel("ComputeBitRevIndices");
        convertTex3DToComplexKernel = shader.FindKernel("ConvertTex3DToComplex");
        convertComplexMagToTex3DKernel = shader.FindKernel("ConvertComplexMagToTex3D");
        convertComplexMagToTex3DScaledKernel = shader.FindKernel("ConvertComplexMagToTex3DScaled");
        convertComplexPhaseToTex3DKernel = shader.FindKernel("ConvertComplexPhaseToTex3D");
        centerComplexKernel = shader.FindKernel("CenterComplex");
        conjugateComplexKernel = shader.FindKernel("ConjugateComplex");
        divideComplexByDimensionsKernel = shader.FindKernel("DivideComplexByDimensions");
        bitRevByRowKernel = shader.FindKernel("BitRevByRow");
        bitRevByColKernel = shader.FindKernel("BitRevByCol");
        bitRevByDepthKernel = shader.FindKernel("BitRevByDepth");
        butterflyByRowKernel = shader.FindKernel("ButterflyByRow");
        butterflyByColKernel = shader.FindKernel("ButterflyByCol");
        butterflyByDepthKernel = shader.FindKernel("ButterflyByDepth");
        clearBufferKernel = shader.FindKernel("ClearBuffer");
    }

    private void InitBitRevBuffers()
    {
        bitRevRow = new ComputeBuffer(width, sizeof(uint));
        ComputeBitRevIndices(width, bitRevRow);

        bitRevCol = new ComputeBuffer(height, sizeof(uint));
        ComputeBitRevIndices(height, bitRevCol);

        bitRevDepth = new ComputeBuffer(depth, sizeof(uint));
        ComputeBitRevIndices(depth, bitRevDepth);
    }

    private void InitTwiddleBuffers()
    {
        twiddleRow = CreateComplexBuffer(width / 2);
        ComputeTwiddleFactors(width, twiddleRow);

        twiddleCol = CreateComplexBuffer(height / 2);
        ComputeTwiddleFactors(height, twiddleCol);

        twiddleDepth = CreateComplexBuffer(depth / 2);
        ComputeTwiddleFactors(depth, twiddleDepth);
    }

    private void InitTempBuffers()
    {
        bufferA = CreateComplexBuffer(width, height, depth);
        bufferB = CreateComplexBuffer(width, height, depth);
    }

    private static ComputeBuffer CreateComplexBuffer(int width, int height = 1, int depth = 1)
    {
        return new ComputeBuffer(width * height * depth, sizeof(float) * 2);
    }
    private static int CeilDiv(int a, int b) => (a + b - 1) / b;
    private void SwapBuffers(ref ComputeBuffer a, ref ComputeBuffer b)
    {
        (a, b) = (b, a);
    }
    
    private void Dispatch(int kernelHandle)
    {
        Dispatch(kernelHandle,  
            CeilDiv(width, GROUP_SIZE_X), 
            CeilDiv(height, GROUP_SIZE_Y), 
            CeilDiv(depth, GROUP_SIZE_Z));
    }

    private void Dispatch(int kernelHandle, int xGroups, int yGroups, int zGroups = 1)
    {
        shader.Dispatch(kernelHandle, xGroups, yGroups, zGroups);
    }

    private void ComputeBitRevIndices(int N, ComputeBuffer bitRevIndices)
    {
        shader.SetInt("N", N);
        shader.SetBuffer(computeBitRevIndicesKernel, "BitRevIndices", bitRevIndices);
        Dispatch(computeBitRevIndicesKernel, CeilDiv(N , GROUP_SIZE_X), 1, 1);
    }

    private void ComputeTwiddleFactors(int N, ComputeBuffer twiddleFactors)
    {
        shader.SetInt("N", N);
        shader.SetBuffer(computeTwiddleFactorsKernel, "TwiddleFactors", twiddleFactors);
        Dispatch(computeTwiddleFactorsKernel, CeilDiv(N / 2, GROUP_SIZE_X), 1, 1);  // Only dispatch N/2 threads!
    }
 
    private void ConvertTex3DToComplex(Texture src, ComputeBuffer dst)
    {
        shader.SetTexture(convertTex3DToComplexKernel, "SrcTex3D", src);
        shader.SetBuffer(convertTex3DToComplexKernel, "Dst", dst);
        Dispatch(convertTex3DToComplexKernel);
    }

    private void CenterComplex(ComputeBuffer src, ComputeBuffer dst)
    {
        shader.SetBuffer(centerComplexKernel, "Src", src);
        shader.SetBuffer(centerComplexKernel, "Dst", dst);
        Dispatch(centerComplexKernel);
    }

    private void BitRevByRow(ComputeBuffer src, ComputeBuffer dst)
    {
        shader.SetBuffer(bitRevByRowKernel, "BitRevIndices", bitRevRow);
        shader.SetBuffer(bitRevByRowKernel, "Src", src);
        shader.SetBuffer(bitRevByRowKernel, "Dst", dst);
        Dispatch(bitRevByRowKernel);
    }

    private void BitRevByCol(ComputeBuffer src, ComputeBuffer dst)
    {
        shader.SetBuffer(bitRevByColKernel, "BitRevIndices", bitRevCol);
        shader.SetBuffer(bitRevByColKernel, "Src", src);
        shader.SetBuffer(bitRevByColKernel, "Dst", dst);
        Dispatch(bitRevByColKernel);
    }

    private void BitRevByDepth(ComputeBuffer src, ComputeBuffer dst)
    {
        shader.SetBuffer(bitRevByDepthKernel, "BitRevIndices", bitRevDepth);
        shader.SetBuffer(bitRevByDepthKernel, "Src", src);
        shader.SetBuffer(bitRevByDepthKernel, "Dst", dst);
        Dispatch(bitRevByDepthKernel);
    }

    // Both src and dst will be modified
    private void ButterflyByRow(ref ComputeBuffer src, ref ComputeBuffer dst)
    {
       
        var swapped = false;
        for (int stride = 2; stride <= width; stride *= 2)
        {
            shader.SetInt("BUTTERFLY_STRIDE", stride);
            shader.SetBuffer(butterflyByRowKernel, "TwiddleFactors", twiddleRow);
            shader.SetBuffer(butterflyByRowKernel, "Src", swapped ? dst : src);
            shader.SetBuffer(butterflyByRowKernel, "Dst", swapped ? src : dst);
            Dispatch(butterflyByRowKernel);
            swapped = !swapped;
        }

        if (!swapped)
        {
            SwapBuffers(ref src, ref dst);
        }
    }

    // Both src and dst will be modified
    private void ButterflyByCol(ref ComputeBuffer src, ref ComputeBuffer dst)
    {
      
        var swapped = false;
        for (int stride = 2; stride <= height; stride *= 2)
        {
            shader.SetInt("BUTTERFLY_STRIDE", stride);
            shader.SetBuffer(butterflyByColKernel, "TwiddleFactors", twiddleCol);
            shader.SetBuffer(butterflyByColKernel, "Src", swapped ? dst : src);
            shader.SetBuffer(butterflyByColKernel, "Dst", swapped ? src : dst);
            Dispatch(butterflyByColKernel);
            swapped = !swapped;
        }

        if (!swapped)
        {
            SwapBuffers(ref src, ref dst);
        }
    }

    // Both src and dst will be modified
    private void ButterflyByDepth(ref ComputeBuffer src, ref ComputeBuffer dst)
    {
        var swapped = false;
        for (int stride = 2; stride <= depth; stride *= 2)
        {
            shader.SetInt("BUTTERFLY_STRIDE", stride);
            shader.SetBuffer(butterflyByDepthKernel, "TwiddleFactors", twiddleDepth);  // ← Move inside loop
            shader.SetBuffer(butterflyByDepthKernel, "Src", swapped ? dst : src);
            shader.SetBuffer(butterflyByDepthKernel, "Dst", swapped ? src : dst);
            Dispatch(butterflyByDepthKernel);
            swapped = !swapped;
        }

        if (!swapped)
        {
            SwapBuffers(ref src, ref dst);
        }
    }

    private void ConvertComplexMagToTex3D(ComputeBuffer src, RenderTexture dst)
    {
        shader.SetBuffer(convertComplexMagToTex3DKernel, "Src", src);
        shader.SetTexture(convertComplexMagToTex3DKernel, "DstTex3D", dst);
        Dispatch(convertComplexMagToTex3DKernel);
    }

    private void ConvertComplexMagToTex3DScaled(ComputeBuffer src, RenderTexture dst)
    {
        shader.SetBuffer(convertComplexMagToTex3DScaledKernel, "Src", src);
        shader.SetTexture(convertComplexMagToTex3DScaledKernel, "DstTex3D", dst);
        Dispatch(convertComplexMagToTex3DScaledKernel);
    }

    private void ConvertComplexPhaseToTex3D(ComputeBuffer src, RenderTexture dst)
    {
        shader.SetBuffer(convertComplexPhaseToTex3DKernel, "Src", src);
        shader.SetTexture(convertComplexPhaseToTex3DKernel, "DstTex3D", dst);
        Dispatch(convertComplexPhaseToTex3DKernel);
    }

    private void ConjugateComplex(ComputeBuffer src, ComputeBuffer dst)
    {
        shader.SetBuffer(conjugateComplexKernel, "Src", src);
        shader.SetBuffer(conjugateComplexKernel, "Dst", dst);
        Dispatch(conjugateComplexKernel);
    }

    private void DivideComplexByDimensions(ComputeBuffer src, ComputeBuffer dst)
    {
        shader.SetBuffer(divideComplexByDimensionsKernel, "Src", src);
        shader.SetBuffer(divideComplexByDimensionsKernel, "Dst", dst);
        Dispatch(divideComplexByDimensionsKernel);
    }
    
    private void ReleaseTwiddleBuffers()
    {
        twiddleRow?.Release();
        twiddleCol?.Release();
        twiddleDepth?.Release();
    }

    private void ReleaseBitRevBuffers()
    {
        bitRevRow?.Release();
        bitRevCol?.Release();
        bitRevDepth?.Release();
    }

    private void ReleaseTempBuffers()
    {
        bufferA?.Release();
        bufferB?.Release();
    }
}
