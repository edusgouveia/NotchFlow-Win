using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NotchFlow.App.Interop;
using NotchFlow.Core.Models;
using Windows.Graphics;
using Windows.Storage.Streams;
using Windows.UI;

namespace NotchFlow.App.Features.Notch;

/// <summary>
/// Uma ilha. Existe uma janela destas por monitor.
///
/// A janela mede sempre 520x224, mas é recortada por <see cref="Win32.ApplyIslandRegion"/> para a
/// silhueta visível. O recorte é reaplicado a cada quadro da animação, junto com o tamanho do
/// Border, para o contorno nunca ficar defasado em relação ao desenho.
/// </summary>
public sealed partial class NotchWindow : Window
{
    private const double AnimationFps = 60;

    /// <summary>Diâmetro dos botões de ação do painel, usado para centralizá-los na faixa
    /// superior. Precisa acompanhar o IslandActionStyle definido em App.xaml.</summary>
    private const double IslandActionSize = 22;

    // Glyphs do Segoe Fluent Icons, escritos com escape porque são da área de uso privado
    // do Unicode e não sobrevivem bem como literais no arquivo-fonte.
    private const string GlyphPlay = "";
    private const string GlyphPause = "";
    private const string GlyphVolume = "";

    private readonly NotchViewModel _viewModel;
    private readonly DispatcherQueueTimer _animationTimer;
    private readonly DispatcherQueueTimer _progressTimer;

    private IntPtr _hwnd;
    private double _animatedWidth;
    private double _animatedHeight;
    private double _animationFrom;
    private double _animationTo;
    private double _animationFromHeight;
    private double _animationToHeight;
    private double _animationProgress = 1;
    private bool _isDraggingProgress;
    private double _dragProgress;

    public NotchWindow(NotchViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        ConfigureWindow();

        var queue = DispatcherQueue.GetForCurrentThread();
        _animationTimer = queue.CreateTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(1000 / AnimationFps);
        _animationTimer.Tick += OnAnimationTick;

        _progressTimer = queue.CreateTimer();
        _progressTimer.Interval = TimeSpan.FromSeconds(1);
        _progressTimer.Tick += (_, _) => UpdateProgress();

        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Media.PropertyChanged += OnMediaChanged;
        _viewModel.Notifications.PropertyChanged += OnNotificationsChanged;

        WireCommands();

        _animatedWidth = _viewModel.CurrentWidth;
        _animatedHeight = _viewModel.CurrentHeight;
        ApplyShape(immediate: true);
        RenderMedia();
        RenderNotifications();
    }

    /// <summary>Linha da lista de notificações. Existe porque o tempo relativo é calculado
    /// no momento da exibição, e não cabe no modelo imutável.</summary>
    private sealed record NotificationRow(
        string Title, string Body, string Time, SolidColorBrush Accent);

    /// <summary>Cinza usado quando a lista mistura fontes e nenhuma cor representa o todo.</summary>
    private static readonly Color NeutralAccent = Color.FromArgb(0xFF, 0xB8, 0xB8, 0xB8);

    public NotchViewModel ViewModel => _viewModel;

    public IntPtr Handle => _hwnd;

    private void ConfigureWindow()
    {
        SystemBackdrop = null;

        // Sem isto o conteúdo XAML começa abaixo da faixa de título, mesmo com ela removida,
        // e o fundo da janela aparece como um risco claro na aresta superior da ilha.
        ExtendsContentIntoTitleBar = true;

        var appWindow = AppWindow;
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        appWindow.IsShownInSwitchers = false;

        Win32.ApplyIslandStyles(_hwnd);
        Win32.RemoveSystemChrome(_hwnd);
    }

    /// <summary>Reposiciona a janela no topo do monitor indicado.</summary>
    public void PositionOn(RectInt32 displayBounds)
    {
        var scale = Win32.GetScaleFactor(_hwnd);
        var (offsetX, offsetY, _, _) = Win32.GetClientMetrics(_hwnd);

        // A janela é dimensionada para que a ÁREA CLIENTE meça 520x224, e é subida em
        // offsetY para que o topo dessa área caia exatamente na primeira linha do monitor.
        // Assim a borda não-cliente clara fica fora da tela.
        var clientWidth = (int)Math.Round(NotchGeometry.WindowWidth * scale);
        var clientHeight = (int)Math.Round(NotchGeometry.WindowHeight * scale);

        var x = displayBounds.X + (displayBounds.Width - clientWidth) / 2 - offsetX;
        var y = displayBounds.Y - offsetY;

        AppWindow.MoveAndResize(new RectInt32(
            x,
            y,
            clientWidth + offsetX * 2,
            clientHeight + offsetY * 2));

        // Reaplicado após a ativação: alguns atributos do DWM são redefinidos quando a
        // janela é mostrada, e aí a moldura volta a aparecer.
        Win32.RemoveSystemChrome(_hwnd);

        ApplyShape(immediate: true);
    }

