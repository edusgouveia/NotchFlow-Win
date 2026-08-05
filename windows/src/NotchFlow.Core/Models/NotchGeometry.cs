namespace NotchFlow.Core.Models;

/// <summary>Tipo de monitor. No Windows nenhuma tela tem notch de hardware, então a
/// distinção que importa é entre a tela principal e as secundárias.</summary>
public enum DisplayKind
{
    Primary,
    Secondary
}

public enum ClosedStyle
{
    /// <summary>Ilha completa, usada na tela principal.</summary>
    Notch,

    /// <summary>Tira mínima usada nas telas secundárias.</summary>
    Sliver
}

/// <summary>
/// Dimensões da ilha. É um valor puro, sem dependência de XAML ou Win32, para permitir testes.
/// As constantes vêm do NotchFlow para macOS e foram preservadas para o desenho carregar idêntico.
/// </summary>
/// <summary>
/// O que a ilha recolhida está mostrando. Define a largura dela, para não reservar espaço
/// que não usa.
/// </summary>
public readonly record struct ClosedContent(
    bool HasArtwork,
    bool HasMedia,
    int NotificationCount)
{
    public static readonly ClosedContent Idle = new(false, false, 0);
}

public sealed record NotchGeometry
{
    /// <summary>Área da janela que hospeda a ilha. Precisa acomodar o painel expandido e a sombra.</summary>
    public const double WindowWidth = 520;
    public const double WindowHeight = 224;

    public const double ExpandedContentHeight = 142;
    public const double MediaColumnWidth = 256;
    public const double CalendarColumnWidth = 176;
    public const double NotificationColumnWidth = 192;
    public const double ColumnSpacing = 10;
    public const double HorizontalPadding = 12;
    public const double BottomPadding = 9;
    public const double DividerWidth = 1;

    public const double SliverWidth = 132;
    public const double SliverHeight = 9;

    /// <summary>Área mínima de interação da tira. O visual continua fino, mas hover e clique
    /// precisam de uma região confortável para funcionar no limite superior da tela.</summary>
    public const double SliverInteractionWidth = 180;
    public const double SliverInteractionHeight = 24;

    public const double SecondaryTopPadding = 26;

    /// <summary>Altura da ilha recolhida. No macOS derivava da barra de menus; o Windows não tem
    /// barra no topo, então usamos alturas fixas.</summary>
    private const double PrimaryClosedHeight = 26;
    private const double SecondaryClosedHeight = 24;

    // Medidas dos elementos que a ilha recolhida mostra. A largura sai da soma do que
    // está visível: no macOS ela era fixa em 156 para imitar a notch de hardware, e no
    // Windows, sem notch para imitar, largura fixa seria só espaço preto desperdiçado.
    private const double ClosedPadding = 6;
    private const double ClosedArtworkSize = 16;
    private const double ClosedStatusWidth = 12;
    private const double ClosedBadgeWidth = 16;

    /// <summary>O contador fica mais largo ao passar de um dígito, virando "9+".</summary>
    private const double ClosedWideBadgeWidth = 22;

    private const double ClosedChipSpacing = 5;

    /// <summary>Largura da ilha ociosa, que mostra apenas a cápsula neutra. É também o
    /// piso das outras combinações, para a ilha nunca virar um ponto perdido no topo.</summary>
    private const double ClosedIdleWidth = 38;

    /// <summary>Largura mínima da região que responde ao ponteiro. A ilha encolheu, mas
    /// mirar nela não pode ficar difícil.</summary>
    private const double ClosedInteractionMinimumWidth = 96;

    public required DisplayKind DisplayKind { get; init; }
    public required ClosedStyle ClosedStyle { get; init; }
    public required double ClosedWidthValue { get; init; }
    public required double ClosedHeightValue { get; init; }
    public required double ExpandedTopPadding { get; init; }

