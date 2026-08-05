using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using NotchFlow.App.Interop;
using NotchFlow.Core.Logging;
using NotchFlow.Core.Media;
using NotchFlow.Core.Models;
using NotchFlow.Core.Settings;
using Windows.Graphics;

namespace NotchFlow.App.Features.Notch;

/// <summary>
/// Cria, posiciona e remove uma ilha por monitor. Equivalente ao NotchWindowController da
/// versão macOS, incluindo o rastreamento do ponteiro por consulta global.
/// </summary>
public sealed class NotchWindowController : IDisposable
{
    /// <summary>Mesma cadência do timer usado na versão macOS.</summary>
    private static readonly TimeSpan PointerInterval = TimeSpan.FromMilliseconds(60);

    /// <summary>A cada N ciclos do ponteiro conferimos se os monitores mudaram. Evita
    /// depender de WM_DISPLAYCHANGE, que exigiria uma janela de mensagens só para isso.</summary>
    private const int DisplaySyncEveryTicks = 33;

    private sealed class DisplayContext
    {
        public required NotchWindow Window { get; init; }
        public required NotchViewModel ViewModel { get; init; }
        public required string Key { get; set; }
        public RectInt32 Bounds { get; set; }
        public bool PointerInside { get; set; }
        public DateTimeOffset? PendingSince { get; set; }
        public bool PendingTarget { get; set; }
    }

    private readonly MediaCoordinator _media;
    private readonly AppSettings _settings;
    private readonly DispatcherQueueTimer _pointerTimer;
    private readonly List<DisplayContext> _contexts = [];

    private int _tickCounter;
    private bool _disposed;

    public NotchWindowController(MediaCoordinator media, AppSettings settings, DispatcherQueue queue)
    {
        _media = media;
        _settings = settings;

        _pointerTimer = queue.CreateTimer();
        _pointerTimer.Interval = PointerInterval;
        _pointerTimer.Tick += OnPointerTick;
    }

    public void Start()
    {
        SynchronizeDisplays();
        _pointerTimer.Start();
        AppLog.Window.Info("Rastreamento do ponteiro iniciado");
    }

    public void Stop()
    {
        _pointerTimer.Stop();

        foreach (var context in _contexts)
        {
            context.Window.Teardown();
            context.Window.Close();
        }

        _contexts.Clear();
    }

    /// <summary>Reaplica preferências que mudam o tamanho ou a presença das ilhas.</summary>
    public void ApplySettings()
    {
        foreach (var context in _contexts)
        {
            context.ViewModel.ApplySettings();
        }

        SynchronizeDisplays();
    }

    public void ToggleUnderPointer()
    {
        var context = ContextUnderPointer() ?? _contexts.FirstOrDefault();
        if (context is null)
        {
            return;
        }

        context.ViewModel.Toggle();
        context.Window.BringToTop();
    }

    private void SynchronizeDisplays()
    {
        var areas = DisplayArea.FindAll();
        var seen = new HashSet<string>();

        // O IReadOnlyList projetado pelo WinRT não aceita foreach: enumerar lança
        // InvalidCastException no CsWinRT. O acesso por índice é o caminho suportado.
        for (var i = 0; i < areas.Count; i++)
        {
            var area = areas[i];
            var bounds = area.OuterBounds;
            var key = DisplayKey(bounds);
            seen.Add(key);

            var isPrimary = area.IsPrimary;

            // Sem a preferência de várias telas, só a principal recebe ilha.
            if (!isPrimary && !_settings.ShowOnAllDisplays)
            {
                continue;
            }

            var existing = _contexts.FirstOrDefault(c => c.Key == key);
            if (existing is not null)
            {
                if (!Equals(existing.Bounds, bounds))
                {
                    existing.Bounds = bounds;
                    existing.Window.PositionOn(bounds);
                }

                continue;
            }

            var context = CreateContext(bounds, key, isPrimary ? DisplayKind.Primary : DisplayKind.Secondary);
            _contexts.Add(context);
            AppLog.Window.Info($"Ilha criada para o monitor {key}");
        }

        for (var i = _contexts.Count - 1; i >= 0; i--)
        {
            var context = _contexts[i];
            var stillPresent = seen.Contains(context.Key)
                && (context.ViewModel.DisplayKind == DisplayKind.Primary || _settings.ShowOnAllDisplays);

            if (stillPresent)
            {
                continue;
            }

            context.Window.Teardown();
            context.Window.Close();
            _contexts.RemoveAt(i);
            AppLog.Window.Info($"Ilha removida do monitor {context.Key}");
        }
    }

