import Foundation

enum ArtworkDownloader {
    static func download(from url: URL) async throws -> Data? {
        guard ArtworkDownloadPolicy.allows(url) else { return nil }

        var request = URLRequest(url: url)
        request.cachePolicy = .returnCacheDataElseLoad
        request.timeoutInterval = 10

        let (bytes, response) = try await URLSession.shared.bytes(for: request)
        guard ArtworkDownloadPolicy.allows(response) else { return nil }

        var data = Data()
        let expectedLength = response.expectedContentLength
        if expectedLength > 0 {
            data.reserveCapacity(min(Int(expectedLength), ArtworkDownloadPolicy.maximumByteCount))
        }

        for try await byte in bytes {
            guard data.count < ArtworkDownloadPolicy.maximumByteCount else { return nil }
            data.append(byte)
        }

        guard ArtworkDownloadPolicy.allows(response: response, byteCount: data.count) else {
            return nil
        }
        return data
    }
}
