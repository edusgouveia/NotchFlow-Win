import AppKit
import SwiftUI

struct NotchView: View {
    @ObservedObject var viewModel: NotchFlowViewModel
    @State private var hoverTask: Task<Void, Never>?

    private let notchSpring = Animation.spring(
        response: 0.46,
        dampingFraction: 0.82,
        blendDuration: 0.08
    )

    var body: some View {
        VStack(spacing: 0) {
            ZStack(alignment: .top) {
                UnevenRoundedRectangle(
                    topLeadingRadius: 0,
                    bottomLeadingRadius: viewModel.isExpanded ? 20 : 9,
                    bottomTrailingRadius: viewModel.isExpanded ? 20 : 9,
                    topTrailingRadius: 0,
                    style: .continuous
                )
                .fill(.black)
                .shadow(
                    color: viewModel.isExpanded ? .black.opacity(0.48) : .clear,
                    radius: 14,
                    y: 8
                )

                if viewModel.isExpanded {
                    expandedContent
                        .transition(
                            .asymmetric(
                                insertion: .opacity
                                    .combined(with: .scale(scale: 0.96, anchor: .top))
                                    .combined(with: .offset(y: -7))
                                    .animation(.easeOut(duration: 0.2).delay(0.1)),
                                removal: .opacity.animation(.easeOut(duration: 0.1))
                            )
                        )
                } else {
                    ClosedNotchView(media: viewModel.media)
                        .transition(.opacity.animation(.easeIn(duration: 0.14).delay(0.08)))
                }
            }
            .frame(
                width: viewModel.isExpanded ? 584 : viewModel.closedSize.width,
                height: viewModel.isExpanded ? 218 : viewModel.closedSize.height,
                alignment: .top
            )
            .contentShape(Rectangle())
            .onHover(perform: handleHover)
            .onTapGesture {
                if !viewModel.isExpanded {
                    withAnimation(notchSpring) {
                        viewModel.isExpanded = true
                    }
                }
            }
            .overlay(alignment: .topTrailing) {
                if viewModel.isExpanded {
                    appActions
                        .padding(.top, 8)
                        .padding(.trailing, 11)
                        .transition(.opacity.animation(.easeOut(duration: 0.16).delay(0.14)))
                }
            }
            .contextMenu {
                Button("Recolher painel") {
                    collapse()
                }
                .disabled(!viewModel.isExpanded)

                Divider()

                Button("Encerrar NotchFlow", role: .destructive) {
                    quitApplication()
                }
            }
            .animation(notchSpring, value: viewModel.isExpanded)

            Spacer(minLength: 0)
        }
        .frame(width: 628, height: 252, alignment: .top)
        .preferredColorScheme(.dark)
    }

    private var appActions: some View {
        HStack(spacing: 5) {
            Button(action: collapse) {
                Image(systemName: "chevron.up")
                    .font(.system(size: 10, weight: .bold))
                    .frame(width: 24, height: 24)
                    .background(.white.opacity(0.1), in: Circle())
            }
            .buttonStyle(.plain)
            .help("Recolher painel")
            .accessibilityLabel("Recolher painel")

            Button(action: quitApplication) {
                Image(systemName: "power")
                    .font(.system(size: 10, weight: .bold))
                    .frame(width: 24, height: 24)
                    .background(.red.opacity(0.18), in: Circle())
                    .foregroundStyle(.red.opacity(0.9))
            }
            .buttonStyle(.plain)
            .help("Encerrar NotchFlow")
            .accessibilityLabel("Encerrar NotchFlow")
        }
    }

    private var expandedContent: some View {
        HStack(alignment: .top, spacing: 14) {
            MediaPanelView(coordinator: viewModel.media)
                .frame(width: 310)

            Divider()
                .overlay(.white.opacity(0.12))
                .padding(.vertical, 4)

            CalendarPanelView(calendar: viewModel.calendar)
                .frame(width: 214)
        }
        .padding(.horizontal, 14)
        .padding(.top, 31)
        .padding(.bottom, 10)
    }

    private func handleHover(_ isHovering: Bool) {
        hoverTask?.cancel()

        hoverTask = Task { @MainActor in
            let delay: Duration = isHovering ? .milliseconds(120) : .milliseconds(480)
            try? await Task.sleep(for: delay)
            guard !Task.isCancelled else { return }

            withAnimation(notchSpring) {
                viewModel.isExpanded = isHovering
            }
        }
    }

    private func collapse() {
        hoverTask?.cancel()
        withAnimation(notchSpring) {
            viewModel.isExpanded = false
        }
    }

    private func quitApplication() {
        NSApplication.shared.terminate(nil)
    }
}

private struct ClosedNotchView: View {
    @ObservedObject var media: MediaCoordinator

