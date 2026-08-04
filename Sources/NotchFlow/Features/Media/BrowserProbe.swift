import Foundation

/// Dados devolvidos pelo JavaScript de uma aba.
struct BrowserMediaPayload: Equatable, Sendable, Decodable {
    var title: String
    var artist: String
    var album: String
    var artwork: String
    var duration: Double
    var position: Double
    var playing: Bool
    var host: String

    private enum CodingKeys: String, CodingKey {
        case title
        case artist
        case album
        case artwork
        case duration
        case position
        case playing
        case host
    }

    init(
        title: String = "",
        artist: String = "",
        album: String = "",
        artwork: String = "",
        duration: Double = 0,
        position: Double = 0,
        playing: Bool = false,
        host: String = ""
    ) {
        self.title = title
        self.artist = artist
        self.album = album
        self.artwork = artwork
        self.duration = duration
        self.position = position
        self.playing = playing
        self.host = host
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        title = try container.decodeIfPresent(String.self, forKey: .title) ?? ""
        artist = try container.decodeIfPresent(String.self, forKey: .artist) ?? ""
        album = try container.decodeIfPresent(String.self, forKey: .album) ?? ""
        artwork = try container.decodeIfPresent(String.self, forKey: .artwork) ?? ""
        duration = try container.decodeIfPresent(Double.self, forKey: .duration) ?? 0
        position = try container.decodeIfPresent(Double.self, forKey: .position) ?? 0
        playing = try container.decodeIfPresent(Bool.self, forKey: .playing) ?? false
        host = try container.decodeIfPresent(String.self, forKey: .host) ?? ""
    }
}

/// Uma aba com mídia, identificada pela posição em que pode ser controlada.
struct BrowserProbeResult: Equatable, Sendable {
    let windowIndex: Int
    let tabIndex: Int
    let payload: BrowserMediaPayload
}

enum BrowserProbeParser {
    struct Output: Equatable, Sendable {
        var results: [BrowserProbeResult]
        var errorMessages: [String]

        init(results: [BrowserProbeResult] = [], errorMessages: [String] = []) {
            self.results = results
            self.errorMessages = errorMessages
        }
    }

    static func parse(_ raw: String) -> Output {
        var output = Output()

        for rawLine in raw.split(whereSeparator: \.isNewline) {
            let parts = rawLine.split(
                separator: Character(BrowserAppleScript.fieldSeparator),
                maxSplits: 2,
                omittingEmptySubsequences: false
            )
            guard parts.count == 3,
                  let windowIndex = Int(parts[0]),
                  let tabIndex = Int(parts[1]) else { continue }

            let value = String(parts[2])
            if value.hasPrefix(BrowserAppleScript.errorPrefix) {
                let message = String(value.dropFirst(BrowserAppleScript.errorPrefix.count))
                if !message.isEmpty {
                    output.errorMessages.append(message)
                }
                continue
            }

            guard let payload = decodePayload(value) else { continue }
            output.results.append(
                BrowserProbeResult(windowIndex: windowIndex, tabIndex: tabIndex, payload: payload)
            )
        }

        return output
    }

    static func decodePayload(_ value: String) -> BrowserMediaPayload? {
        guard let data = value.data(using: .utf8) else { return nil }
        return try? JSONDecoder().decode(BrowserMediaPayload.self, from: data)
    }

    /// Ordena as abas colocando o que está tocando primeiro e, em empate, a mídia mais longa.
    static func ranked(_ results: [BrowserProbeResult]) -> [BrowserProbeResult] {
        results.sorted { lhs, rhs in
            if lhs.payload.playing != rhs.payload.playing { return lhs.payload.playing }
            if lhs.payload.duration != rhs.payload.duration { return lhs.payload.duration > rhs.payload.duration }
            if lhs.windowIndex != rhs.windowIndex { return lhs.windowIndex < rhs.windowIndex }
            return lhs.tabIndex < rhs.tabIndex
        }
    }
}

/// Limpa títulos vindos de `document.title`, que trazem contadores e o nome do site.
enum BrowserTitleCleaner {
    private static let suffixes = [
        " - YouTube Music",
        " - YouTube",
        " | SoundCloud",
        " - SoundCloud",
        " | Spotify",
        " - Twitch",
        " on Vimeo",
        " | Netflix"
    ]

    static func clean(_ raw: String) -> String {
        var value = removeBadge(raw.trimmingCharacters(in: .whitespacesAndNewlines))

        for suffix in suffixes where value.hasSuffix(suffix) {
            value = String(value.dropLast(suffix.count))
            break
        }

        return value.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private static func removeBadge(_ value: String) -> String {
        guard value.hasPrefix("("), let closingIndex = value.firstIndex(of: ")") else { return value }

        let badge = value[value.index(after: value.startIndex)..<closingIndex]
        guard !badge.isEmpty, badge.allSatisfy(\.isNumber) else { return value }

        let remainder = value[value.index(after: closingIndex)...]
        return String(remainder).trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
