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

    /// <summary>
    /// Tamanho da ilha recolhida. No macOS a largura era 156 para imitar a notch de hardware
    /// e a altura vinha da barra de menus. No Windows não há o que imitar, então a ilha é uma
    /// faixa mais discreta: comprida o bastante para ser uma alça reconhecível, e com a altura
    /// pouco maior que o contador de notificações que ela carrega.
    /// </summary>
    private const double ClosedWidth = 120;
    private const double PrimaryClosedHeight = 22;
    private const double SecondaryClosedHeight = 20;

    /// <summary>Faixa livre no topo do painel aberto. Acompanha a altura recolhida, mas com
    /// um piso: os botões de ação têm 22 pontos e precisam de folga para não ficarem colados
    /// na borda quando a ilha recolhida é baixa.</summary>
    private const double MinimumExpandedTopPadding = 30;

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

    /// <summary>Região que responde ao ponteiro. Na tira ela é maior que o visual, porque
    /// mirar em 9 pixels de altura seria impossível. Na ilha, que é larga o bastante,
    /// o alvo é o próprio desenho.</summary>
    public double ClosedInteractionWidth =>
        IsMinimized ? Math.Max(ClosedWidthValue, SliverInteractionWidth) : ClosedWidthValue;

    public double ClosedInteractionHeight =>
        IsMinimized ? Math.Max(ClosedHeightValue, SliverInteractionHeight) : ClosedHeightValue;

    public static NotchGeometry Make(
        DisplayKind displayKind,
        bool minimizeOnSecondaryDisplays,
        bool includesCalendar = false,
        bool includesNotifications = false)
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
            ClosedWidthValue = ClosedWidth,
            ClosedHeightValue = height,
            ExpandedTopPadding = Math.Max(height + 4, MinimumExpandedTopPadding),
            IncludesCalendar = includesCalendar,
            IncludesNotifications = includesNotifications
        };
    }
}
