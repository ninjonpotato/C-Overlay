using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media.Effects;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;

namespace screengrabber_wpf
{


    internal class Direct3D11_helper : IDisposable
    {
        private IDXGIFactory1? _factory;
        private ID3D11Device? _device;
        private ID3D11DeviceContext? _context;
        private IDXGIOutputDuplication? _duplication;
        private ID3D11ShaderResourceView? _screenTextureView;
        private ID3D11Texture2D? _gpuTexture;
        private IDXGISwapChain1? _previewSwapChain;
        private ID3D11RenderTargetView? _previewRenderTargetView; //Preview a main window számára van
        private IDXGISwapChain1? _swapChain;
        private ID3D11RenderTargetView? _renderTargetView;
        private ID3D11VertexShader? _vertexShader;
        private ID3D11PixelShader? _pixelShader;

        private int _width;
        private int _height;

        private IntPtr _overlayHwnd;
        private IntPtr _previewHwnd;

        private const int PreviewWidth = 1920;
        private const int PreviewHeight = 1080;

        public int Width => _width;
        public int Height => _height;

        public void Initialize( IntPtr overlayHwnd,IntPtr previewHwnd)
        {
            _overlayHwnd = overlayHwnd;
            _previewHwnd = previewHwnd;

            InitCapture();

            InitSwapChain();
            InitRenderTarget();

            InitPreviewSwapChain();

          
        }
        private CancellationTokenSource? _cts;
        private Task? _captureTask;


        private ID3D11Buffer? _positionBuffer;

    

        private readonly object _offsetLock = new();

        [StructLayout(LayoutKind.Sequential)]
        private struct PositionConstants
        {
            public Vector2 Offset;
            public Vector2 Padding;
        }

        private readonly Stopwatch _stopwatch = new();
        private long _lastTimestamp;
        private long _frameStartTicks;

       

        private readonly long _targetTicksPerFrame =
            Stopwatch.Frequency / 60;


        public void Start()
        {

            if (_captureTask != null)
                throw new InvalidOperationException(
                    "A képernyőrögzítés már elindult.");

            _cts = new CancellationTokenSource();
            _stopwatch.Start();
            _captureTask = Task.Run(
                () => CaptureLoop(_cts.Token));
        }

        public void InitializeCapture()
        {
            // Direct3D eszköz, shader, monitorrögzítés,
            // GPU-textúra és shader resource view.
            InitCapture();
        }



        public void InitializeRenderTargets( //hova fogjuk vetíteni a képet
            IntPtr overlayHwnd,
            IntPtr previewHwnd)
        {
            _overlayHwnd = overlayHwnd;
            _previewHwnd = previewHwnd;

            InitSwapChain();
            InitRenderTarget();
            InitPreviewSwapChain();
        }

        private void InitCapture()
        {
            ////Rögzítésnek előkészítése////

            _factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

            _factory.EnumAdapters(0, out var adapter);

            adapter.EnumOutputs(0, out var output);

            var output1 = output.QueryInterface<IDXGIOutput1>();


            D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.BgraSupport, null, out _device, out _context);

            var bufferDesc = new BufferDescription  //buffer a shader változókhoz
            {
                ByteWidth = 16,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ConstantBuffer,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None,
                StructureByteStride = 0
            };
            setShader();
            _positionBuffer = _device.CreateBuffer(bufferDesc);


            //Tényleges rögzítés
            var bounds = output.Description.DesktopCoordinates;
            _width = bounds.Right - bounds.Left;
            _height = bounds.Bottom - bounds.Top;
            _duplication = output1.DuplicateOutput(_device);


            //gpu textúra
            var gpuDesc = new Texture2DDescription
            {
                Width = (uint)_width,
                Height = (uint)_height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };

            _gpuTexture = _device.CreateTexture2D(gpuDesc);
            _screenTextureView = _device.CreateShaderResourceView(_gpuTexture);


            output1.Dispose();

            output.Dispose();

            adapter.Dispose();

        }


