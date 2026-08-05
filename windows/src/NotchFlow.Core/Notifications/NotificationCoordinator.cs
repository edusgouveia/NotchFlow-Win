using System.ComponentModel;
using System.Runtime.CompilerServices;
using NotchFlow.Core.Logging;
using NotchFlow.Core.Models;
using NotchFlow.Core.Settings;

namespace NotchFlow.Core.Notifications;

/// <summary>
/// Mantém o que a ilha mostra sobre notificações. Espelha o <c>MediaCoordinator</c>:
/// escuta o serviço, agrupa rajadas e expõe o estado já pronto para a interface.
/// </summary>
public sealed class NotificationCoordinator : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(150);

    /// <summary>Rede de segurança para eventos perdidos, como no coordenador de mídia.</summary>
    private static readonly TimeSpan SafetyInterval = TimeSpan.FromSeconds(4);

    /// <summary>Quantas notificações o painel lista. Acima disso o contador informa o resto.</summary>
    private const int VisibleLimit = 3;

    private readonly INotificationService _service;
    private readonly AppSettings _settings;
    private readonly Action<Action> _dispatch;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private CancellationTokenSource? _debounceSource;
    private Timer? _safetyTimer;
    private IReadOnlyList<NotificationItem> _items = [];

    /// <summary>Ids já vistos. Serve para distinguir uma notificação nova de uma releitura
    /// da mesma lista, que é o que dispara o aviso na ilha.</summary>
    private readonly HashSet<uint> _known = [];

    private bool _primed;
    private bool _disposed;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Disparado quando chega uma notificação inédita. Já na thread de interface.</summary>
    public event EventHandler<NotificationItem>? NotificationArrived;

    public NotificationCoordinator(
        INotificationService service,
        AppSettings settings,
        Action<Action> dispatch)
    {
        _service = service;
        _settings = settings;
        _dispatch = dispatch;
    }

    public IReadOnlyList<NotificationItem> Items
    {
        get => _items;
        private set
        {
            _items = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(Count));
            RaisePropertyChanged(nameof(HasNotifications));
            RaisePropertyChanged(nameof(Visible));
            RaisePropertyChanged(nameof(Accent));
        }
    }

    public int Count => _items.Count;

    public bool HasNotifications => _items.Count > 0;

    /// <summary>As mais recentes, na quantidade que cabe no painel.</summary>
    public IReadOnlyList<NotificationItem> Visible =>
        _items.Count <= VisibleLimit ? _items : _items.Take(VisibleLimit).ToList();

    /// <summary>Quantas ficaram de fora da lista visível.</summary>
    public int Overflow => Math.Max(0, _items.Count - VisibleLimit);

    /// <summary>Cor da notificação mais recente, usada no indicador recolhido.</summary>
    public uint Accent => _items.Count > 0 ? _items[0].Accent : 0xFF9E9E9E;

    public NotificationAccess Access => _service.Access;

    public async Task StartAsync()
    {
        if (!_settings.NotificationsEnabled)
        {
            AppLog.Notifications.Info("Notificações desligadas nas preferências");
            return;
        }

        _service.NotificationsChanged += OnNotificationsChanged;

        var access = await _service.RequestAccessAsync();
        if (access != NotificationAccess.Allowed)
        {
            _dispatch(() => RaisePropertyChanged(nameof(Access)));
            return;
        }

        await RefreshAsync();

        _safetyTimer = new Timer(
            _ => _ = RefreshAsync(), state: null, dueTime: SafetyInterval, period: SafetyInterval);
    }

    public void Stop()
    {
        _service.NotificationsChanged -= OnNotificationsChanged;
        _safetyTimer?.Dispose();
        _safetyTimer = null;
        _debounceSource?.Cancel();
    }

    private void OnNotificationsChanged(object? sender, EventArgs e)
    {
        _debounceSource?.Cancel();
        var source = new CancellationTokenSource();
        _debounceSource = source;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceWindow, source.Token);
                await RefreshAsync();
            }
            catch (OperationCanceledException)
            {
                // Substituída por uma mudança mais recente.
            }
        });
    }

    public async Task RefreshAsync()
    {
        if (_disposed || !await _refreshGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var items = await _service.ReadAsync();

            // A primeira leitura só registra o que já estava na Central. Sem isso, abrir o
            // NotchFlow com mensagens acumuladas dispararia um aviso para cada uma.
            var arrivals = new List<NotificationItem>();
            foreach (var item in items)
            {
                if (_known.Add(item.Id) && _primed)
                {
                    arrivals.Add(item);
                }
            }

            _primed = true;

            // Ids que sumiram da Central foram lidos no aplicativo de origem; esquecê-los
            // evita o conjunto crescer para sempre e permite reavisar se voltarem.
            var present = items.Select(i => i.Id).ToHashSet();
            _known.RemoveWhere(id => !present.Contains(id));

            _dispatch(() =>
            {
                Items = items;

                // Avisa só da mais recente: uma rajada não deve virar uma fila de aberturas.
                if (arrivals.Count > 0)
                {
                    NotificationArrived?.Invoke(this, arrivals[0]);
                }
            });
        }
        catch (Exception ex)
        {
            AppLog.Notifications.Error($"Falha ao atualizar notificações: {ex.GetType().Name}");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void RaisePropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _debounceSource?.Dispose();
        _refreshGate.Dispose();
        _service.Dispose();
    }
}
