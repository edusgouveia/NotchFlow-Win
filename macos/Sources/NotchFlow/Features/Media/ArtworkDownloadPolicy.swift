import Foundation

enum ArtworkDownloadPolicy {
    static let maximumByteCount = 8 * 1_024 * 1_024
    private static let allowedMIMETypes: Set<String> = [
        "image/avif",
        "image/gif",
        "image/heic",
        "image/heif",
        "image/jpeg",
        "image/png",
        "image/webp"
    ]

    static func allows(_ url: URL) -> Bool {
        guard url.scheme?.lowercased() == "https",
              let host = url.host?.lowercased(),
              !host.isEmpty else { return false }

        return !isLocalOrPrivate(host: host)
    }

    static func allows(_ response: URLResponse) -> Bool {
        guard let httpResponse = response as? HTTPURLResponse,
              let finalURL = response.url,
              allows(finalURL),
              (200..<300).contains(httpResponse.statusCode),
              let mimeType = httpResponse.mimeType?.lowercased(),
              allowedMIMETypes.contains(mimeType) else {
            return false
        }

        let expectedLength = response.expectedContentLength
        return expectedLength <= 0 || expectedLength <= maximumByteCount
    }

    static func allows(response: URLResponse, byteCount: Int) -> Bool {
        guard allows(response),
              byteCount > 0,
              byteCount <= maximumByteCount else {
            return false
        }

        return true
    }

    private static func isLocalOrPrivate(host: String) -> Bool {
        if host == "localhost"
            || host.hasSuffix(".localhost")
            || host.hasSuffix(".local")
            || host == "::1"
            || host.hasPrefix("fe80:")
            || host.hasPrefix("fc")
            || host.hasPrefix("fd") {
            return true
        }

        let components = host.split(separator: ".").compactMap { Int($0) }
        guard components.count == 4,
              components.allSatisfy({ (0...255).contains($0) }) else { return false }

        let first = components[0]
        let second = components[1]
        return first == 0
            || first == 10
            || first == 127
            || (first == 100 && (64...127).contains(second))
            || (first == 169 && second == 254)
            || (first == 172 && (16...31).contains(second))
            || (first == 192 && second == 168)
            || first >= 224
    }
}