        private void InitSwapChain()
        {
            if (_device == null || _overlayHwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "A Direct3D eszköz vagy az overlay HWND hiányzik.");
            }

            using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
            using var adapter = dxgiDevice.GetAdapter();
            using var factory = adapter.GetParent<IDXGIFactory2>();

            var description = new SwapChainDescription1
            {
                Width = (uint)_width,
                Height = (uint)_height,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = AlphaMode.Ignore
            };

            _swapChain = factory.CreateSwapChainForHwnd(
                _device,
                _overlayHwnd,
                description);
        }

        private void InitRenderTarget()
        {
            if (_swapChain == null || _device == null)
                throw new InvalidOperationException(
                    "A swap chain vagy a Direct3D eszköz még nem létezik.");

            using var backBuffer =
                _swapChain.GetBuffer<ID3D11Texture2D>(0);

            _renderTargetView =
                _device.CreateRenderTargetView(backBuffer);
        }

        private void InitPreviewSwapChain()
        {
            if (_device == null || _previewHwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "A Direct3D eszköz vagy a preview HWND hiányzik.");
            }


            using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
            using var adapter = dxgiDevice.GetAdapter();
            using var factory = adapter.GetParent<IDXGIFactory2>();



            var description = new SwapChainDescription1
            {
                Width = (uint)_width,
                Height = (uint)_height,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = AlphaMode.Ignore
            };

            _previewSwapChain = factory.CreateSwapChainForHwnd(
            _device,
            _previewHwnd,
            description);

            using var backBuffer =
                _previewSwapChain.GetBuffer<ID3D11Texture2D>(0);

            _previewRenderTargetView =
                _device.CreateRenderTargetView(backBuffer);
        }
        public void setShader()
        {
            byte[] vertexBytecode = ShaderCompiler.CompileVertexShader();
            byte[] pixelBytecode = ShaderCompiler.CompilePixelShader();

            try
            {
                var newVertexShader = _device.CreateVertexShader(vertexBytecode);
                var newPixelShader = _device.CreatePixelShader(pixelBytecode);

                // Régi shaderek eltárolása
                var oldVertexShader = _vertexShader;
                var oldPixelShader = _pixelShader;

                // Új shaderek beállítása
                _vertexShader = newVertexShader;
                _pixelShader = newPixelShader;

                // Régi shaderek felszabadítása
                oldVertexShader?.Dispose();
                oldPixelShader?.Dispose();
            }
            catch (SharpGen.Runtime.SharpGenException ex)
            {
                Debug.WriteLine($"Direct3D hiba: {ex.Message}");

                if (_device != null)
                {
                    Debug.WriteLine(
                        $"GPU Device Removed Reason: {_device.DeviceRemovedReason}"
                    );
                }
            }
        }
        private void RenderdFrame(ID3D11ShaderResourceView textureView) //ez a fő ciklusun, mint az Update()
        {
           if (_context == null ||
            _positionBuffer == null)
            return;

            PositionConstants constants;
         
            lock (_offsetLock)
            {

                  constants = new PositionConstants // a változók átadása
                  {
                      Offset = VisualEffektek.Offset,
                      Padding = Vector2.Zero
                  };
                
            }

            _context.UpdateSubresource(in constants, _positionBuffer);//átadjuk a Shadernek a változókat
            _context.PSSetConstantBuffer(0, _positionBuffer);

            _context.VSSetShader(_vertexShader); //beállítjuk a shadereket
            _context.PSSetShader(_pixelShader);
            _context.PSSetShaderResource(0, textureView);
            _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            // 1. Teljes képernyős overlay
            if (_swapChain != null && _renderTargetView != null)
            {
                _context.OMSetRenderTargets(_renderTargetView);
                _context.RSSetViewport(new Viewport(0, 0, _width, _height));
                _context.Draw(3, 0);
                _swapChain.Present(1, PresentFlags.None);
            }

            // 2. OBS számára látható előnézeti ablak
            if (_previewSwapChain != null &&
                _previewRenderTargetView != null)
            {

                _context.OMSetRenderTargets(_previewRenderTargetView);

                _context.RSSetViewport(new Viewport(
                    0, 0,
                    PreviewWidth,
                    PreviewHeight));

                _context.Draw(3, 0);
                _previewSwapChain.Present(0, PresentFlags.None);
            }

            _context.PSSetShaderResource(0, null);
        }

        private void CaptureDesktopFrame() {
            if (_duplication != null && _context != null && _gpuTexture != null)
            {
                var result = _duplication.AcquireNextFrame(1, out _, out IDXGIResource desktopResource);
                if (result.Success)
                {
                    try
                    {
                        using var desktopTexture = desktopResource.QueryInterface<ID3D11Texture2D>();
                        _context.CopyResource(_gpuTexture, desktopTexture);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"GPU másolási hiba: {ex}");
                    }
                    finally
                    {
                        desktopResource.Dispose();
                        try { _duplication.ReleaseFrame(); } catch { }
                    }
                }
            }
        }

        private void CaptureLoop(CancellationToken token)
        {
             _stopwatch.Restart();
             _lastTimestamp = _stopwatch.ElapsedTicks;

            // Cél FPS (pl. 60 FPS = ~16.6 ms / frame)
            long targetTicksPerFrame = System.Diagnostics.Stopwatch.Frequency / 60;

            while (!token.IsCancellationRequested)
            {
                StartFrame();
                float deltaTime = Delta();
                // 1. Megpróbáljuk elkapni a legújabb desktop képkockát (nem baj, ha timeoutol)
                CaptureDesktopFrame();

                lock (_offsetLock) //itt frissítjük a sahder változókat
                {
                    VisualEffektek.DvdLogo(deltaTime, new Vector2(Width, Height));
                }

                // 4. MINDIG renderelünk, függetlenül attól, hogy változott-e az asztal!
                if (_screenTextureView != null)
                {
                    RenderdFrame(_screenTextureView);
                }

                LimitFPS();
               
            }
        }

        private void StartFrame() //képkocka kezdetén hivjuk
        {
            _frameStartTicks = _stopwatch.ElapsedTicks;
        }

        public float Delta() //eltelt idő
        {
            long currentTicks = _stopwatch.ElapsedTicks;

            float deltaTime =
                (float)(currentTicks - _lastTimestamp)
                / Stopwatch.Frequency;

            _lastTimestamp = currentTicks;

            if (deltaTime > 0.1f)
                deltaTime = 0.016f;

            return deltaTime;
        }

        private void LimitFPS() //képkocka végén
        {
            long elapsedFrameTicks =
                _stopwatch.ElapsedTicks - _frameStartTicks;

            long remainingTicks =
                _targetTicksPerFrame - elapsedFrameTicks;

            if (remainingTicks > 0)
            {
                int sleepMs = (int)(
                    remainingTicks * 1000 / Stopwatch.Frequency);

                if (sleepMs > 1)
                    Thread.Sleep(sleepMs - 1);
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();

            try
            {
                _captureTask?.Wait(1000);
            }
            catch (AggregateException)
            {
                // A háttérfeladat hibával fejeződhetett be.
            }

            _cts?.Dispose();
            _cts = null;
            _captureTask = null;

            _context?.ClearState();

            _previewRenderTargetView?.Dispose();
            _previewSwapChain?.Dispose();

            _renderTargetView?.Dispose();
            _swapChain?.Dispose();

            _screenTextureView?.Dispose();
            _gpuTexture?.Dispose();

            _vertexShader?.Dispose();
            _pixelShader?.Dispose();

            _duplication?.Dispose();
            _context?.Dispose();
            _device?.Dispose();
            _factory?.Dispose();

            _previewRenderTargetView = null;
            _previewSwapChain = null;
            _renderTargetView = null;
            _swapChain = null;

            _screenTextureView = null;
            _gpuTexture = null;

            _vertexShader = null;
            _pixelShader = null;

            _duplication = null;
            _context = null;
            _device = null;
            _factory = null;
        }
    }
}
