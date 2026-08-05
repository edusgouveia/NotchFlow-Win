using System.ComponentModel;
using System.Runtime.CompilerServices;
using NotchFlow.Core.Logging;
using NotchFlow.Core.Models;

namespace NotchFlow.Core.Media;

/// <summary>
/// Mantém o estado de reprodução que a ilha mostra: escuta o serviço de mídia, escolhe a
/// sessão ativa e executa os comandos. Equivalente ao MediaCoordinator da versão macOS.
/// </summary>
public sealed class MediaCoordinator : INotifyPropertyChanged, IDisposable
{
    /// <summary>O SMTC dispara vários eventos em rajada quando uma faixa troca.
    /// Agrupar as leituras evita reconstruir o painel várias vezes seguidas.</summary>
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(120);

    /// <summary>Rede de segurança para eventos perdidos. Não é o mecanismo principal de
    /// atualização, ao contrário do polling obrigatório da versão macOS.</summary>
    private static readonly TimeSpan SafetyInterval = TimeSpan.FromSeconds(5);

    private readonly ISystemMediaService _service;
    private readonly Action<Action> _dispatch;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private CancellationTokenSource? _debounceSource;
    private Timer? _safetyTimer;
    private PlaybackSnapshot? _currentSnapshot;
    private string? _preferredSessionId;
    private bool _disposed;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <param name="dispatch">Marshala para a thread de UI. Os eventos do SMTC chegam em
    /// threads de background, e o estado é consumido por XAML.</param>
    public MediaCoordinator(ISystemMediaService service, Action<Action> dispatch)
    {
        _service = service;
        _dispatch = dispatch;
    }

    public PlaybackSnapshot? CurrentSnapshot
    {
        get => _currentSnapshot;
        private set
        {
            if (_currentSnapshot == value)
            {
                return;
            }

            _currentSnapshot = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasMedia));
        }
    }

    public bool HasMedia => _currentSnapshot is not null;

    public async Task StartAsync()
    {
        _service.SessionsChanged += OnSessionsChanged;
        await _service.StartAsync();
        await RefreshAsync();

        _safetyTimer = new Timer(
            _ => _ = RefreshAsync(),
            state: null,
            dueTime: SafetyInterval,
            period: SafetyInterval);

        AppLog.Media.Info("Coordenador de mídia iniciado");
    }

    public void Stop()
    {
        _service.SessionsChanged -= OnSessionsChanged;
        _safetyTimer?.Dispose();
        _safetyTimer = null;
        _debounceSource?.Cancel();
    }

    private void OnSessionsChanged(object? sender, EventArgs e)
    {
        // Reinicia a janela de debounce: só a última mudança da rajada gera leitura.
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
            var snapshots = await _service.ReadSnapshotsAsync();
            var selected = PlaybackSelector.Select(snapshots, _preferredSessionId);

            if (selected?.IsPlaying == true)
            {
                _preferredSessionId = selected.SessionId;
            }

            _dispatch(() => CurrentSnapshot = selected);
        }
        catch (Exception ex)
        {
            AppLog.Media.Error($"Falha ao atualizar mídia: {ex.GetType().Name}");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public async Task PerformAsync(PlayerCommand command)
    {
        var snapshot = _currentSnapshot;
        if (snapshot is null)
        {
            return;
        }

        // A sessão comandada passa a ser a preferida, para a ilha não pular para outro
        // player logo depois de o usuário interagir com este.
        _preferredSessionId = snapshot.SessionId;

        var succeeded = await _service.PerformAsync(snapshot.SessionId, command, snapshot);
        if (!succeeded)
        {
            AppLog.Media.Error($"Player recusou o comando {command.Kind}");
        }

        // O SMTC costuma emitir o evento sozinho, mas uma leitura extra deixa a resposta
        // ao clique imediata em players que demoram a notificar.
        await Task.Delay(180);
        await RefreshAsync();
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
