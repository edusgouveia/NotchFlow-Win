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
    /// barra no topo, então usamos alturas fixas equivalentes aos limites de lá.</summary>
    private const double PrimaryClosedHeight = 32;
    private const double SecondaryClosedHeight = 28;
    private const double ClosedWidth = 156;

    public required DisplayKind DisplayKind { get; init; }
    public required ClosedStyle ClosedStyle { get; init; }
    public required double ClosedWidthValue { get; init; }
    public required double ClosedHeightValue { get; init; }
    public required double ExpandedTopPadding { get; init; }

    /// <summary>O painel do calendário ainda não existe no Windows. Enquanto não existir,
    /// a ilha expande só com a coluna de mídia em vez de abrir um espaço vazio.</summary>
    public required bool IncludesCalendar { get; init; }

    public double ExpandedWidth =>
        HorizontalPadding * 2
        + MediaColumnWidth
        + (IncludesCalendar ? ColumnSpacing * 2 + DividerWidth + CalendarColumnWidth : 0);

    public double ExpandedHeight => ExpandedContentHeight + ExpandedTopPadding + BottomPadding;

    public double ClosedCornerRadius => ClosedStyle switch
    {
        ClosedStyle.Notch => 9,
        _ => 5
    };

    public double ExpandedCornerRadius => 18;

    public bool IsMinimized => ClosedStyle == ClosedStyle.Sliver;

    public double ClosedInteractionWidth =>
        IsMinimized ? Math.Max(ClosedWidthValue, SliverInteractionWidth) : ClosedWidthValue;

    public double ClosedInteractionHeight =>
        IsMinimized ? Math.Max(ClosedHeightValue, SliverInteractionHeight) : ClosedHeightValue;

    public static NotchGeometry Make(
        DisplayKind displayKind,
        bool minimizeOnSecondaryDisplays,
        bool includesCalendar = false)
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
                IncludesCalendar = includesCalendar
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
            ExpandedTopPadding = height + 4,
            IncludesCalendar = includesCalendar
        };
    }
}
