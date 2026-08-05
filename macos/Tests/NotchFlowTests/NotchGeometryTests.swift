import CoreGraphics
import Testing
@testable import NotchFlow

@Suite("Geometria da ilha")
struct NotchGeometryTests {
    @Test("Tela externa fica reduzida a uma tira")
    func externalDisplayIsMinimized() {
        let geometry = NotchGeometry.make(
            displayKind: .external,
            hardwareNotchWidth: nil,
            menuBarHeight: 24,
            minimizeOnExternalDisplays: true
        )

        #expect(geometry.closedStyle == .sliver)
        #expect(geometry.closedSize == NotchGeometry.sliverSize)
        #expect(geometry.isMinimized)
        #expect(geometry.closedInteractionSize.width >= 180)
        #expect(geometry.closedInteractionSize.height >= 24)
        #expect(geometry.closedInteractionSize.width > geometry.closedSize.width)
        #expect(geometry.closedInteractionSize.height > geometry.closedSize.height)
    }

    @Test("Tela externa pode manter a ilha inteira")
    func externalDisplayCanKeepIsland() {
        let geometry = NotchGeometry.make(
            displayKind: .external,
            hardwareNotchWidth: nil,
            menuBarHeight: 24,
            minimizeOnExternalDisplays: false
        )

        #expect(geometry.closedStyle == .notch)
        #expect(geometry.closedSize.width == 156)
        #expect(geometry.closedInteractionSize == geometry.closedSize)
        #expect(geometry.isMinimized == false)
    }

    @Test("A ilha do Mac acompanha o notch de hardware")
    func builtInFollowsHardwareNotch() {
        let geometry = NotchGeometry.make(
            displayKind: .builtIn,
            hardwareNotchWidth: 168,
            menuBarHeight: 37,
            minimizeOnExternalDisplays: true
        )

        #expect(geometry.closedSize.width == 168)
        #expect(geometry.closedSize.height == 32)
        #expect(geometry.expandedTopPadding == 36)
    }

    @Test("O painel expandido cabe na janela em qualquer tela")
    func expandedPanelFitsWindow() {
        for kind in [NotchGeometry.DisplayKind.builtIn, .external] {
            let geometry = NotchGeometry.make(
                displayKind: kind,
                hardwareNotchWidth: 176,
                menuBarHeight: 32,
                minimizeOnExternalDisplays: true
            )

            #expect(geometry.expandedSize.width == 477)
            #expect(geometry.expandedSize.width <= NotchGeometry.windowSize.width)
            #expect(geometry.expandedSize.height <= NotchGeometry.windowSize.height)
        }
    }

    @Test("A área de interação fica centralizada no topo da janela")
    func interactionAreaIsTopCentered() {
        let windowFrame = CGRect(x: 100, y: 200, width: 520, height: 224)
        let interactionSize = CGSize(width: 180, height: 24)

        let rect = NotchGeometry.interactionRect(in: windowFrame, size: interactionSize)

        #expect(rect == CGRect(x: 270, y: 400, width: 180, height: 24))
        #expect(rect.midX == windowFrame.midX)
        #expect(rect.maxY == windowFrame.maxY)
    }

    @Test("O painel novo é mais compacto que o anterior")
    func expandedPanelIsMoreCompact() {
        let geometry = NotchGeometry.make(
            displayKind: .builtIn,
            hardwareNotchWidth: 176,
            menuBarHeight: 32,
            minimizeOnExternalDisplays: true
        )

        #expect(geometry.expandedSize.width < 584)
        #expect(geometry.expandedSize.height < 218)
    }
}
