import AppKit
import SwiftUI

struct NotchView: View {
    @ObservedObject var viewModel: NotchFlowViewModel
    @State private var showsAbout = false

    private let actionSize: CGFloat = 22

    private var geometry: NotchGeometry { viewModel.geometry }

    private var visualSize: CGSize {
        viewModel.isExpanded ? geometry.expandedSize : geometry.closedSize
    }

    private var interactionSize: CGSize {
        viewModel.isExpanded ? geometry.expandedSize : geometry.closedInteractionSize
    }

    var body: some View {
        VStack(spacing: 0) {
            ZStack(alignment: .top) {
                ZStack(alignment: .top) {
                    UnevenRoundedRectangle(
                        topLeadingRadius: 0,
                        bottomLeadingRadius: bottomRadius,
                        bottomTrailingRadius: bottomRadius,
                        topTrailingRadius: 0,
                        style: .continuous
                    )
                    .fill(.black)
                    .shadow(
                        color: viewModel.isExpanded ? .black.opacity(0.48) : .clear,
                        radius: 14,
                        y: 8
                    )

                    if viewModel.showsContent {
                        expandedContent
                            .transition(.opacity)
                    } else if !viewModel.isExpanded {
                        ClosedNotchView(media: viewModel.media, style: geometry.closedStyle)
                            .transition(.opacity)
                    }
                }
                .frame(width: visualSize.width, height: visualSize.height, alignment: .top)
            }
            .frame(width: interactionSize.width, height: interactionSize.height, alignment: .top)
            // Uma opacidade quase nula garante hit testing também na parte invisível da área.
            .background(Color.black.opacity(0.001))
            .contentShape(Rectangle())
            .onTapGesture {
                if !viewModel.isExpanded {
                    viewModel.setExpanded(true)
                }
            }
            .overlay(alignment: .topTrailing) {
                if viewModel.showsContent {
                    appActions
                        .padding(.top, actionTopPadding)
                        .padding(.trailing, 10)
                        .transition(.opacity)
                }
            }
            .contextMenu { menuItems }
            .animation(NotchAnimation.shape, value: viewModel.isExpanded)
            .onChange(of: viewModel.isExpanded) { _, isExpanded in
                if !isExpanded {
                    showsAbout = false
                }
            }

            Spacer(minLength: 0)
        }
        .frame(
            width: NotchGeometry.windowSize.width,
            height: NotchGeometry.windowSize.height,
            alignment: .top
        )
        .preferredColorScheme(.dark)
    }

    private var bottomRadius: CGFloat {
        viewModel.isExpanded ? geometry.expandedCornerRadius : geometry.closedCornerRadius
    }

    private var actionTopPadding: CGFloat {
        max((geometry.expandedTopPadding - actionSize) / 2, 3)
    }

    @ViewBuilder
    private var menuItems: some View {
        Button("Recolher painel") {
            collapse()
        }
        .disabled(!viewModel.isExpanded)

        Button("Atualizar mídia e calendário") {
            Task {
                viewModel.media.resetBrowserDiagnostics()
                await viewModel.media.refreshAll()
                await viewModel.calendar.refresh()
            }
        }

        Divider()

        Toggle("Controlar mídia do navegador", isOn: browserIntegrationBinding)
        Toggle("Mostrar ícone na barra de menus", isOn: menuBarIconBinding)

        Divider()

        SettingsLink {
            Text("Ajustes…")
        }

        Divider()

        Button("Encerrar NotchFlow", role: .destructive) {
            quitApplication()
        }
    }

    private var browserIntegrationBinding: Binding<Bool> {
        let settings = viewModel.settings
        let media = viewModel.media

        return Binding(
            get: { settings.browserIntegrationEnabled },
            set: { newValue in
                settings.browserIntegrationEnabled = newValue
                Task { @MainActor in
                    media.resetBrowserDiagnostics()
                    await media.refreshAll()
                }
            }
        )
    }

    private var menuBarIconBinding: Binding<Bool> {
        let settings = viewModel.settings

        return Binding(
            get: { settings.showMenuBarIcon },
            set: { settings.showMenuBarIcon = $0 }
        )
    }

