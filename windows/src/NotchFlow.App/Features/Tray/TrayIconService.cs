using System.Runtime.InteropServices;
using NotchFlow.Core.Logging;

namespace NotchFlow.App.Features.Tray;

/// <summary>Item do menu de contexto da bandeja.</summary>
public sealed record TrayMenuItem(string Text, Action Invoke)
{
    public bool IsChecked { get; init; }

    public bool IsSeparator { get; init; }

    public static TrayMenuItem Separator() =>
        new(string.Empty, () => { }) { IsSeparator = true };
}

/// <summary>
/// Ícone na bandeja do sistema, equivalente ao NSStatusItem da versão macOS.
///
/// O Shell_NotifyIcon precisa de uma janela para receber as notificações do mouse, e o
/// NotchFlow não tem janela principal. Por isso criamos uma janela apenas de mensagens,
/// que nunca aparece na tela.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const int WM_APP_TRAY = 0x0400 + 1;
    private const int WM_DESTROY = 0x0002;
    private const int WM_COMMAND = 0x0111;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint NIM_ADD = 0;
    private const uint NIM_MODIFY = 1;
    private const uint NIM_DELETE = 2;

    private const uint NIF_MESSAGE = 0x01;
    private const uint NIF_ICON = 0x02;
    private const uint NIF_TIP = 0x04;

    private const uint MF_STRING = 0x0000;
    private const uint MF_SEPARATOR = 0x0800;
    private const uint MF_CHECKED = 0x0008;

    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;

    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private readonly Func<IReadOnlyList<TrayMenuItem>> _menuFactory;
    private readonly Action _onPrimaryAction;
    private readonly WndProc _wndProc;
    private readonly string _className = "NotchFlowTray_" + Guid.NewGuid().ToString("N");

    private IntPtr _hwnd;
    private IntPtr _icon;
    private bool _visible;
    private bool _disposed;

    /// <param name="menuFactory">Chamado a cada abertura para o menu refletir o estado atual
    /// das preferências, em vez de guardar uma cópia que envelhece.</param>
    /// <param name="onPrimaryAction">Ação do clique com o botão esquerdo.</param>
    public TrayIconService(Func<IReadOnlyList<TrayMenuItem>> menuFactory, Action onPrimaryAction)
    {
        _menuFactory = menuFactory;
        _onPrimaryAction = onPrimaryAction;

        // O delegate precisa ser mantido vivo: o Win32 guarda só o ponteiro, e um
        // coletor de lixo no meio do caminho derrubaria o processo.
        _wndProc = HandleMessage;

        CreateMessageWindow();
        _icon = LoadApplicationIcon();
    }

    public void SetVisible(bool visible)
    {
        if (_disposed || visible == _visible || _hwnd == IntPtr.Zero)
        {
            return;
        }

        var data = CreateIconData();

        if (visible)
        {
            data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
            if (Shell_NotifyIcon(NIM_ADD, ref data))
            {
                _visible = true;
                AppLog.Lifecycle.Info("Ícone da bandeja exibido");
            }
            else
            {
                AppLog.Lifecycle.Error("Falha ao exibir o ícone da bandeja");
            }
        }
        else
        {
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _visible = false;
        }
    }

    private NOTIFYICONDATA CreateIconData() => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = 1,
        uCallbackMessage = WM_APP_TRAY,
        hIcon = _icon,
        szTip = "NotchFlow"
    };

    private void CreateMessageWindow()
    {
        var wndClass = new WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = _className
        };

        if (RegisterClass(ref wndClass) == 0)
        {
            AppLog.Lifecycle.Error("Falha ao registrar a classe da janela de mensagens");
            return;
        }

        _hwnd = CreateWindowEx(
            0, _className, string.Empty, 0, 0, 0, 0, 0,
            HWND_MESSAGE, IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            AppLog.Lifecycle.Error("Falha ao criar a janela de mensagens da bandeja");
        }
    }

    /// <summary>
    /// Carrega o ícone embutido no executável no tamanho exato da bandeja.
    ///
    /// LoadIcon devolveria sempre 32x32 e deixaria o sistema reduzir, o que borra o
    /// desenho. LoadImage pede a resolução certa e o .ico multi-resolução entrega o
    /// quadro nativo de 16x16.
    /// </summary>
    private static IntPtr LoadApplicationIcon()
    {
        const int applicationIconResourceId = 32512;
        const uint IMAGE_ICON = 1;
        const uint LR_SHARED = 0x8000;
        const int SM_CXSMICON = 49;
        const int SM_CYSMICON = 50;

        var module = GetModuleHandle(null);
        var width = GetSystemMetrics(SM_CXSMICON);
        var height = GetSystemMetrics(SM_CYSMICON);

        var icon = LoadImage(
            module, new IntPtr(applicationIconResourceId), IMAGE_ICON, width, height, LR_SHARED);

        return icon != IntPtr.Zero
            ? icon
            : LoadIcon(IntPtr.Zero, new IntPtr(applicationIconResourceId));
    }

    private IntPtr HandleMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WM_APP_TRAY:
                switch ((int)lParam)
                {
                    case WM_LBUTTONUP:
                        _onPrimaryAction();
                        return IntPtr.Zero;

                    case WM_RBUTTONUP:
                        ShowContextMenu();
                        return IntPtr.Zero;
                }

                break;

            case WM_DESTROY:
                PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var items = _menuFactory();
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.IsSeparator)
                {
                    AppendMenu(menu, MF_SEPARATOR, IntPtr.Zero, null);
                    continue;
                }

                var flags = MF_STRING | (item.IsChecked ? MF_CHECKED : 0);
                // O identificador é o índice mais um: zero significa "nada escolhido".
                AppendMenu(menu, flags, new IntPtr(i + 1), item.Text);
            }

            // Sem trazer a janela para frente o menu não fecha ao clicar fora dele.
            SetForegroundWindow(_hwnd);

            GetCursorPos(out var point);
            var chosen = TrackPopupMenuEx(
                menu, TPM_RIGHTBUTTON | TPM_RETURNCMD, point.X, point.Y, _hwnd, IntPtr.Zero);

            if (chosen > 0 && chosen <= items.Count)
            {
                items[chosen - 1].Invoke();
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SetVisible(false);

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        UnregisterClass(_className, GetModuleHandle(null));
    }

    // ---------- Interop ----------

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int width, int height,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadImage(
        IntPtr hInst, IntPtr name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(
        IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);
}
