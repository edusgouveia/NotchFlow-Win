// swift-tools-version: 6.0

import PackageDescription

let package = Package(
    name: "NotchFlow",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .executable(name: "NotchFlow", targets: ["NotchFlow"])
    ],
    targets: [
        .executableTarget(
            name: "NotchFlow",
            path: "Sources/NotchFlow"
        ),
        .testTarget(
            name: "NotchFlowTests",
            dependencies: ["NotchFlow"],
            path: "Tests/NotchFlowTests"
        )
    ]
)
