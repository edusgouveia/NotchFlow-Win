@preconcurrency import AppKit
import Combine
import Foundation

@MainActor
final class MediaCoordinator: ObservableObject {
    @Published private(set) var currentSnapshot: PlaybackSnapshot?
    @Published private(set) var errorMessage: String?
    @Published private(set) var isRefreshing = false

    private let services: [PlayerSource: any MusicService]
    private var snapshots: [PlayerSource: PlaybackSnapshot] = [:]
    private var preferredSource: PlayerSource?
    private var observerTokens: [NSObjectProtocol] = []
    private var refreshTimer: Timer?

    init(
        appleMusicService: any MusicService = AppleMusicService(),
        spotifyService: any MusicService = SpotifyService()
    ) {
        services = [
            .appleMusic: appleMusicService,
            .spotify: spotifyService
        ]
    }

    func start() {
        guard observerTokens.isEmpty else { return }

        observeDistributedNotification("com.apple.Music.playerInfo", source: .appleMusic)
        observeDistributedNotification("com.spotify.client.PlaybackStateChanged", source: .spotify)

        for name in [NSWorkspace.didLaunchApplicationNotification, NSWorkspace.didTerminateApplicationNotification] {
            let token = NSWorkspace.shared.notificationCenter.addObserver(
                forName: name,
                object: nil,
                queue: .main
            ) { [weak self] _ in
                Task { @MainActor in
                    await self?.refreshAll()
                }
            }
            observerTokens.append(token)
        }

        refreshTimer = Timer.scheduledTimer(withTimeInterval: 5, repeats: true) { [weak self] _ in
            Task { @MainActor in
                await self?.refreshAll()
            }
        }

        Task { await refreshAll() }
    }

    func stop() {
        observerTokens.forEach { token in
            DistributedNotificationCenter.default().removeObserver(token)
            NSWorkspace.shared.notificationCenter.removeObserver(token)
        }
        observerTokens.removeAll()
        refreshTimer?.invalidate()
        refreshTimer = nil
    }

    func refreshAll() async {
        guard !isRefreshing else { return }
        isRefreshing = true
        defer { isRefreshing = false }

        for source in PlayerSource.allCases {
            await refresh(source: source, markPreferred: false)
        }
        applySelection()
    }

    func perform(_ command: PlayerCommand) async {
        guard let snapshot = currentSnapshot,
              let service = services[snapshot.source] else { return }

        do {
            try await service.perform(command, currentSnapshot: snapshot)
            errorMessage = nil
            try? await Task.sleep(for: .milliseconds(180))
            await refresh(source: snapshot.source, markPreferred: true)
        } catch {
            errorMessage = error.localizedDescription
            AppLog.media.error("Falha no comando de mídia: \(error.localizedDescription, privacy: .public)")
        }
    }

    private func observeDistributedNotification(_ rawName: String, source: PlayerSource) {
        let token = DistributedNotificationCenter.default().addObserver(
            forName: Notification.Name(rawName),
            object: nil,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                await self?.refresh(source: source, markPreferred: true)
            }
        }
        observerTokens.append(token)
    }

    private func refresh(source: PlayerSource, markPreferred: Bool) async {
        guard let service = services[source] else { return }

        guard service.isRunning() else {
            snapshots.removeValue(forKey: source)
            applySelection()
            return
        }

        do {
            if let snapshot = try await service.fetchSnapshot(), snapshot.hasTrack {
                snapshots[source] = snapshot
                if markPreferred || snapshot.isPlaying {
                    preferredSource = source
                }
            } else {
                snapshots.removeValue(forKey: source)
            }
            errorMessage = nil
        } catch {
            errorMessage = error.localizedDescription
            AppLog.media.error("Falha ao consultar \(source.displayName, privacy: .public): \(error.localizedDescription, privacy: .public)")
        }

        applySelection()
    }

    private func applySelection() {
        currentSnapshot = PlaybackSelector.select(
            from: snapshots,
            preferredSource: preferredSource
        )
    }
}
