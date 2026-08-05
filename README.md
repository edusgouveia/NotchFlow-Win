<div align="center">
  <img src="Assets/AppIcon.png" width="148" alt="Ícone do NotchFlow">

  # NotchFlow para Windows

  **Sua música no ponto mais natural da tela.**

  [![Windows 10/11](https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows)](https://www.microsoft.com/windows)
  [![.NET 9](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
  [![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4)](https://learn.microsoft.com/windows/apps/winui/)
  [![License: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-22c55e.svg)](LICENSE)

  Aplicativo nativo, leve e sem telemetria para Windows.
</div>

---

## Visão geral

O NotchFlow põe uma ilha discreta no topo da tela para controlar o que estiver tocando: Spotify,
Apple Music, qualquer aba de navegador com áudio ou vídeo, VLC e outros players.

A ilha fica recolhida no topo, abre com uma animação ao aproximar o cursor e funciona de forma
independente em todos os monitores. Na tela principal ela aparece como uma ilha; nas demais fica
apenas uma tira fina, que abre o painel ao passar o mouse.

Esta é a versão Windows do [NotchFlow para macOS](https://github.com/Thiagof2755/NotchFlow-swift),
de Thiago Alves. O código Swift original continua neste repositório, em `macos/`, como
referência da arquitetura e do desenho.

## O que já está disponível

| Área | Funcionalidades |
| --- | --- |
| Música | Capa, faixa, artista, progresso, play/pause, anterior, próxima e avanço ou retorno de 15 segundos. |
| Fontes | Qualquer player registrado no Windows: Spotify, Apple Music, VLC, foobar2000, MusicBee e outros. |
| Navegador | YouTube, YouTube Music, SoundCloud, Spotify Web e qualquer site com `mediaSession`, no Edge, Chrome, Firefox, Brave, Opera e Vivaldi. |
| Monitores | Ilha na tela principal e tira discreta nas demais, com painel independente em cada uma. |
| Controles | Barra de progresso arrastável, além dos saltos de 15 segundos. |
| Sistema | Aplicativo residente, sem janela e fora do Alt+Tab, com ícone na bandeja e opção de iniciar junto com o Windows. |

Os controles disponíveis acompanham o que cada player declara: um serviço que não permite pular
faixa deixa esses botões esmaecidos, em vez de oferecer uma ação que não funciona.

## O que ainda não existe

**O painel do calendário.** Na versão macOS ele lê o EventKit, que agrega todas as contas do
sistema com uma permissão local e nenhuma requisição de rede. O Windows não tem equivalente com
as mesmas propriedades:

- `Windows.ApplicationModel.Appointments` depende do app Mail e Calendário, descontinuado em
  favor do novo Outlook;
- o Microsoft Graph funciona bem, mas exige OAuth e acesso à rede, o que quebraria a promessa de
  não ter servidor nem conta;
- um arquivo `.ics` local ou CalDAV preserva a privacidade, mas exige configuração manual.

É uma decisão de produto em aberto, não uma limitação técnica. A geometria da ilha já prevê a
coluna extra: quando o painel existir, ela abre sem que o resto mude.

## Download e instalação

1. Baixe e descompacte o pacote em uma pasta de sua preferência.
2. Execute `NotchFlow.exe`.
3. Para iniciar junto com o Windows, clique com o botão direito no ícone da bandeja e marque
   **Abrir com o Windows**.

Não há instalador, serviço nem tarefa agendada. Para remover, encerre pelo menu da bandeja,
desmarque o início automático e apague a pasta.

> Na primeira execução o Windows costuma esconder ícones novos da bandeja. Se não encontrar o
> ícone, clique na seta de ícones ocultos, ao lado do relógio.

### Requisitos

- Windows 10 versão 1809 ou posterior, ou Windows 11;
- [Windows App Runtime 1.8](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads).

Para dispensar o runtime, gere um pacote autocontido com `./Scripts/build.ps1 -SelfContained`.

## Privacidade por padrão

O NotchFlow não possui servidor, conta, banco de dados ou telemetria.

- os metadados vêm do próprio Windows, pelo SMTC, e permanecem em memória;
- as capas chegam prontas do sistema, sem download pela rede;
- **o aplicativo não faz nenhuma requisição de rede**;
- o log local registra apenas eventos do próprio aplicativo, nunca o que você ouviu;
- as preferências são três interruptores em `%APPDATA%\NotchFlow\settings.json`.

## Permissões utilizadas

Nenhuma. O SMTC é uma API pública que não exige consentimento, e o início automático usa a chave
`Run` do usuário atual, sem privilégio de administrador.

Isso é uma vantagem sobre a versão macOS, que precisa de autorização de Automação para cada
player e da opção "Permitir JavaScript de Apple Events" nos navegadores.

## Desenvolvimento

### Requisitos

- .NET SDK 9 ou posterior;
- Windows SDK 10.0.26100;
- Git.

Não são necessários Visual Studio, workloads adicionais nem chaves de API.

Todos os comandos abaixo rodam a partir da pasta `windows/`.

### Compilar e executar

```bash
dotnet run --project src/NotchFlow.App/NotchFlow.App.csproj -c Release
```

### Testes

```bash
dotnet test NotchFlow.sln
```

### Gerar o pacote

```bash
./Scripts/build.ps1
```

O script roda os testes antes de empacotar e publica o SHA-256 do resultado em `windows/dist/`.

## Arquitetura

```mermaid
flowchart LR
    UI["WinUI 3 · NotchWindow"] --> VM["NotchViewModel"]
    UI --> WIN32["Win32 · recorte e estilos"]
    VM --> MEDIA["MediaCoordinator"]
    MEDIA --> SMTC["SystemMediaService"]
    SMTC --> WINRT["Windows.Media.Control"]
    WINRT --> PLAYERS["Spotify · Navegadores · VLC"]
    APP["App"] --> TRAY["TrayIconService"]
    APP --> LOGIN["LaunchAtLoginService"]
```

Três projetos: `NotchFlow.Core` concentra os modelos e o acesso ao SMTC sem depender de XAML, o
que o torna testável; `NotchFlow.App` é o aplicativo WinUI 3; `NotchFlow.Core.Tests` cobre a
lógica pura.

Detalhes das decisões, e o que mudou em relação ao macOS, estão em
[windows/docs/ARCHITECTURE.md](windows/docs/ARCHITECTURE.md).

## Estrutura do repositório

As duas implementações vivem lado a lado, cada uma autocontida:

```text
NotchFlow-Win/
├── windows/                       # a versão Windows, o produto deste repositório
│   ├── NotchFlow.sln
│   ├── src/NotchFlow.Core/        # modelos, SMTC e seleção de fonte
│   ├── src/NotchFlow.App/         # aplicativo WinUI 3
│   ├── tests/NotchFlow.Core.Tests/
│   ├── Scripts/build.ps1          # empacotamento local
│   └── docs/ARCHITECTURE.md
├── macos/                         # a versão Swift original, mantida como referência
│   ├── Package.swift
│   ├── Sources/ · Tests/ · Configuration/ · Scripts/
│   └── docs/ARCHITECTURE.md
└── Assets/                        # ícone, compartilhado pelas duas
```

## Roadmap

- decidir e implementar a fonte do calendário;
- janela de ajustes, hoje substituída pelo menu da bandeja;
- preferências de aparência;
- aperfeiçoar acessibilidade e navegação por teclado.

## Licença

Distribuído sob a [licença MIT](LICENSE), como o projeto original.
