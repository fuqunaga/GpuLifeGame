using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;


[RequireComponent(typeof(Camera))]
public class LifeGame : MonoBehaviour
{
    public static class COMMONPARAM
    {
        public const string WIDTH = "_Width";
        public const string HEIGHT = "_Height";
    }

    public static class CSPARAM
    {
        public const string KERNEL_STEP = "Step";
        public const string WRITE_BUF = "_WriteBuf";
        public const string READ_BUF = "_ReadBuf";

        public const string KERNEL_INPUT = "Input";
        public const string INPUT_POS = "_InputPos";
        public const string INPUT_RADIUS = "_InputRadius";
    }

    public static class SHADERPARAM
    {
        public const string BUF = "_Buf";
    }

    public struct Data
    {
        public int alive;
    }

    [Header("CS")]
    public ComputeShader _cs;

    public int LOD = 1;
    int _width => Screen.width >> LOD;
    int _height => Screen.height >> LOD;

    public float _stepInterval = 0.1f;
    public float _initialAliveRate = 0.2f;

    float _interval = 0f;
    ComputeBuffer _writeBufs;
    ComputeBuffer _readBufs;

    public float _InputRadius = 10f;

    [Header("Render")]
    public Shader _shader;
    Material _mat;

    void Start()
    {
        _cs.SetInt(COMMONPARAM.WIDTH, _width);
        _cs.SetInt(COMMONPARAM.HEIGHT, _height);

        var gridNum = _width * _height;
        var data = Enumerable.Range(0, gridNum).Select(_ => new Data() { alive = (Random.value < _initialAliveRate) ? 1 : 0 }).ToArray();

        var bufs = Enumerable.Range(0, 2).Select(_ =>
        {
            var cs = new ComputeBuffer(gridNum, Marshal.SizeOf(typeof(Data)));
            cs.SetData(data);
            return cs;
        }).ToArray();


        _readBufs = bufs[0];
        _writeBufs = bufs[1];

        _mat = new Material(_shader);
    }

    private void OnDestroy()
    {
        if (_readBufs != null) _readBufs.Release();
        if (_writeBufs != null) _writeBufs.Release();
        if (_mat != null) Destroy(_mat);
    }

    void Update()
    {
        DoInput();

        _interval -= Time.deltaTime;

        if (_interval <= 0f)
        {
            Step();
            _interval = Mathf.Max(0f, _interval + _stepInterval);
        }
    }

    void DoInput()
    {
        if (Input.GetMouseButton(0))
        {
            var pos = Input.mousePosition / Mathf.Pow(2f, LOD);

            var kernel = _cs.FindKernel(CSPARAM.KERNEL_INPUT);
            _cs.SetVector(CSPARAM.INPUT_POS, pos);
            _cs.SetFloat(CSPARAM.INPUT_RADIUS, _InputRadius);
            _cs.SetBuffer(kernel, CSPARAM.WRITE_BUF, _readBufs);

            Dispatch(_cs, kernel, new Vector3(_width, _height, 1));
        }
    }

    void Step()
    {
        var kernel = _cs.FindKernel(CSPARAM.KERNEL_STEP);

        _cs.SetBuffer(kernel, CSPARAM.READ_BUF, _readBufs);
        _cs.SetBuffer(kernel, CSPARAM.WRITE_BUF, _writeBufs);

        Dispatch(_cs, kernel, new Vector3(_width, _height, 1));

        SwapBuf();
    }

    void SwapBuf()
    {
        var tmp = _readBufs;
        _readBufs = _writeBufs;
        _writeBufs = tmp;
    }

    public static void Dispatch(ComputeShader cs, int kernel, Vector3 threadNum)
    {
        uint x, y, z;
        cs.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
        cs.Dispatch(kernel, Mathf.CeilToInt(threadNum.x / x), Mathf.CeilToInt(threadNum.y / y), Mathf.CeilToInt(threadNum.z / z));
    }


    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        _mat.SetInt(COMMONPARAM.WIDTH, _width);
        _mat.SetInt(COMMONPARAM.HEIGHT, _height);
        _mat.SetBuffer(SHADERPARAM.BUF, _readBufs);
        Graphics.Blit(source, destination, _mat);
    }
}
