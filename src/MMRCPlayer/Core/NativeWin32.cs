using System.Runtime.InteropServices;

namespace MMRCPlayer.Core;

public static partial class NativeWin32
{
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_SIZE = 0x0005;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_CLOSE = 0x0010;
    
    public const int WS_POPUP = unchecked((int)0x80000000);
    public const int WS_VISIBLE = 0x10000000;
    public const int WS_EX_TOPMOST = 0x00000008;
    
    public const int SWP_FRAMECHANGED = 0x0020;
    public const int SWP_NOMOVE = 0x0002;
    public const int SWP_NOSIZE = 0x0001;
    public const int SWP_SHOWWINDOW = 0x0040;
    
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    
    public const int VK_F11 = 0x7A;
    public const int VK_ESCAPE = 0x1B;
    
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial IntPtr CreateWindowExW(
        int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
    
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(IntPtr hWnd);
    
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);
    
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateWindow(IntPtr hWnd);
    
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
    
    [LibraryImport("user32.dll")]
    public static partial int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);
    
    [LibraryImport("user32.dll")]
    public static partial int GetWindowLongW(IntPtr hWnd, int nIndex);
    
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(IntPtr hWnd);
    
    [LibraryImport("user32.dll")]
    public static partial IntPtr DefWindowProcW(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
    
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PeekMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
    
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(ref MSG lpMsg);
    
    [LibraryImport("user32.dll")]
    public static partial IntPtr DispatchMessageW(ref MSG lpMsg);
    
    [LibraryImport("user32.dll")]
    public static partial IntPtr PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    [LibraryImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string? lpModuleName);
    
    [LibraryImport("user32.dll")]
    public static partial short VkKeyScanW(char ch);
    
    [LibraryImport("user32.dll")]
    public static partial IntPtr LoadCursorW(IntPtr hInstance, IntPtr lpCursorName);
    
    [LibraryImport("user32.dll")]
    public static partial void PostQuitMessage(int nExitCode);
    
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct WNDCLASSEXW
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
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }
    
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.U2)]
    public static partial ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);
    
    public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate IntPtr WndProcNative(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
