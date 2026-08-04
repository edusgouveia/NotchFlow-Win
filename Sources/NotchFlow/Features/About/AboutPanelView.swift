import AppKit
import SwiftUI

struct AboutPanelView: View {
    let onClose: () -> Void

    var body: some View {
        HStack(spacing: 14) {
            Image(systemName: "waveform.path.ecg.rectangle.fill")
                .font(.system(size: 34, weight: .medium))
                .symbolRenderingMode(.hierarchical)
                .foregroundStyle(.white.opacity(0.82))
                .frame(width: 48, height: 48)
                .background(.white.opacity(0.06), in: RoundedRectangle(cornerRadius: 12))

            VStack(alignment: .leading, spacing: 5) {
                Text(ProjectInfo.name)
                    .font(.system(size: 15, weight: .bold))

                Text(ProjectInfo.summary)
                    .font(.system(size: 9.5))
                    .foregroundStyle(.secondary)
                    .lineLimit(2)

                HStack(spacing: 5) {
                    Image(systemName: "person.crop.circle.fill")
                    Text("Criado por \(ProjectInfo.author)")
                }
                .font(.system(size: 9, weight: .medium))
                .foregroundStyle(.white.opacity(0.72))

                HStack(spacing: 7) {
                    Button {
                        NSWorkspace.shared.open(ProjectInfo.repositoryURL)
                    } label: {
                        Label("Abrir repositório", systemImage: "arrow.up.right.square")
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(.gray)

                    Button("Voltar", action: onClose)
                        .buttonStyle(.bordered)
                }
                .controlSize(.small)
            }

            Spacer(minLength: 0)

            VStack(alignment: .leading, spacing: 5) {
                Label("Projeto independente", systemImage: "sparkles")
                Label("Open source • \(ProjectInfo.license)", systemImage: "chevron.left.forwardslash.chevron.right")
                Label("Feito para macOS", systemImage: "apple.logo")
            }
            .font(.system(size: 8.5, weight: .medium))
            .foregroundStyle(.white.opacity(0.62))
            .frame(width: 112, alignment: .leading)
        }
        .padding(.horizontal, 12)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Sobre o NotchFlow")
    }
}
