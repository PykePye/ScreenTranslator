using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ScreenTranslator.Hotkey;

public sealed class HotkeyManager : IDisposable
{
    // Modifier flags cho RegisterHotKey
    [Flags]
    public enum Modifiers : uint
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
        NoRepeat = 0x4000  // Không trigger lặp khi giữ phím
    }

    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 9000;  // ID tự chọn, miễn unique trong app

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly IntPtr _windowHandle;
    private bool _isRegistered;

    public event EventHandler? HotkeyPressed;

    public HotkeyManager()
    {
        // Tạo message-only window ẩn để nhận WM_HOTKEY
        var parameters = new HwndSourceParameters("ScreenTranslatorHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            ParentWindow = new IntPtr(-3)  // HWND_MESSAGE: message-only window
        };

        _source = new HwndSource(parameters);
        _windowHandle = _source.Handle;
        _source.AddHook(WndProc);
    }

    /// <summary>
    /// Đăng ký hotkey. Trả về true nếu thành công, false nếu bị app khác chiếm.
    /// </summary>
    public bool Register(Modifiers modifiers, uint virtualKey)
    {
        if (_isRegistered)
            UnregisterHotKey(_windowHandle, HotkeyId);

        var flags = (uint)(modifiers | Modifiers.NoRepeat);
        _isRegistered = RegisterHotKey(_windowHandle, HotkeyId, flags, virtualKey);
        return _isRegistered;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_isRegistered)
            UnregisterHotKey(_windowHandle, HotkeyId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}