    var body: some View {
        HStack(spacing: 6) {
            if let snapshot = media.currentSnapshot {
                ArtworkView(snapshot: snapshot, size: 18)
                Spacer(minLength: 8)
                Image(systemName: snapshot.isPlaying ? "waveform" : "pause.fill")
                    .font(.system(size: 10, weight: .semibold))
                    .foregroundStyle(snapshot.isPlaying ? .green : .white.opacity(0.65))
            }
        }
        .padding(.horizontal, 7)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
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
    }

    private func player(_ snapshot: PlaybackSnapshot) -> some View {
        VStack(spacing: 12) {
            HStack(spacing: 13) {
                ArtworkView(snapshot: snapshot, size: 72)

                VStack(alignment: .leading, spacing: 4) {
                    Label(snapshot.source.displayName, systemImage: snapshot.source.symbolName)
                        .font(.caption2.weight(.semibold))
                        .foregroundStyle(snapshot.source == .spotify ? .green : .pink)

                    Text(snapshot.title)
                        .font(.headline)
                        .lineLimit(1)

                    Text(snapshot.artist.isEmpty ? snapshot.album : snapshot.artist)
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }

                Spacer(minLength: 0)
            }

            TimelineView(.periodic(from: .now, by: 1)) { timeline in
                PlaybackProgressView(snapshot: snapshot, date: timeline.date)
            }

            HStack(spacing: 12) {
                mediaButton("gobackward.15", label: "Voltar 15 segundos") {
                    await coordinator.perform(.skip(seconds: -15))
                }
                mediaButton("backward.fill", label: "Faixa anterior") {
                    await coordinator.perform(.previousTrack)
                }
                mediaButton(snapshot.isPlaying ? "pause.fill" : "play.fill", label: "Play ou pause", prominent: true) {
                    await coordinator.perform(.togglePlayPause)
                }
                mediaButton("forward.fill", label: "Próxima faixa") {
                    await coordinator.perform(.nextTrack)
                }
                mediaButton("goforward.15", label: "Avançar 15 segundos") {
                    await coordinator.perform(.skip(seconds: 15))
                }
            }
        }
    }

    private var emptyState: some View {
        VStack(spacing: 12) {
            Image(systemName: "music.note.list")
                .font(.system(size: 32, weight: .light))
                .foregroundStyle(.secondary)
            Text("Nada tocando")
                .font(.headline)
            Text("Abra o Apple Music ou o Spotify")
                .font(.caption)
                .foregroundStyle(.secondary)
            Button("Atualizar") {
                Task { await coordinator.refreshAll() }
            }
            .buttonStyle(.bordered)
            .controlSize(.small)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func mediaButton(
        _ symbol: String,
        label: String,
        prominent: Bool = false,
        action: @escaping @MainActor () async -> Void
    ) -> some View {
        Button {
            Task { await action() }
        } label: {
            Image(systemName: symbol)
                .font(.system(size: prominent ? 17 : 13, weight: .semibold))
                .frame(width: prominent ? 34 : 28, height: prominent ? 34 : 28)
                .background(prominent ? Color.white : Color.white.opacity(0.09))
                .foregroundStyle(prominent ? Color.black : Color.white)
                .clipShape(Circle())
        }
        .buttonStyle(.plain)
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
                        colors: snapshot.source == .spotify
                            ? [.green.opacity(0.8), .black]
                            : [.pink.opacity(0.9), .purple.opacity(0.7)],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    )
                    Image(systemName: snapshot.source.symbolName)
                        .font(.system(size: size * 0.38, weight: .semibold))
                        .foregroundStyle(.white)
                }
            }
        }
        .frame(width: size, height: size)
        .clipShape(RoundedRectangle(cornerRadius: max(size * 0.16, 4), style: .continuous))
    }
}

private struct PlaybackProgressView: View {
    let snapshot: PlaybackSnapshot
    let date: Date

    var body: some View {
        let position = snapshot.effectivePosition(at: date)
        let progress = snapshot.duration > 0 ? position / snapshot.duration : 0

        VStack(spacing: 4) {
            GeometryReader { proxy in
                ZStack(alignment: .leading) {
                    Capsule().fill(.white.opacity(0.14))
                    Capsule()
                        .fill(.white.opacity(0.85))
                        .frame(width: proxy.size.width * min(max(progress, 0), 1))
                }
            }
            .frame(height: 4)

            HStack {
                Text(formatTime(position))
                Spacer()
                Text(formatTime(snapshot.duration))
            }
            .font(.caption2.monospacedDigit())
            .foregroundStyle(.secondary)
        }
    }

    private func formatTime(_ seconds: TimeInterval) -> String {
        guard seconds.isFinite, seconds >= 0 else { return "0:00" }
        let total = Int(seconds)
        return String(format: "%d:%02d", total / 60, total % 60)
    }
}