    private var appActions: some View {
        HStack(spacing: 5) {
            actionButton(
                symbol: showsAbout ? "info.circle.fill" : "info.circle",
                label: "Sobre o NotchFlow",
                background: showsAbout ? Color.gray.opacity(0.34) : Color.white.opacity(0.1),
                foreground: Color.white,
                action: { showsAbout.toggle() }
            )

            actionButton(
                symbol: "chevron.up",
                label: "Recolher painel",
                background: Color.white.opacity(0.1),
                foreground: Color.white,
                action: collapse
            )

            actionButton(
                symbol: "power",
                label: "Encerrar NotchFlow",
                background: Color.red.opacity(0.18),
                foreground: Color.red.opacity(0.9),
                action: quitApplication
            )
        }
    }

    private func actionButton(
        symbol: String,
        label: String,
        background: Color,
        foreground: Color,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            Image(systemName: symbol)
                .font(.system(size: 9, weight: .bold))
                .frame(width: actionSize, height: actionSize)
                .background(background, in: Circle())
                .foregroundStyle(foreground)
        }
        .buttonStyle(.plain)
        .help(label)
        .accessibilityLabel(label)
    }

    private var expandedContent: some View {
        Group {
            if showsAbout {
                AboutPanelView {
                    showsAbout = false
                }
                .frame(height: NotchGeometry.expandedContentHeight)
                .transition(.opacity.combined(with: .scale(scale: 0.97)))
            } else {
                HStack(alignment: .top, spacing: NotchGeometry.columnSpacing) {
                    MediaPanelView(coordinator: viewModel.media)
                        .frame(
                            width: NotchGeometry.mediaColumnWidth,
                            height: NotchGeometry.expandedContentHeight
                        )

                    Rectangle()
                        .fill(.white.opacity(0.12))
                        .frame(width: NotchGeometry.dividerWidth)

                    CalendarPanelView(calendar: viewModel.calendar)
                        .frame(
                            width: NotchGeometry.calendarColumnWidth,
                            height: NotchGeometry.expandedContentHeight
                        )
                }
                .transition(.opacity)
            }
        }
        .padding(.horizontal, NotchGeometry.horizontalPadding)
        .padding(.top, geometry.expandedTopPadding)
        .padding(.bottom, NotchGeometry.bottomPadding)
        .animation(.easeInOut(duration: 0.16), value: showsAbout)
    }

    private func collapse() {
        viewModel.setExpanded(false)
    }

    private func quitApplication() {
        NSApplication.shared.terminate(nil)
    }
}

private struct ClosedNotchView: View {
    @ObservedObject var media: MediaCoordinator
    let style: NotchGeometry.ClosedStyle

    var body: some View {
        switch style {
        case .notch:
            notchContent
        case .sliver:
            sliverContent
        }
    }