    public void BringToTop() => Win32.BringToTop(_hwnd);

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NotchViewModel.IsExpanded):
                StartShapeAnimation();
                UpdateContentVisibility();
                break;

            case nameof(NotchViewModel.ShowsContent):
                UpdateContentVisibility();
                break;

            case nameof(NotchViewModel.Geometry):
                ApplyShape(immediate: true);
                RenderMedia();
                RenderNotifications();
                break;
        }
    }

    private void OnMediaChanged(object? sender, PropertyChangedEventArgs e) => RenderMedia();

    private void OnNotificationsChanged(object? sender, PropertyChangedEventArgs e)
        => RenderNotifications();

    // ---------- Notificações ----------

    private void RenderNotifications()
    {
        var notifications = _viewModel.Notifications;
        var show = _viewModel.Geometry.IncludesNotifications && notifications.HasNotifications;

        NotificationColumn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        NotificationDivider.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        // Quem decide o contador é o RenderCollapsed, junto com o resto da fileira:
        // a cápsula neutra depende de saber se o contador está lá.
        RenderCollapsed(_viewModel.Media.CurrentSnapshot, BrandColor(
            _viewModel.Media.CurrentSnapshot?.Brand ?? PlaybackBrand.Neutral));

        if (!show)
        {
            NotificationList.ItemsSource = null;
            return;
        }

        // Com fontes misturadas o cabeçalho fica genérico: dizer "Teams" numa lista que
        // também traz Outlook seria mentira. A cor acompanha a mesma regra.
        var single = notifications.SingleSource;
        var rotulo = single ?? "Notificações";
        NotificationHeader.Text = notifications.Count > 1
            ? $"{rotulo} · {notifications.Count}"
            : rotulo;

        var corDoCabecalho = new SolidColorBrush(
            single is null ? NeutralAccent : FromArgb(notifications.Accent));
        NotificationHeader.Foreground = corDoCabecalho;
        NotificationIcon.Foreground = corDoCabecalho;

        var now = DateTimeOffset.Now;
        NotificationList.ItemsSource = notifications.Visible
            .Select(item => new NotificationRow(
                item.HasTitle ? item.Title : item.Source,
                item.Body,
                item.RelativeTime(now),
                new SolidColorBrush(FromArgb(item.Accent))))
            .ToList();

        var overflow = notifications.Overflow;
        NotificationOverflow.Visibility = overflow > 0 ? Visibility.Visible : Visibility.Collapsed;
        NotificationOverflow.Text = overflow == 1 ? "+1 mais" : $"+{overflow} mais";
    }

    private static Color FromArgb(uint value) => Color.FromArgb(
        (byte)((value >> 24) & 0xFF),
        (byte)((value >> 16) & 0xFF),
        (byte)((value >> 8) & 0xFF),
        (byte)(value & 0xFF));

    // ---------- Forma ----------

    private void StartShapeAnimation()
    {
        _animationFrom = _animatedWidth;
        _animationFromHeight = _animatedHeight;
        _animationTo = _viewModel.CurrentWidth;
        _animationToHeight = _viewModel.CurrentHeight;
        _animationProgress = 0;
        _animationTimer.Start();
    }

    private void OnAnimationTick(DispatcherQueueTimer sender, object args)
    {
        _animationProgress += (1000.0 / AnimationFps) / NotchAnimation.ShapeDuration.TotalMilliseconds;

        if (_animationProgress >= 1)
        {
            _animationProgress = 1;
            _animationTimer.Stop();
        }

        var eased = EaseInOut(_animationProgress);
        _animatedWidth = _animationFrom + (_animationTo - _animationFrom) * eased;
        _animatedHeight = _animationFromHeight + (_animationToHeight - _animationFromHeight) * eased;

        ApplyShape(immediate: false);
    }

    /// <summary>Curva suave equivalente à <c>Animation.smooth</c> usada no SwiftUI.</summary>
    private static double EaseInOut(double t)
        => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

    /// <summary>
    /// Faixa livre no topo do painel aberto, onde a ilha recolhida estaria.
    ///
    /// Vem da geometria em vez de ficar fixa no XAML: a altura da ilha recolhida mudou uma
    /// vez e o valor cravado deixaria o conteúdo desalinhado sem ninguém perceber.
    /// </summary>
    private void ApplyExpandedPadding()
    {
        var topo = _viewModel.Geometry.ExpandedTopPadding;

        ExpandedContent.Padding = new Thickness(
            NotchGeometry.HorizontalPadding,
            topo,
            NotchGeometry.HorizontalPadding,
            NotchGeometry.BottomPadding);

        // As ações sobem para dentro dessa faixa, centralizadas nela.
        IslandActions.Margin = new Thickness(0, -(topo + IslandActionSize) / 2, 0, 0);
    }

    private void ApplyShape(bool immediate)
    {
        if (immediate)
        {
            _animatedWidth = _viewModel.CurrentWidth;
            _animatedHeight = _viewModel.CurrentHeight;
        }

        Island.Width = _animatedWidth;
        Island.Height = _animatedHeight;

        var radius = _viewModel.CurrentCornerRadius;
        Island.CornerRadius = new CornerRadius(0, 0, radius, radius);

        ApplyExpandedPadding();

        // O recorte acompanha o mesmo valor que o Border acabou de receber, então
        // a silhueta do sistema e o desenho nunca divergem durante a animação.
        Win32.ApplyIslandRegion(_hwnd, _animatedWidth, _animatedHeight, radius);
    }

    private void UpdateContentVisibility()
    {
        var showsContent = _viewModel.ShowsContent;

        ExpandedContent.Visibility = showsContent ? Visibility.Visible : Visibility.Collapsed;
        CollapsedContent.Visibility = _viewModel.IsExpanded ? Visibility.Collapsed : Visibility.Visible;

        FadeTo(ExpandedContent, showsContent ? 1 : 0,
            showsContent ? NotchAnimation.ContentInDuration : NotchAnimation.ContentOutDuration);

        if (showsContent)
        {
            UpdateProgress();
            _progressTimer.Start();
        }
        else
        {
            _progressTimer.Stop();
        }
    }

    private static void FadeTo(UIElement element, double opacity, TimeSpan duration)
    {
        var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            To = opacity,
            Duration = new Duration(duration),
            EnableDependentAnimation = true
        };

        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, element);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    // ---------- Mídia ----------

    private void RenderMedia()
    {
        var snapshot = _viewModel.Media.CurrentSnapshot;
        var accent = BrandColor(snapshot?.Brand ?? PlaybackBrand.Neutral);

        RenderCollapsed(snapshot, accent);
        RenderExpanded(snapshot, accent);
    }

    /// <summary>Conteúdo da ilha recolhida: capa à esquerda, risco no meio, estado da
    /// reprodução e contador de notificações à direita.</summary>
    private void RenderCollapsed(PlaybackSnapshot? snapshot, Color accent)
    {
        // Numa tela secundária a ilha recolhida é só a tira, sem capa, ícone nem contador.
        var isSliver = _viewModel.Geometry.IsMinimized;
        var hasMedia = snapshot is not null;
        var hasArtwork = !isSliver && snapshot?.ArtworkData is not null;

        var notifications = _viewModel.Notifications;
        var badgeCount = !isSliver && _viewModel.Geometry.IncludesNotifications
            ? notifications.Count
            : 0;

        CollapsedArtwork.Visibility = hasArtwork ? Visibility.Visible : Visibility.Collapsed;

        CollapsedStatusIcon.Visibility = !isSliver && hasMedia ? Visibility.Visible : Visibility.Collapsed;
        CollapsedStatusIcon.Glyph = snapshot?.IsPlaying == true ? GlyphVolume : GlyphPause;
        CollapsedStatusIcon.Foreground = new SolidColorBrush(accent);

        if (badgeCount > 0)
        {
            NotificationBadge.Visibility = Visibility.Visible;
            NotificationBadgeText.Text = badgeCount > 9 ? "9+" : badgeCount.ToString();
            NotificationBadge.Background = new SolidColorBrush(FromArgb(notifications.Accent));
        }
        else
        {
            NotificationBadge.Visibility = Visibility.Collapsed;
        }

        // O risco no meio some só quando há capa ocupando o lugar dele.
        CollapsedIndicator.Visibility = hasArtwork ? Visibility.Collapsed : Visibility.Visible;
        CollapsedIndicator.Background = new SolidColorBrush(
            hasMedia ? accent : Color.FromArgb(0x9E, 0x9E, 0x9E, 0x9E));

        if (hasArtwork && snapshot?.ArtworkData is { } artwork)
        {
            _ = SetImageAsync(CollapsedArtworkBrush, artwork);
        }
    }

    private void RenderExpanded(PlaybackSnapshot? snapshot, Color accent)
    {
        if (snapshot is null)
        {
            EmptyState.Visibility = Visibility.Visible;
            PlayerState.Visibility = Visibility.Collapsed;
            _progressTimer.Stop();
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        PlayerState.Visibility = Visibility.Visible;

        TitleText.Text = snapshot.Title;
        SubtitleText.Text = Subtitle(snapshot);
        SourceLabel.Text = snapshot.SourceLabel;

        var accentBrush = new SolidColorBrush(accent);
        SourceLabel.Foreground = accentBrush;
        SourceIcon.Foreground = accentBrush;

        PlayPauseIcon.Glyph = snapshot.IsPlaying ? GlyphPause : GlyphPlay;

        PlayPauseButton.IsEnabled = snapshot.SupportsPlayPause;
        PreviousButton.IsEnabled = snapshot.SupportsTrackSkip;
        NextButton.IsEnabled = snapshot.SupportsTrackSkip;
        BackFifteenButton.IsEnabled = snapshot.SupportsSeek;
        ForwardFifteenButton.IsEnabled = snapshot.SupportsSeek;

        if (snapshot.ArtworkData is { } artwork)
        {
            ArtworkImage.Visibility = Visibility.Visible;
            ArtworkFallback.Visibility = Visibility.Collapsed;
            _ = SetImageAsync(ArtworkBrush, artwork);
        }
        else
        {
            ArtworkImage.Visibility = Visibility.Collapsed;
            ArtworkFallback.Visibility = Visibility.Visible;
        }

        UpdateProgress();

        if (_viewModel.ShowsContent)
        {
            _progressTimer.Start();
        }
    }

    private static string Subtitle(PlaybackSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.Artist))
        {
            return snapshot.Artist;
        }

        return string.IsNullOrWhiteSpace(snapshot.Album) ? snapshot.SourceLabel : snapshot.Album;
    }

    private void UpdateProgress()
    {
        var snapshot = _viewModel.Media.CurrentSnapshot;
        if (snapshot is null || _isDraggingProgress)
        {
            return;
        }

        var now = DateTimeOffset.Now;

        PositionText.Text = FormatTime(snapshot.EffectivePosition(now));
        DurationText.Text = FormatTime(snapshot.Duration);

        RenderProgress(Math.Clamp(snapshot.Progress(now), 0, 1));
    }

    /// <summary>Posiciona o preenchimento e o polegar a partir de um progresso de 0 a 1.</summary>
    private void RenderProgress(double progress)
    {
        var width = ProgressTrack.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        var filled = width * progress;
        ProgressFill.Width = filled;

        // O polegar é centralizado no ponto atual, sem escapar das pontas do traço.
        var thumbSize = ProgressThumb.Width;
        var offset = Math.Clamp(filled - thumbSize / 2, 0, Math.Max(width - thumbSize, 0));
        ProgressThumb.Margin = new Thickness(offset, 0, 0, 0);

        var seekable = _viewModel.Media.CurrentSnapshot?.SupportsSeek == true;
        ProgressThumb.Opacity = seekable ? 1 : 0;
    }

    /// <summary>Converte a posição do ponteiro dentro do traço em progresso de 0 a 1.</summary>
    private double ProgressFromPointer(PointerRoutedEventArgs args)
    {
        var width = ProgressTrack.ActualWidth;
        if (width <= 0)
        {
            return 0;
        }

        var x = args.GetCurrentPoint(ProgressTrack).Position.X;
        return Math.Clamp(x / width, 0, 1);
    }

    private static string FormatTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:D2}:{value.Seconds:D2}"
            : $"{(int)value.TotalMinutes}:{value.Seconds:D2}";
    }

    private static async Task SetImageAsync(ImageBrush brush, byte[] data)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();

            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(data);
                await writer.StoreAsync();
                await writer.FlushAsync();
                // Sem o detach, o writer fecharia o stream ao ser descartado.
                writer.DetachStream();
            }

            stream.Seek(0);

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            brush.ImageSource = bitmap;
        }
        catch (Exception)
        {
            // Uma capa ilegível apenas não aparece.
        }
    }

    private static Color BrandColor(PlaybackBrand brand) => brand switch
    {
        PlaybackBrand.Spotify => Color.FromArgb(0xFF, 0x1E, 0xD7, 0x60),
        PlaybackBrand.YouTube => Color.FromArgb(0xFF, 0xFF, 0x28, 0x24),
        PlaybackBrand.AppleMusic => Color.FromArgb(0xFF, 0xFF, 0x7A, 0x1F),
        _ => Color.FromArgb(0xFF, 0xB8, 0xB8, 0xB8)
    };

    // ---------- Comandos ----------

    private void WireCommands()
    {
        PlayPauseButton.Click += (_, _) => Perform(PlayerCommand.TogglePlayPause());
        PreviousButton.Click += (_, _) => Perform(PlayerCommand.PreviousTrack());
        NextButton.Click += (_, _) => Perform(PlayerCommand.NextTrack());
        BackFifteenButton.Click += (_, _) => Perform(PlayerCommand.Skip(TimeSpan.FromSeconds(-15)));
        ForwardFifteenButton.Click += (_, _) => Perform(PlayerCommand.Skip(TimeSpan.FromSeconds(15)));

        CollapseButton.Click += (_, _) => _viewModel.SetExpanded(false);
        QuitButton.Click += (_, _) => Microsoft.UI.Xaml.Application.Current.Exit();

        // O valor arrastado tem prioridade sobre a posição lida do player até o
        // usuário soltar, evitando que o indicador pule de volta no meio do gesto.
        ProgressTrack.PointerPressed += (_, args) =>
        {
            if (_viewModel.Media.CurrentSnapshot?.SupportsSeek != true)
            {
                return;
            }

            _isDraggingProgress = true;
            _dragProgress = ProgressFromPointer(args);
            RenderProgress(_dragProgress);
            ProgressTrack.CapturePointer(args.Pointer);
        };

        ProgressTrack.PointerMoved += (_, args) =>
        {
            if (!_isDraggingProgress)
            {
                return;
            }

            _dragProgress = ProgressFromPointer(args);
            RenderProgress(_dragProgress);
            UpdateDraggedPositionText();
        };

        ProgressTrack.PointerReleased += (_, args) =>
        {
            ProgressTrack.ReleasePointerCapture(args.Pointer);
            CommitSeek();
        };

        ProgressTrack.PointerCaptureLost += (_, _) => CommitSeek();

        // O traço só ganha largura depois do primeiro layout; sem isto o preenchimento
        // ficaria em zero até o próximo tique do relógio.
        ProgressTrack.SizeChanged += (_, _) => UpdateProgress();

        Island.PointerPressed += (_, _) =>
        {
            if (!_viewModel.IsExpanded)
            {
                _viewModel.SetExpanded(true);
            }
        };
    }

    private void UpdateDraggedPositionText()
    {
        var duration = _viewModel.Media.CurrentSnapshot?.Duration ?? TimeSpan.Zero;
        PositionText.Text = FormatTime(TimeSpan.FromSeconds(duration.TotalSeconds * _dragProgress));
    }

    private void CommitSeek()
    {
        if (!_isDraggingProgress)
        {
            return;
        }

        _isDraggingProgress = false;

        var snapshot = _viewModel.Media.CurrentSnapshot;
        if (snapshot is null || snapshot.Duration <= TimeSpan.Zero)
        {
            return;
        }

        Perform(PlayerCommand.Seek(TimeSpan.FromSeconds(snapshot.Duration.TotalSeconds * _dragProgress)));
    }

    private void Perform(PlayerCommand command) => _ = _viewModel.Media.PerformAsync(command);

    public void Teardown()
    {
        _animationTimer.Stop();
        _progressTimer.Stop();
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Media.PropertyChanged -= OnMediaChanged;
        _viewModel.Notifications.PropertyChanged -= OnNotificationsChanged;
    }
}
