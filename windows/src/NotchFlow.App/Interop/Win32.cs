using System.Runtime.InteropServices;

namespace NotchFlow.App.Interop;

/// <summary>
/// Interop com o Win32 para o que o AppWindow do WinAppSDK não expõe: janela não-ativante,
/// topmost confiável, região de recorte e posição global do ponteiro.
/// </summary>
internal static class Win32
{
    internal const int GWL_EXSTYLE = -20;

    internal const long WS_EX_TOPMOST = 0x00000008L;
    internal const long WS_EX_TOOLWINDOW = 0x00000080L;
    internal const long WS_EX_NOACTIVATE = 0x08000000L;

    internal static readonly IntPtr HWND_TOPMOST = new(-1);

    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    internal const int RGN_OR = 2;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    /// <summary>
    /// Distância entre a borda da janela e a área cliente, e o tamanho desta.
    ///
    /// Mesmo com a faixa de título removida, uma janela do WinAppSDK mantém 3 pixels de área
    /// não-cliente em volta, e o sistema os pinta de claro. Esses pixels ficam por cima do
    /// conteúdo XAML, então a ilha precisa ser posicionada descontando o recuo, para a borda
    /// clara cair fora da tela em vez de aparecer como um risco no topo.
    /// </summary>
    internal static (int OffsetX, int OffsetY, int ClientWidth, int ClientHeight) GetClientMetrics(IntPtr hwnd)
    {
        if (!GetWindowRect(hwnd, out var windowRect) || !GetClientRect(hwnd, out var clientRect))
        {
            return (0, 0, 0, 0);
        }

        var origin = new POINT();
        if (!ClientToScreen(hwnd, ref origin))
        {
            return (0, 0, clientRect.Right - clientRect.Left, clientRect.Bottom - clientRect.Top);
        }

        return (
            origin.X - windowRect.Left,
            origin.Y - windowRect.Top,
            clientRect.Right - clientRect.Left,
            clientRect.Bottom - clientRect.Top);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    internal static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateRoundRectRgn(
        int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32.dll")]
    internal static extern int CombineRgn(IntPtr hrgnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int fnCombineMode);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(IntPtr hObject);

    private const int DWMWA_NCRENDERING_POLICY = 2;
    private const int DWMNCRP_DISABLED = 1;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;

    /// <summary>Valor sentinela do DWM que remove a borda em vez de colori-la.</summary>
    private const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

    private const int DWMWCP_DONOTROUND = 1;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref uint value, int size);

    /// <summary>
    /// Remove os enfeites que o Windows 11 aplica a qualquer janela: a borda de 1 pixel e o
    /// arredondamento automático dos cantos. Sem isto, a borda aparece como um risco claro
    /// na aresta superior da ilha, e o arredondamento do sistema briga com o nosso recorte.
    /// </summary>
    internal static void RemoveSystemChrome(IntPtr hwnd)
    {
        // O DWM continua desenhando a moldura por cima do conteúdo enquanto a renderização
        // não-cliente estiver ativa. É ela que produz o risco claro na aresta superior.
        var disabled = (uint)DWMNCRP_DISABLED;
        DwmSetWindowAttribute(hwnd, DWMWA_NCRENDERING_POLICY, ref disabled, sizeof(uint));

        var noBorder = DWMWA_COLOR_NONE;
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref noBorder, sizeof(uint));

        var doNotRound = (uint)DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref doNotRound, sizeof(uint));
    }

    /// <summary>
    /// Aplica os estilos estendidos da ilha. WS_EX_NOACTIVATE impede que clicar na ilha
    /// roube o foco do aplicativo em uso, e WS_EX_TOOLWINDOW a mantém fora do Alt+Tab.
    /// </summary>
    internal static void ApplyIslandStyles(IntPtr hwnd)
    {
        var current = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        var desired = current | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(desired));
    }

    /// <summary>
    /// Reafirma o topmost. WS_EX_TOPMOST é ignorado quando aplicado por SetWindowLongPtr:
    /// só SetWindowPos com HWND_TOPMOST realmente promove a janela.
    /// </summary>
    internal static void BringToTop(IntPtr hwnd)
        => SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

    internal static double GetScaleFactor(IntPtr hwnd)
    {
        var dpi = GetDpiForWindow(hwnd);
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    /// <summary>
    /// Recorta a janela para a silhueta da ilha: cantos de cima retos e os de baixo
    /// arredondados, como o UnevenRoundedRectangle da versão macOS.
    ///
    /// Sem isso a janela de 520x224 continuaria capturando cliques em toda a faixa superior
    /// da tela, mesmo onde é invisível. Com o recorte, tudo fora da ilha deixa de existir
    /// para o sistema e os cliques chegam ao aplicativo de baixo.
    /// </summary>
    internal static void ApplyIslandRegion(
        IntPtr hwnd,
        double islandWidthDips,
        double islandHeightDips,
        double cornerRadiusDips)
    {
        var scale = GetScaleFactor(hwnd);
        var (offsetX, offsetY, clientWidth, _) = GetClientMetrics(hwnd);

        var width = (int)Math.Round(islandWidthDips * scale);
        var height = (int)Math.Round(islandHeightDips * scale);
        var radius = (int)Math.Round(cornerRadiusDips * scale);

        if (width <= 0 || height <= 0)
        {
            return;
        }

        // A região usa coordenadas da janela, então precisa somar o recuo da área cliente.
        var left = offsetX + (clientWidth - width) / 2;
        var right = left + width;
        var top = offsetY;
        var bottom = top + height;

        IntPtr region;
        if (radius <= 0)
        {
            region = CreateRectRgn(left, top, right, bottom);
        }
        else
        {
            // O arredondamento vale só embaixo: a ilha encosta no topo da tela.
            var rounded = CreateRoundRectRgn(left, top, right, bottom + 1, radius * 2, radius * 2);
            var square = CreateRectRgn(left, top, right, top + Math.Min(radius, height));
            region = CreateRectRgn(0, 0, 1, 1);
            CombineRgn(region, rounded, square, RGN_OR);
            DeleteObject(rounded);
            DeleteObject(square);
        }

        // O sistema assume a posse da região; não se deve liberá-la depois desta chamada.
        SetWindowRgn(hwnd, region, true);
    }
}