    private DisplayContext CreateContext(RectInt32 bounds, string key, DisplayKind kind)
    {
        var viewModel = new NotchViewModel(_media, _settings, kind);
        var window = new NotchWindow(viewModel);

        window.Activate();
        window.PositionOn(bounds);
        window.BringToTop();

        return new DisplayContext
        {
            Window = window,
            ViewModel = viewModel,
            Key = key,
            Bounds = bounds
        };
    }

    /// <summary>Identidade estável do monitor. O DisplayArea não expõe um id persistente,
    /// então a posição e o tamanho servem de chave.</summary>
    private static string DisplayKey(RectInt32 bounds)
        => $"{bounds.X}x{bounds.Y}:{bounds.Width}x{bounds.Height}";

    private void OnPointerTick(DispatcherQueueTimer sender, object args)
    {
        if (++_tickCounter >= DisplaySyncEveryTicks)
        {
            _tickCounter = 0;
            SynchronizeDisplays();
        }

        if (!Win32.GetCursorPos(out var point))
        {
            return;
        }

        var now = DateTimeOffset.Now;

        foreach (var context in _contexts)
        {
            var inside = IsPointerInside(context, point);
            UpdatePointerState(context, inside, now);
        }
    }

    /// <summary>
    /// A janela é transparente e não ativa o aplicativo. Nessas condições o hover do XAML
    /// não chega de forma confiável quando outro aplicativo está em primeiro plano, então
    /// consultamos a posição global do ponteiro, como a versão macOS faz.
    /// </summary>
    private static bool IsPointerInside(DisplayContext context, Win32.POINT point)
    {
        var scale = Win32.GetScaleFactor(context.Window.Handle);

        var width = context.ViewModel.InteractionWidth * scale;
        var height = context.ViewModel.InteractionHeight * scale;

        // A ilha fica centralizada no monitor e encostada no topo.
        var left = context.Bounds.X + (context.Bounds.Width - width) / 2;
        var top = context.Bounds.Y;

        return point.X >= left
            && point.X <= left + width
            && point.Y >= top
            && point.Y <= top + height;
    }

    /// <summary>
    /// Histerese: abre depois de uma pausa curta e fecha depois de uma pausa maior, para o
    /// painel não piscar quando o cursor apenas atravessa a região.
    /// </summary>
    private static void UpdatePointerState(DisplayContext context, bool inside, DateTimeOffset now)
    {
        if (inside == context.PointerInside)
        {
            context.PendingSince = null;
            return;
        }

        if (context.PendingSince is null || context.PendingTarget != inside)
        {
            context.PendingSince = now;
            context.PendingTarget = inside;
            return;
        }

        var delay = inside ? NotchAnimation.PointerEnterDelay : NotchAnimation.PointerExitDelay;
        if (now - context.PendingSince.Value < delay)
        {
            return;
        }

        context.PointerInside = inside;
        context.PendingSince = null;
        context.ViewModel.SetExpanded(inside);

        if (inside)
        {
            context.Window.BringToTop();
        }
    }

    private DisplayContext? ContextUnderPointer()
    {
        if (!Win32.GetCursorPos(out var point))
        {
            return null;
        }

        return _contexts.FirstOrDefault(c =>
            point.X >= c.Bounds.X
            && point.X < c.Bounds.X + c.Bounds.Width
            && point.Y >= c.Bounds.Y
            && point.Y < c.Bounds.Y + c.Bounds.Height);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }
}