    /// <summary>O painel do calendário ainda não existe no Windows. Enquanto não existir,
    /// a ilha expande só com a coluna de mídia em vez de abrir um espaço vazio.</summary>
    public required bool IncludesCalendar { get; init; }

    /// <summary>A coluna de notificações aparece só quando há algo a mostrar, para a ilha
    /// ociosa continuar compacta em vez de reservar um espaço vazio permanente.</summary>
    public required bool IncludesNotifications { get; init; }

    public double ExpandedWidth =>
        HorizontalPadding * 2
        + MediaColumnWidth
        + Column(IncludesNotifications, NotificationColumnWidth)
        + Column(IncludesCalendar, CalendarColumnWidth);

    /// <summary>Largura que uma coluna extra acrescenta, já com o espaçamento e o divisor.</summary>
    private static double Column(bool present, double width)
        => present ? ColumnSpacing * 2 + DividerWidth + width : 0;

    public double ExpandedHeight => ExpandedContentHeight + ExpandedTopPadding + BottomPadding;

    public double ClosedCornerRadius => ClosedStyle switch
    {
        ClosedStyle.Notch => 9,
        _ => 5
    };

    public double ExpandedCornerRadius => 18;

    public bool IsMinimized => ClosedStyle == ClosedStyle.Sliver;

    /// <summary>Região que responde ao ponteiro. É maior que o visual nos dois estilos:
    /// a tira tem 9 pixels de altura e a ilha encolhe conforme o conteúdo, então em ambos
    /// mirar só no desenho seria desconfortável.</summary>
    public double ClosedInteractionWidth => IsMinimized
        ? Math.Max(ClosedWidthValue, SliverInteractionWidth)
        : Math.Max(ClosedWidthValue, ClosedInteractionMinimumWidth);

    public double ClosedInteractionHeight =>
        IsMinimized ? Math.Max(ClosedHeightValue, SliverInteractionHeight) : ClosedHeightValue;

    /// <summary>
    /// Largura da ilha recolhida, somando só o que está visível.
    /// </summary>
    private static double MeasureClosedWidth(ClosedContent content)
    {
        var total = 0.0;
        var chips = 0;

        void Add(double largura)
        {
            total += largura;
            chips++;
        }

        if (content.HasArtwork)
        {
            Add(ClosedArtworkSize);
        }

        if (content.HasMedia)
        {
            Add(ClosedStatusWidth);
        }

        if (content.NotificationCount > 0)
        {
            Add(content.NotificationCount > 9 ? ClosedWideBadgeWidth : ClosedBadgeWidth);
        }

        if (chips == 0)
        {
            return ClosedIdleWidth;
        }

        var largura = ClosedPadding * 2 + total + ClosedChipSpacing * (chips - 1);
        return Math.Max(largura, ClosedIdleWidth);
    }

    public static NotchGeometry Make(
        DisplayKind displayKind,
        bool minimizeOnSecondaryDisplays,
        bool includesCalendar = false,
        bool includesNotifications = false,
        ClosedContent closedContent = default)
    {
        if (displayKind == DisplayKind.Secondary && minimizeOnSecondaryDisplays)
        {
            return new NotchGeometry
            {
                DisplayKind = DisplayKind.Secondary,
                ClosedStyle = ClosedStyle.Sliver,
                ClosedWidthValue = SliverWidth,
                ClosedHeightValue = SliverHeight,
                ExpandedTopPadding = SecondaryTopPadding,
                IncludesCalendar = includesCalendar,
                IncludesNotifications = includesNotifications
            };
        }

        var height = displayKind == DisplayKind.Primary
            ? PrimaryClosedHeight
            : SecondaryClosedHeight;

        return new NotchGeometry
        {
            DisplayKind = displayKind,
            ClosedStyle = ClosedStyle.Notch,
            ClosedWidthValue = MeasureClosedWidth(closedContent),
            ClosedHeightValue = height,
            ExpandedTopPadding = height + 4,
            IncludesCalendar = includesCalendar,
            IncludesNotifications = includesNotifications
        };
    }
}
