import CoreGraphics

/// Dimensões da ilha. É um valor puro para permitir testes sem AppKit.
struct NotchGeometry: Equatable, Sendable {
    enum DisplayKind: String, Equatable, Sendable {
        case builtIn
        case external
    }

    enum ClosedStyle: String, Equatable, Sendable {
        /// Réplica do notch de hardware, usada na tela do Mac.
        case notch
        /// Tira mínima usada em monitores externos.
        case sliver
    }

    /// Área da janela que hospeda a ilha. Precisa acomodar o painel expandido e a sombra.
    static let windowSize = CGSize(width: 520, height: 224)

    static let expandedContentHeight: CGFloat = 142
    static let mediaColumnWidth: CGFloat = 256
    static let calendarColumnWidth: CGFloat = 176
    static let columnSpacing: CGFloat = 10
    static let horizontalPadding: CGFloat = 12
    static let bottomPadding: CGFloat = 9
    static let dividerWidth: CGFloat = 1
    static let sliverSize = CGSize(width: 132, height: 9)
    static let externalTopPadding: CGFloat = 26

    let displayKind: DisplayKind
    let closedStyle: ClosedStyle
    let closedSize: CGSize
    let expandedTopPadding: CGFloat

    var expandedSize: CGSize {
        CGSize(
            width: Self.horizontalPadding * 2
                + Self.mediaColumnWidth
                + Self.columnSpacing * 2
                + Self.dividerWidth
                + Self.calendarColumnWidth,
            height: Self.expandedContentHeight + expandedTopPadding + Self.bottomPadding
        )
    }

    var closedCornerRadius: CGFloat {
        switch closedStyle {
        case .notch: 9
        case .sliver: 5
        }
    }

    var expandedCornerRadius: CGFloat { 18 }

    var isMinimized: Bool { closedStyle == .sliver }

    static func make(
        displayKind: DisplayKind,
        hardwareNotchWidth: CGFloat?,
        menuBarHeight: CGFloat,
        minimizeOnExternalDisplays: Bool
    ) -> NotchGeometry {
        if displayKind == .external, minimizeOnExternalDisplays {
            return NotchGeometry(
                displayKind: .external,
                closedStyle: .sliver,
                closedSize: sliverSize,
                expandedTopPadding: externalTopPadding
            )
        }

        let width: CGFloat
        if let hardwareNotchWidth, hardwareNotchWidth > 120 {
            width = min(max(hardwareNotchWidth, 150), 176)
        } else {
            width = 156
        }

        let height: CGFloat
        switch displayKind {
        case .builtIn:
            height = min(max(menuBarHeight, 28), 32)
        case .external:
            height = min(max(menuBarHeight, 24), 28)
        }

        return NotchGeometry(
            displayKind: displayKind,
            closedStyle: .notch,
            closedSize: CGSize(width: width, height: height),
            expandedTopPadding: height + 4
        )
    }
}
