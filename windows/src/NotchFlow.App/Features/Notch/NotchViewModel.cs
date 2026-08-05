using System.ComponentModel;
using System.Runtime.CompilerServices;
using NotchFlow.Core.Media;
using NotchFlow.Core.Models;
using NotchFlow.Core.Notifications;
using NotchFlow.Core.Settings;

namespace NotchFlow.App.Features.Notch;

/// <summary>Tempos da abertura e do fechamento, herdados do NotchAnimation da versão macOS.</summary>
public static class NotchAnimation
{
    public static readonly TimeSpan ShapeDuration = TimeSpan.FromMilliseconds(280);
    public static readonly TimeSpan ContentInDuration = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan ContentOutDuration = TimeSpan.FromMilliseconds(70);

    /// <summary>Espera até a forma estar praticamente aberta antes de mostrar o conteúdo.</summary>
    public static readonly TimeSpan ContentInDelay = TimeSpan.FromMilliseconds(110);

    /// <summary>Deixa o conteúdo desaparecer antes de a forma voltar ao tamanho fechado.</summary>
    public static readonly TimeSpan ShapeCloseDelay = TimeSpan.FromMilliseconds(70);

    /// <summary>Atrasos de histerese do ponteiro: abre rápido, fecha com folga para não
    /// piscar quando o cursor apenas cruza a região.</summary>
    public static readonly TimeSpan PointerEnterDelay = TimeSpan.FromMilliseconds(55);
    public static readonly TimeSpan PointerExitDelay = TimeSpan.FromMilliseconds(300);
}

/// <summary>
/// Estado de uma ilha. Há uma instância por monitor, todas compartilhando o mesmo
/// coordenador de mídia para não multiplicar consultas ao sistema.
/// </summary>
public sealed class NotchViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;

    private CancellationTokenSource? _stageSource;
    private bool _isExpanded;
    private bool _showsContent;
    private NotchGeometry _geometry;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Disparado quando a silhueta muda de tamanho, para a janela recortar de novo.</summary>
    public event EventHandler? ShapeChanged;

    public NotchViewModel(
        MediaCoordinator media,
        NotificationCoordinator notifications,
        AppSettings settings,
        DisplayKind displayKind)
    {
        Media = media;
        Notifications = notifications;
        _settings = settings;
        DisplayKind = displayKind;
        _geometry = BuildGeometry();

        // A ilha recolhida tem largura variável e a coluna de notificações aparece e some,
        // então qualquer mudança nas duas fontes precisa recalcular a geometria.
        Notifications.PropertyChanged += (_, _) => RefreshGeometry();
        Media.PropertyChanged += (_, _) => RefreshGeometry();
    }

    public MediaCoordinator Media { get; }

    public NotificationCoordinator Notifications { get; }

    public DisplayKind DisplayKind { get; }

    public NotchGeometry Geometry
    {
        get => _geometry;
        private set
        {
            if (_geometry == value)
            {
                return;
            }

            _geometry = value;
            RaisePropertyChanged();
            ShapeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Controla a forma. O interior é controlado à parte por <see cref="ShowsContent"/>
    /// para as duas animações não se atropelarem.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        private set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            RaisePropertyChanged();
            ShapeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool ShowsContent
    {
        get => _showsContent;
        private set
        {
            if (_showsContent == value)
            {
                return;
            }

            _showsContent = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>Silhueta atual, já considerando se a ilha está aberta.</summary>
    public double CurrentWidth => IsExpanded ? Geometry.ExpandedWidth : Geometry.ClosedWidthValue;

    public double CurrentHeight => IsExpanded ? Geometry.ExpandedHeight : Geometry.ClosedHeightValue;

    public double CurrentCornerRadius =>
        IsExpanded ? Geometry.ExpandedCornerRadius : Geometry.ClosedCornerRadius;

    /// <summary>Região que responde ao ponteiro. Quando recolhida numa tela secundária ela é
    /// maior que a tira visível, senão seria difícil acertar 9 pixels no topo da tela.</summary>
    public double InteractionWidth => IsExpanded ? Geometry.ExpandedWidth : Geometry.ClosedInteractionWidth;

    public double InteractionHeight => IsExpanded ? Geometry.ExpandedHeight : Geometry.ClosedInteractionHeight;

    public void ApplySettings() => RefreshGeometry();

    private void RefreshGeometry()
    {
        Geometry = BuildGeometry();
        if (Geometry.IsMinimized && IsExpanded)
        {
            SetExpanded(false);
        }
    }

    private NotchGeometry BuildGeometry()
    {
        var snapshot = Media.CurrentSnapshot;
        var mostraNotificacoes = _settings.NotificationsEnabled && Notifications.HasNotifications;

        return NotchGeometry.Make(
            DisplayKind,
            _settings.MinimizeOnSecondaryDisplays,
            includesNotifications: mostraNotificacoes,
            closedContent: new ClosedContent(
                HasArtwork: snapshot?.ArtworkData is not null,
                HasMedia: snapshot is not null,
                NotificationCount: mostraNotificacoes ? Notifications.Count : 0));
    }

    public void Toggle() => SetExpanded(!IsExpanded);

    /// <summary>
    /// Abre em duas etapas: primeiro a forma cresce, depois o conteúdo aparece.
    /// Ao fechar, o conteúdo sai primeiro e a forma encolhe em seguida.
    /// A etapa pendente é cancelada a cada chamada, então movimentos rápidos do cursor
    /// não deixam a ilha num estado intermediário.
    /// </summary>
    public void SetExpanded(bool expanded)
    {
        _stageSource?.Cancel();
        _stageSource?.Dispose();

        if (expanded == IsExpanded && expanded == ShowsContent)
        {
            _stageSource = null;
            return;
        }

        var source = new CancellationTokenSource();
        _stageSource = source;

        if (expanded)
        {
            IsExpanded = true;
            RunStage(NotchAnimation.ContentInDelay, () => ShowsContent = true, source.Token);
        }
        else
        {
            ShowsContent = false;
            RunStage(NotchAnimation.ShapeCloseDelay, () => IsExpanded = false, source.Token);
        }
    }

    private static async void RunStage(TimeSpan delay, Action action, CancellationToken token)
    {
        try
        {
            // Continua na thread de UI: quem chama SetExpanded já está nela.
            await Task.Delay(delay, token).ConfigureAwait(true);
            if (!token.IsCancellationRequested)
            {
                action();
            }
        }
        catch (OperationCanceledException)
        {
            // Substituída por uma mudança de estado mais recente.
        }
    }

    /// <summary>Notifica as propriedades derivadas do estado de abertura.</summary>
    internal void RaiseShapeProperties()
    {
        RaisePropertyChanged(nameof(CurrentWidth));
        RaisePropertyChanged(nameof(CurrentHeight));
        RaisePropertyChanged(nameof(CurrentCornerRadius));
    }

    private void RaisePropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
