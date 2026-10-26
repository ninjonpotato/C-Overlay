using System;
using System.Numerics;
using System.Runtime.InteropServices;

using System.Threading;

using System.Threading.Tasks;

using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

using System.Windows.Media;

using System.Windows.Media.Imaging;

using System.Windows.Threading;



using Vortice.Direct3D;

using Vortice.Direct3D11;

using Vortice.DXGI;

using Vortice.Mathematics;



using static Vortice.Direct3D11.D3D11;



namespace screengrabber_wpf

{

    public partial class MainWindow : Window

    {

        [DllImport("user32.dll", SetLastError = true)]

        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;



        [DllImport("user32.dll", SetLastError = true)]

        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);



        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]

        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);



        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]

        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);



        [DllImport("user32.dll", SetLastError = true)]

        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);


        private const int GWL_EXSTYLE = -20;

        private const int WS_EX_TRANSPARENT = 0x00000020;

        private const int WS_EX_LAYERED = 0x00080000;


        private const uint SWP_NOSIZE = 0x0001;

        private const uint SWP_NOMOVE = 0x0002;

        private const uint SWP_NOZORDER = 0x0004;

        private const uint SWP_FRAMECHANGED = 0x0020;

        private const uint SWP_NOACTIVATE = 0x0010;


        //Kép megjelenítésért
        private Direct3D11_helper? _d3d;
        private NativeOverlay? _nativeOverlay;

        private readonly DispatcherTimer _shaderTimer = new();
        private readonly Random _random = new();

        public MainWindow()

        {

            InitializeComponent();



            this.IsHitTestVisible = false;

            this.Closed += MainWindow_Closed;

            _shaderTimer.Interval = TimeSpan.FromSeconds(1);
            _shaderTimer.Tick += Clock;
            _shaderTimer.Start();

        }
     


        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            style |= WS_EX_TRANSPARENT;
            style &= ~WS_EX_LAYERED;

            SetWindowLong(hwnd, GWL_EXSTYLE, style);

            SetWindowPos(
                hwnd,
                IntPtr.Zero,
                0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER |
                SWP_NOACTIVATE | SWP_FRAMECHANGED
            );

            bool success = SetWindowDisplayAffinity(
                hwnd,0x00
               // WDA_EXCLUDEFROMCAPTURE
            );

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(HwndHook);

        }

       private void Clock(object? sender, EventArgs e)
        {
            int index = _random.Next(ShaderCompiler.Shaders.Length);

            ShaderCompiler.SelectedShader =
                (ShaderCompiler.ShaderNames)index;
            _d3d.setShader();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.A) {
             //   ShaderCompiler.SelectedShader = ShaderCompiler.ShaderNames.DVD;
            }
            if (e.Key == Key.S)
            {
                ShaderCompiler.SelectedShader = ShaderCompiler.ShaderNames.INVERT;
            }
            if (e.Key == Key.D)
            {
                ShaderCompiler.SelectedShader = ShaderCompiler.ShaderNames.BW;
            }

            _d3d.setShader();

        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _d3d = new Direct3D11_helper();

            // 1. Direct3D és képernyőrögzítés inicializálása.
            _d3d.InitializeCapture();

            // 2. Overlay létrehozása a tényleges monitor méretével.
            _nativeOverlay = new NativeOverlay(
                0,
                0,
                _d3d.Width,
                _d3d.Height);

            // 3. HWND-k átadása a Direct3D-osztálynak.
            IntPtr previewHwnd =
                new System.Windows.Interop.WindowInteropHelper(this).Handle;

            _d3d.InitializeRenderTargets(
                _nativeOverlay.Handle,
                previewHwnd);

            // 4. Háttérben indul a képernyőrögzítés.
            _d3d.Start();
        }



        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam,IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;

            if (msg == WM_NCHITTEST)
            {
                handled = true;
                return new IntPtr(HTTRANSPARENT);
            }

            return IntPtr.Zero;
        }


  
      

        private const int _previewWidth = 1920;
        private const int _previewHeight = 1080;




        private void MainWindow_Closed(
     object? sender,
     EventArgs e)
        {
            _d3d?.Dispose();
            _nativeOverlay?.Dispose();
        }

    }

}