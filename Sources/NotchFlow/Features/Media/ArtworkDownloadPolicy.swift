import Foundation

enum ArtworkDownloadPolicy {
    static let maximumByteCount = 8 * 1_024 * 1_024

    static func allows(_ url: URL) -> Bool {
        url.scheme?.lowercased() == "https" && !(url.host?.isEmpty ?? true)
    }

    static func allows(response: URLResponse, byteCount: Int) -> Bool {
        guard let httpResponse = response as? HTTPURLResponse,
              (200..<300).contains(httpResponse.statusCode),
              httpResponse.mimeType?.lowercased().hasPrefix("image/") == true,
              byteCount > 0,
              byteCount <= maximumByteCount else {
            return false
        }

        let expectedLength = response.expectedContentLength
        return expectedLength <= 0 || expectedLength <= maximumByteCount
    }
}
