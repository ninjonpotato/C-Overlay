
using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;

namespace screengrabber_wpf
{
    public sealed class NativeOverlay : IDisposable
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private const uint WS_POPUP = 0x80000000;
        private const uint WS_VISIBLE = 0x10000000;

        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
        private const uint LWA_ALPHA = 0x00000002;

        private const uint WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;

        private const uint ERROR_CLASS_ALREADY_EXISTS = 1410;

        private const string ClassName = "GpuInvertNativeOverlay";

        private IntPtr _hwnd;
        private bool _disposed;

        public IntPtr Handle => _hwnd;

        private delegate IntPtr WndProcDelegate(
            IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        // A delegáltat életben kell tartani az ablak fennállásáig.
        private static readonly WndProcDelegate WindowProcedure =
            WndProc;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? name);

        [DllImport("user32.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern ushort RegisterClassEx(
            ref WNDCLASSEX windowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int exStyle,
            string className,
            string windowName,
            uint style,
            int x, int y, int width, int height,
            IntPtr parent,
            IntPtr menu,
            IntPtr instance,
            IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hwnd, IntPtr insertAfter,
            int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(
            IntPtr hwnd, uint affinity);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(
            IntPtr hwnd, uint colorKey, byte alpha, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProc(
            IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static IntPtr WndProc(
            IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_NCHITTEST)
                return new IntPtr(HTTRANSPARENT);

            return DefWindowProc(hwnd, msg, wParam, lParam);
        }

        private const uint SWP_NOSIZE = 0x0001;

        private const uint SWP_NOZORDER = 0x0004;

        public void Mozgatas(Vector2 offset) {
            SetWindowPos(
          Handle,
          IntPtr.Zero,
          (int)offset.X, (int)offset.Y,
          0, 0,
          SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE
  );
        }

        public NativeOverlay(int x, int y, int width, int height)
        {
            IntPtr instance = GetModuleHandle(null);

            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(
                    WindowProcedure),
                hInstance = instance,
                lpszClassName = ClassName
            };

            if (RegisterClassEx(ref wc) == 0)
            {
                int error = Marshal.GetLastWin32Error();

                if ((uint)error != ERROR_CLASS_ALREADY_EXISTS)
                    throw new Win32Exception(error);
            }

            int exStyle =
                WS_EX_LAYERED |
                WS_EX_TRANSPARENT |
                WS_EX_TOOLWINDOW |
                WS_EX_NOACTIVATE;

            _hwnd = CreateWindowEx(
                exStyle,
                ClassName,
                "GPU Invert Overlay",
                WS_POPUP | WS_VISIBLE,
                x, y, width, height,
                IntPtr.Zero,
                IntPtr.Zero,
                instance,
                IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (!SetLayeredWindowAttributes(
                _hwnd, 0, 255, LWA_ALPHA))
            {
                int error = Marshal.GetLastWin32Error();
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
                throw new Win32Exception(error);
            }

            if (!SetWindowPos(
                _hwnd, new IntPtr(-1),
                x, y, width, height,
                SWP_SHOWWINDOW | SWP_NOACTIVATE))
            {
                int error = Marshal.GetLastWin32Error();
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
                throw new Win32Exception(error);
            }

                 bool affinity = SetWindowDisplayAffinity(
                      _hwnd, WDA_EXCLUDEFROMCAPTURE);

            //    bool affinity = SetWindowDisplayAffinity(_hwnd, 0);
       //     SetWindowDisplayAffinity(_hwnd, WDA_EXCLUDEFROMCAPTURE);
      /*      System.Diagnostics.Debug.WriteLine(
                $"Native overlay HWND: 0x{_hwnd.ToInt64():X}");
            System.Diagnostics.Debug.WriteLine(
                $"Kizárás a felvételből: {affinity}, " +
                $"hiba: {(affinity ? 0 : Marshal.GetLastWin32Error())}");*/
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }
    }
}
