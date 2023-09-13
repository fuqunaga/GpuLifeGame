using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Random = System.Random;

[RequireComponent(typeof(Camera))]
public class LifeGame : MonoBehaviour
{
    #region Type Define
    
    private static class CommonParam
    {
        public static readonly int Width = Shader.PropertyToID("_Width");
        public static readonly int Height = Shader.PropertyToID("_Height");
    }

    private static class CsParam
    {
        public const string KernelStep = "Step";
        public static readonly int WriteBuf = Shader.PropertyToID("_WriteBuf");
        public static readonly int ReadBuf = Shader.PropertyToID("_ReadBuf");

        public const string KernelInput = "Input";
        public static readonly int InputPos = Shader.PropertyToID("_InputPos");
        public static readonly int InputRadius = Shader.PropertyToID("_InputRadius");
    }

    private static class ShaderParam
    {
        public static readonly int Buf = Shader.PropertyToID("_Buf");
    }

    public class StepData
    {
        public bool isResize;
        public int width;
        public int height;
        public int randSeed;
        public bool isInputEnable;
        public Vector2 inputPos;
        public float deltaTime;
    }
    
    #endregion
    

    [FormerlySerializedAs("cs")] [Header("ComputeShader")]
    public ComputeShader computeShader;

    public float stepInterval;
    public float initialAliveRate = 0.2f;

    private float _interval;
    private GraphicsBuffer _writeBuffer;
    private GraphicsBuffer _readBuffer;

    public float inputRadius = 10f;

    
    [Header("Render")]
    public Shader shader;

    private Material _mat;

    
    [Header("Reproducibility")]
    // for inspector(and copy for reproducibility)
    public int width;
    public int height;
    public int seed;
    
    private void Start()
    {
        _mat = new Material(shader);
    }

    private void OnDestroy()
    {
        DestroyBuffers();
        if (_mat != null) Destroy(_mat);
    }

    private void DestroyBuffers()
    {
        _readBuffer?.Release();
        _writeBuffer?.Release();
        _readBuffer = null;
        _writeBuffer = null;
    }

    public void Step(StepData data)
    {
        if (data.isResize)
        {
            DoResize(data);
        }

        if (data.isInputEnable)
        {
            DoInput(data);
        }
        _interval -= data.deltaTime;

        if (_interval <= 0f)
        {
            DoStep();
            _interval = Mathf.Max(0f, _interval + stepInterval);
        }
    }

    private void DoResize(StepData data)
    {
        width = width <= 0 ? data.width : width;
        height = height <= 0 ? data.height : height;

        computeShader.SetInt(CommonParam.Width, width);
        computeShader.SetInt(CommonParam.Height, height);

        DestroyBuffers();

        seed = (seed <= 0) ? data.randSeed : seed;
        var rand = new Random(seed);
        var gridNum = width * height;
        
        _readBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, gridNum, Marshal.SizeOf(typeof(int)));
        _writeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, gridNum, Marshal.SizeOf(typeof(int)));

        var array = new NativeArray<int>(_readBuffer.count, Allocator.Temp);
        for (var i = 0; i < gridNum; ++i)
        {
            array[i] = (rand.NextDouble() < initialAliveRate) ? 1 : 0;
        }
        _readBuffer.SetData(array);
        array.Dispose();
    }

    private void DoInput(StepData data)
    {
        var kernel = computeShader.FindKernel(CsParam.KernelInput);
        computeShader.SetVector(CsParam.InputPos, data.inputPos);
        computeShader.SetFloat(CsParam.InputRadius, inputRadius);
        computeShader.SetBuffer(kernel, CsParam.WriteBuf, _readBuffer);

        Dispatch(computeShader, kernel, new Vector3(width, height, 1));
    }

    private void DoStep()
    {
        var kernel = computeShader.FindKernel(CsParam.KernelStep);

        computeShader.SetBuffer(kernel, CsParam.ReadBuf, _readBuffer);
        computeShader.SetBuffer(kernel, CsParam.WriteBuf, _writeBuffer);

        Dispatch(computeShader, kernel, new Vector3(width, height, 1));

        (_writeBuffer, _readBuffer) = (_readBuffer, _writeBuffer);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (_readBuffer == null) return;
        
        _mat.SetInt(CommonParam.Width, width);
        _mat.SetInt(CommonParam.Height, height);
        _mat.SetBuffer(ShaderParam.Buf, _readBuffer);
        Graphics.Blit(source, destination, _mat);
    }

    private static void Dispatch(ComputeShader cs, int kernel, Vector3 threadNum)
    {
        cs.GetKernelThreadGroupSizes(kernel, out var x, out var y, out var z);
        cs.Dispatch(kernel,
            Mathf.CeilToInt(threadNum.x / x),
            Mathf.CeilToInt(threadNum.y / y),
            Mathf.CeilToInt(threadNum.z / z)
        );
    }
}
