
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace screengrabber_wpf
{
    public partial class OverlayWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(
            IntPtr hwnd, uint affinity);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;


        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;

        private IntPtr OverlayHwndHook(
            IntPtr hwnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                handled = true;
                return new IntPtr(HTTRANSPARENT);
            }

            return IntPtr.Zero;
        }

        public OverlayWindow()
        {
            InitializeComponent();

            Loaded += OverlayWindow_Loaded;

            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                    Application.Current.Shutdown();
            };
        }

        private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

           /* var source = HwndSource.FromHwnd(hwnd);*
            source?.AddHook(OverlayHwndHook);*/

            int style = GetWindowLong(hwnd, GWL_EXSTYLE);

            style |= WS_EX_TRANSPARENT
 
                   | WS_EX_TOOLWINDOW
                   | WS_EX_NOACTIVATE;
            style -= WS_EX_LAYERED;

            SetWindowLong(hwnd, GWL_EXSTYLE, style);


            bool success = SetWindowDisplayAffinity(
     hwnd, WDA_EXCLUDEFROMCAPTURE);

            int error = Marshal.GetLastWin32Error();

            int actualStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            System.Diagnostics.Debug.WriteLine(
                $"Overlay affinity: {success}, hiba: {error}, " +
                $"stílus: 0x{actualStyle:X8}, " +
                $"Layered: {(actualStyle & WS_EX_LAYERED) != 0}");
        }

        public void SetFrame(WriteableBitmap bitmap)
        {
            OverlayImage.Source = bitmap;
        }
    }
}
