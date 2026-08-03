import Foundation
import Testing
@testable import NotchFlow

@Suite("Artwork download policy")
struct ArtworkDownloadPolicyTests {
    @Test("Only HTTPS artwork URLs are accepted")
    func urlPolicy() throws {
        #expect(ArtworkDownloadPolicy.allows(try #require(URL(string: "https://image.example/cover.jpg"))))
        #expect(!ArtworkDownloadPolicy.allows(try #require(URL(string: "http://image.example/cover.jpg"))))
        #expect(!ArtworkDownloadPolicy.allows(try #require(URL(string: "file:///tmp/cover.jpg"))))
    }

    @Test("Responses must be images within the size limit")
    func responsePolicy() throws {
        let url = try #require(URL(string: "https://image.example/cover.jpg"))
        let imageResponse = try #require(HTTPURLResponse(
            url: url,
            statusCode: 200,
            httpVersion: nil,
            headerFields: ["Content-Type": "image/jpeg"]
        ))
        let textResponse = try #require(HTTPURLResponse(
            url: url,
            statusCode: 200,
            httpVersion: nil,
            headerFields: ["Content-Type": "text/html"]
        ))

        #expect(ArtworkDownloadPolicy.allows(response: imageResponse, byteCount: 1_024))
        #expect(!ArtworkDownloadPolicy.allows(response: textResponse, byteCount: 1_024))
        #expect(!ArtworkDownloadPolicy.allows(
            response: imageResponse,
            byteCount: ArtworkDownloadPolicy.maximumByteCount + 1
        ))
    }
}
