<div align="center">
  <img src="Assets/AppIcon.png" width="148" alt="Ícone do NotchFlow">

  # NotchFlow

  **Música e calendário no ponto mais natural do seu Mac.**

  [![macOS 14+](https://img.shields.io/badge/macOS-14%2B-111827?logo=apple)](https://www.apple.com/macos/)
  [![Swift 6](https://img.shields.io/badge/Swift-6-F05138?logo=swift&logoColor=white)](https://www.swift.org/)
  [![CI](https://github.com/Thiagof2755/NotchFlow-swift/actions/workflows/ci.yml/badge.svg)](https://github.com/Thiagof2755/NotchFlow-swift/actions/workflows/ci.yml)
  [![Privacy](https://img.shields.io/badge/privacidade-local--first-22c55e)](PRIVACY.md)

  [**Baixar a versão mais recente**](https://github.com/Thiagof2755/NotchFlow-swift/releases/latest/download/NotchFlow-macOS-arm64.zip)

  Aplicativo nativo, leve e sem telemetria para macOS.
</div>

---

## Visão geral

O NotchFlow transforma a área do notch em um painel discreto para controlar o **Apple Music**, o **Spotify** e a mídia que está tocando no **navegador**, como YouTube e YouTube Music, além de consultar o mês do **Calendário** sem interromper o que você está fazendo.

O painel fica recolhido no topo, abre com uma animação ao aproximar o cursor e funciona de forma independente em todos os monitores conectados. Na tela do Mac ele imita o notch; nos monitores externos fica apenas uma tira fina, que abre o painel ao passar o mouse.

## O que já está disponível

| Área | Funcionalidades |
| --- | --- |
| Música | Capa, faixa, artista, progresso, play/pause, anterior, próxima e avanço ou retorno de 15 segundos. |
| Fontes | Seleção automática entre Apple Music, Spotify e a aba de mídia do navegador. |
| Navegador | YouTube, YouTube Music, SoundCloud, Spotify Web e outros sites no Chrome, Brave, Edge, Arc, Vivaldi, Opera e Safari. |
| Calendário | Mês completo, navegação entre meses, marcações nos dias com eventos e resumo do dia selecionado. |
| Monitores | Ilha completa na tela do Mac e tira discreta nas telas externas, com painel independente em cada uma. |
| Controles | Barra de progresso arrastável, além dos saltos de 15 segundos. |
| Sistema | Aplicativo acessório, sem ícone no Dock e sem ícone na barra de menus por padrão, com opção de iniciar junto com o Mac. Todas as opções ficam no menu de contexto da ilha. |

## Download e instalação

### Download rápido

Baixe o arquivo **NotchFlow-macOS-arm64.zip** na página de [releases](https://github.com/Thiagof2755/NotchFlow-swift/releases/latest) ou use o botão no início desta página.

> A versão atual é destinada a Macs com Apple Silicon e macOS 14 ou posterior.

### Primeira abertura

1. Descompacte o arquivo baixado.
2. Mova `NotchFlow.app` para a pasta **Aplicativos**.
3. Clique com o botão direito no aplicativo e selecione **Abrir**.
4. Autorize o Calendário e a automação do Apple Music, do Spotify ou do navegador quando o macOS solicitar.
5. Para controlar a mídia do navegador, ative **Permitir JavaScript de Apple Events**: no Chrome e derivados em **Visualizar › Desenvolvedor**, no Safari em **Desenvolvedor**.
6. Clique com o botão direito na ilha para abrir o menu com **Ajustes…**, e marque **Abrir ao iniciar o Mac** se quiser inicialização automática.

> O NotchFlow pede a permissão do Calendário sozinho na primeira execução. Com assinatura ad hoc, o macOS pede de novo depois de cada build, porque considera o aplicativo assinado outra vez. Para manter as permissões, assine com um certificado fixo:
>
> ```bash
> NOTCHFLOW_SIGN_IDENTITY="Nome do certificado" ./Scripts/build-app.sh
> ```

O build público inicial usa assinatura local ad hoc e ainda não é notarizado pela Apple. Por isso, a primeira abertura pode exigir a confirmação descrita acima.

## Privacidade por padrão

O NotchFlow não possui servidor, conta, banco de dados ou telemetria.

- eventos são lidos diretamente do EventKit e permanecem na memória do Mac;
- títulos de músicas e controles são obtidos por Apple Events;
- nenhuma informação de calendário ou reprodução é enviada pelo NotchFlow;
- a única requisição de rede do aplicativo baixa, por HTTPS, a capa fornecida pelo Spotify ou pelo site aberto no navegador;
- capas remotas são limitadas a imagens de até 8 MB e não são salvas em disco.

Consulte [PRIVACY.md](PRIVACY.md) para a descrição completa e [SECURITY.md](SECURITY.md) para relatar uma vulnerabilidade.

## Permissões utilizadas

| Permissão | Motivo | O que o app não faz |
| --- | --- | --- |
| Calendário | Exibir os eventos do mês. | Não cria, altera ou exclui eventos. |
| Automação | Ler e controlar Apple Music, Spotify e a aba de mídia do navegador. | Não executa comandos fornecidos pelo usuário, não lê o conteúdo das outras abas e não altera páginas. |
| Item de início | Abrir o próprio aplicativo após o login. | Não instala daemon ou serviço privilegiado. |

## Desenvolvimento

### Requisitos

- macOS 14 ou posterior;
- Xcode 26 ou toolchain Swift 6 compatível;
- Git.

Não são necessários Homebrew, Node.js, chaves do Spotify ou dependências externas de pacote.

### Executar pelo Xcode

1. Abra `Package.swift` no Xcode.
2. Selecione o esquema **NotchFlow**.
3. Pressione `⌘R` para executar e `⌘.` para encerrar.

### Executar pelo terminal

```bash
swift test
swift run NotchFlow
```

### Gerar o aplicativo

```bash
./Scripts/security-check.sh
./Scripts/build-app.sh
```

O pacote será criado em `dist/NotchFlow.app` com ícone, `Info.plist`, permissões mínimas declaradas, hardened runtime e assinatura local ad hoc.

## Arquitetura

```mermaid
flowchart LR
    UI["SwiftUI · Notch e Ajustes"] --> VM["NotchFlowViewModel"]
    VM --> MEDIA["MediaCoordinator"]
    VM --> CAL["CalendarService"]
    VM --> LOGIN["LaunchAtLoginService"]
    MEDIA --> MUSIC["Apple Music"]
    MEDIA --> SPOTIFY["Spotify"]
    MEDIA --> BROWSER["Navegadores"]
    CAL --> EVENTKIT["EventKit"]
    LOGIN --> SM["ServiceManagement"]
```

Detalhes das responsabilidades, fluxos e decisões técnicas estão em [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Estrutura do projeto

```text
NotchFlow-swift/
├── Assets/                 # ícone-fonte do aplicativo
├── Configuration/          # Info.plist e entitlements
├── Scripts/                # auditoria e empacotamento local
├── Sources/NotchFlow/      # aplicativo macOS
├── Tests/NotchFlowTests/   # testes de unidade
└── .github/workflows/      # CI e publicação de releases
```

## Roadmap

- aperfeiçoar acessibilidade e navegação por teclado;
- adicionar preferências de aparência;
- ampliar testes dos serviços de mídia;
- disponibilizar build universal e notarizado.

## Estado do projeto

O NotchFlow está em desenvolvimento inicial. Use a seção de [Issues](https://github.com/Thiagof2755/NotchFlow-swift/issues) para problemas não sensíveis.

## Licença

Copyright © 2026 Thiago Alves. Todos os direitos reservados. Consulte [LICENSE](LICENSE).