    private var notchContent: some View {
        HStack(spacing: 6) {
            if let snapshot = media.currentSnapshot {
                ArtworkView(snapshot: snapshot, size: 18)
                Spacer(minLength: 8)
                Image(systemName: snapshot.isPlaying ? "waveform" : "pause.fill")
                    .font(.system(size: 10, weight: .semibold))
                    .foregroundStyle(sourceColor(for: snapshot))
            }
        }
        .padding(.horizontal, 7)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    /// Em telas externas fica apenas uma tira, que serve de alça para abrir o painel.
    private var sliverContent: some View {
        Capsule()
            .fill(indicatorColor)
            .frame(width: 26, height: 2.5)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var indicatorColor: Color {
        guard let snapshot = media.currentSnapshot else { return .gray.opacity(0.62) }
        return sourceColor(for: snapshot)
    }

    private func sourceColor(for snapshot: PlaybackSnapshot) -> Color {
        switch snapshot.playbackBrand {
        case .spotify:
            Color(red: 0.12, green: 0.84, blue: 0.38)
        case .youtube:
            Color(red: 1, green: 0.16, blue: 0.14)
        case .appleMusic:
            Color(red: 1, green: 0.48, blue: 0.12)
        case .neutral:
            Color.gray.opacity(0.72)
        }
    }
}

struct MediaPanelView: View {
    @ObservedObject var coordinator: MediaCoordinator

    var body: some View {
        Group {
            if let snapshot = coordinator.currentSnapshot {
                player(snapshot)
            } else {
                emptyState
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
    }

    private func player(_ snapshot: PlaybackSnapshot) -> some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack(spacing: 9) {
                ArtworkView(snapshot: snapshot, size: 58)

                VStack(alignment: .leading, spacing: 2) {
                    HStack(spacing: 3) {
                        Image(systemName: snapshot.source.symbolName)
                            .font(.system(size: 7, weight: .bold))
                        Text(snapshot.sourceLabel)
                            .font(.system(size: 8.5, weight: .semibold))
                            .lineLimit(1)
                    }
                    .foregroundStyle(accent(for: snapshot))

                    Text(snapshot.title)
                        .font(.system(size: 12, weight: .semibold))
                        .lineLimit(1)

                    Text(subtitle(for: snapshot))
                        .font(.system(size: 10))
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }

                Spacer(minLength: 0)
            }

            TimelineView(.periodic(from: .now, by: 1)) { timeline in
                PlaybackProgressView(snapshot: snapshot, date: timeline.date) { position in
                    Task { await coordinator.perform(.seek(to: position)) }
                }
            }

            Spacer(minLength: 0)

            HStack(spacing: 8) {
                mediaButton("gobackward.15", label: "Voltar 15 segundos", enabled: snapshot.supportsSeek) {
                    await coordinator.perform(.skip(seconds: -15))
                }
                mediaButton("backward.fill", label: "Faixa anterior", enabled: snapshot.supportsTrackSkip) {
                    await coordinator.perform(.previousTrack)
                }
                mediaButton(
                    snapshot.isPlaying ? "pause.fill" : "play.fill",
                    label: "Play ou pause",
                    prominent: true
                ) {
                    await coordinator.perform(.togglePlayPause)
                }
                mediaButton("forward.fill", label: "Próxima faixa", enabled: snapshot.supportsTrackSkip) {
                    await coordinator.perform(.nextTrack)
                }
                mediaButton("goforward.15", label: "Avançar 15 segundos", enabled: snapshot.supportsSeek) {
                    await coordinator.perform(.skip(seconds: 15))
                }
            }
            .frame(maxWidth: .infinity)
        }
    }

    private var emptyState: some View {
        VStack(spacing: 6) {
            Image(systemName: "music.note.list")
                .font(.system(size: 22, weight: .light))
                .foregroundStyle(.secondary)

            Text("Nada tocando")
                .font(.system(size: 12, weight: .semibold))

            Text(coordinator.browserHint ?? "Abra o Apple Music, o Spotify ou um site com áudio ou vídeo.")
                .font(.system(size: 9.5))
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
                .lineLimit(3)

            Button("Atualizar") {
                Task {
                    coordinator.resetBrowserDiagnostics()
                    await coordinator.refreshAll()
                }
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
        }
        .padding(.horizontal, 8)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func subtitle(for snapshot: PlaybackSnapshot) -> String {
        if !snapshot.artist.isEmpty { return snapshot.artist }
        if !snapshot.album.isEmpty { return snapshot.album }
        return snapshot.source == .browser ? "Reproduzindo no navegador" : ""
    }

    private func accent(for snapshot: PlaybackSnapshot) -> Color {
        switch snapshot.source {
        case .spotify: .green
        case .appleMusic: .pink
        case .browser: .red
        }
    }

    private func mediaButton(
        _ symbol: String,
        label: String,
        prominent: Bool = false,
        enabled: Bool = true,
        action: @escaping @MainActor () async -> Void
    ) -> some View {
        Button {
            Task { await action() }
        } label: {
            Image(systemName: symbol)
                .font(.system(size: prominent ? 15 : 11, weight: .semibold))
                .frame(width: prominent ? 30 : 25, height: prominent ? 30 : 25)
                .background(prominent ? Color.white : Color.white.opacity(0.09))
                .foregroundStyle(prominent ? Color.black : Color.white)
                .clipShape(Circle())
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
        .opacity(enabled ? 1 : 0.3)
        .help(label)
        .accessibilityLabel(label)
    }
}

private struct ArtworkView: View {
    let snapshot: PlaybackSnapshot
    let size: CGFloat

    var body: some View {
        Group {
            if let data = snapshot.artworkData,
               let image = NSImage(data: data) {
                Image(nsImage: image)
                    .resizable()
                    .scaledToFill()
            } else {
                ZStack {
                    LinearGradient(
                        colors: gradientColors,
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    )
                    Image(systemName: snapshot.source.symbolName)
                        .font(.system(size: size * 0.34, weight: .semibold))
                        .foregroundStyle(.white)
                }
            }
        }
        .frame(width: size, height: size)
        .clipShape(RoundedRectangle(cornerRadius: max(size * 0.16, 4), style: .continuous))
    }

    private var gradientColors: [Color] {
        switch snapshot.source {
        case .spotify: [.green.opacity(0.8), .black]
        case .appleMusic: [.orange.opacity(0.92), .red.opacity(0.68)]
        case .browser: [.red.opacity(0.85), .black]
        }
    }
}

/// Barra de progresso arrastável. O valor arrastado tem prioridade sobre a posição lida do player
/// até chegar uma leitura nova, evitando que o indicador pule de volta.
private struct PlaybackProgressView: View {
    let snapshot: PlaybackSnapshot
    let date: Date
    let onSeek: (TimeInterval) -> Void

    @State private var dragProgress: Double?
    @State private var isDragging = false

    private let trackHeight: CGFloat = 3
    private let hitAreaHeight: CGFloat = 13

    private var canSeek: Bool { snapshot.supportsSeek && snapshot.duration > 0 }

    var body: some View {
        let livePosition = snapshot.effectivePosition(at: date)
        let liveProgress = snapshot.duration > 0 ? livePosition / snapshot.duration : 0
        let progress = min(max(dragProgress ?? liveProgress, 0), 1)
        let displayedPosition = dragProgress.map { $0 * snapshot.duration } ?? livePosition

        VStack(spacing: 2) {
            GeometryReader { proxy in
                let width = proxy.size.width
                let knobSize: CGFloat = isDragging ? 9 : 7

                ZStack(alignment: .leading) {
                    Capsule()
                        .fill(.white.opacity(0.14))
                        .frame(height: trackHeight)

                    Capsule()
                        .fill(.white.opacity(0.85))
                        .frame(width: width * progress, height: trackHeight)

                    if canSeek {
                        Circle()
                            .fill(.white)
                            .frame(width: knobSize, height: knobSize)
                            .offset(x: min(max(width * progress - knobSize / 2, 0), max(width - knobSize, 0)))
                    }
                }
                .frame(width: width, height: proxy.size.height)
                .contentShape(Rectangle())
                .gesture(dragGesture(width: width))
            }
            .frame(height: hitAreaHeight)

            HStack {
                Text(formatTime(displayedPosition))
                Spacer()
                Text(formatTime(snapshot.duration))
            }
            .font(.system(size: 8.5).monospacedDigit())
            .foregroundStyle(.secondary)
        }
        .onChange(of: snapshot.capturedAt) { _, _ in
            guard !isDragging else { return }
            dragProgress = nil
        }
    }

    private func dragGesture(width: CGFloat) -> some Gesture {
        DragGesture(minimumDistance: 0)
            .onChanged { value in
                guard canSeek, width > 0 else { return }
                isDragging = true
                dragProgress = min(max(value.location.x / width, 0), 1)
            }
            .onEnded { value in
                guard canSeek, width > 0 else { return }
                let target = min(max(value.location.x / width, 0), 1)
                dragProgress = target
                isDragging = false
                onSeek(target * snapshot.duration)
            }
    }

    private func formatTime(_ seconds: TimeInterval) -> String {
        guard seconds.isFinite, seconds >= 0 else { return "0:00" }
        let total = Int(seconds)
        if total >= 3_600 {
            return String(format: "%d:%02d:%02d", total / 3_600, (total % 3_600) / 60, total % 60)
        }
        return String(format: "%d:%02d", total / 60, total % 60)
    }
}
