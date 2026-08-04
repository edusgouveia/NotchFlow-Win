import Testing
@testable import NotchFlow

@Suite("Informações do projeto")
struct ProjectInfoTests {
    @Test("Aponta para o repositório oficial e identifica o autor")
    func officialProjectInformation() {
        #expect(ProjectInfo.author == "Thiago Alves")
        #expect(ProjectInfo.repositoryURL.host == "github.com")
        #expect(ProjectInfo.repositoryURL.path == "/Thiagof2755/NotchFlow-swift")
    }
}
