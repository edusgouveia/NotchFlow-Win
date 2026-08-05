# Arquitetura do NotchFlow para Windows

Este documento descreve a versão Windows. A implementação original em Swift continua no
repositório, em `macos/`, e serve de especificação viva: os nomes dos componentes, os
tempos de animação e as constantes de geometria foram preservados.

Para a arquitetura da versão macOS, veja [macos/docs/ARCHITECTURE.md](../../macos/docs/ARCHITECTURE.md).

## Objetivos

Os mesmos da versão macOS: interface pequena, integração nativa, nenhuma infraestrutura
externa e separação clara entre apresentação, estado e serviços do sistema.

## Projetos

| Projeto | Responsabilidade |
| --- | --- |
| `NotchFlow.Core` | Modelos, seleção de fonte, catálogo e acesso ao SMTC. Sem dependência de XAML, o que o torna testável. |
| `NotchFlow.App` | Aplicativo WinUI 3: janelas, interop Win32, bandeja e início automático. |
| `NotchFlow.Core.Tests` | Testes de unidade da lógica pura. |

## Componentes

| Componente | Responsabilidade |
| --- | --- |
| `App` | Sobe os serviços compartilhados e monta o menu da bandeja. Equivale ao `AppDelegate`. |
| `NotchWindowController` | Cria, posiciona e remove uma ilha por monitor, e rastreia o ponteiro. |
| `NotchWindow` | Uma ilha: desenho, recorte da silhueta e comandos de mídia. |
| `NotchViewModel` | Estado de expansão e geometria de uma ilha. |
| `Win32` | Interop: estilos estendidos, topmost, região de recorte e métricas da área cliente. |
| `MediaCoordinator` | Escuta o serviço de mídia, escolhe a sessão ativa e executa comandos. |
| `SystemMediaService` | Lê e controla a mídia do sistema pelo SMTC. |
| `MediaSourceCatalog` | Traduz o AppUserModelId em nome legível e marca visual. |
| `NotificationCoordinator` | Mantém a lista e o contador, e avisa quando chega algo inédito. |
| `SystemNotificationService` | Lê a Central de Ações pela `UserNotificationListener`. |
| `NotificationSourceCatalog` | Define quais aplicativos a ilha acompanha. É o filtro de privacidade. |
| `PlaybackSelector` | Decide qual sessão aparece quando há mais de um player. Porte direto. |
| `NotchGeometry` | Valor puro com os tamanhos recolhido e expandido. Porte direto. |
| `AppSettings` | Preferências em JSON, em `%APPDATA%\NotchFlow`. |
| `TrayIconService` | Ícone na bandeja. Equivale ao `NSStatusItem`. |
| `LaunchAtLoginService` | Início automático pela chave `Run` do usuário. Equivale ao `SMAppService`. |

## O que mudou em relação ao macOS

### Mídia: sete arquivos viraram um

A versão macOS controlava a mídia por Apple Events, com um serviço para o Apple Music,
outro para o Spotify e um terceiro que varria as abas dos navegadores injetando JavaScript.
Exigia que o usuário liberasse a automação e a opção "Permitir JavaScript de Apple Events",
e consultava tudo a cada 5 segundos.

O Windows oferece o `GlobalSystemMediaTransportControlsSessionManager`, que entrega de forma
oficial e **por evento** os metadados, a capa, a linha do tempo e os comandos, para qualquer
player que se registre: Spotify, Apple Music, navegadores com `mediaSession` (incluindo o
YouTube), VLC e outros.

`AppleMusicService`, `SpotifyService`, `BrowserMediaService`, `AppleScriptRunner`,
`BrowserAppleScript`, `BrowserProbe`, `BrowserScript` e `BrowserCatalog` foram substituídos
por `SystemMediaService` e `MediaSourceCatalog`. Não há permissão a conceder.

**A única perda de fidelidade** está no rótulo da fonte. No Mac, o app lia o endereço da aba
e distinguia "YouTube Music" de "YouTube". O SMTC informa o aplicativo, não a aba, então para
navegadores a marca é inferida dos metadados e pode ficar neutra.

O polling de 5 segundos continua existindo, mas como rede de segurança para eventos perdidos,
não como mecanismo principal.

### A notch de hardware não existe

A versão macOS media a notch física por `auxiliaryTopLeftArea` e `safeAreaInsets`, e separava
telas por `CGDisplayIsBuiltin`. Nenhum PC tem notch.

A resposta já estava no próprio desenho: o modo `sliver`, criado para monitores externos. No
Windows, `DisplayKind` distingue apenas tela principal de secundária. A principal recebe a ilha
completa; as secundárias recebem a tira, se a preferência estiver ligada. Como o Windows não tem
barra de menus no topo, a ilha encosta na primeira linha da tela.

Há uma consequência que demorou a aparecer: sem notch para imitar, **largura fixa é só espaço
preto desperdiçado**. Os 156 pontos herdados do Mac deixavam uma barra grande e vazia no topo.
A ilha recolhida passou a medir o que mostra, somando capa, ícone de reprodução e contador de
notificações — de 38 pontos quando ociosa até cerca de 75 com tudo visível. A altura caiu de 32
para 26.

`ClosedContent` carrega esse estado até a geometria, e `NotchViewModel` recalcula a cada mudança
de mídia ou notificação. Como a animação já interpola largura e reaplica o recorte a cada quadro,
a ilha cresce e encolhe sozinha sem código adicional.

Duas consequências disso:

- a região que responde ao ponteiro ganhou piso próprio, de 96 pontos. Mirar numa ilha de 38
  pontos seria desconfortável, então o alvo é maior que o desenho, como já acontecia na tira;
- o recuo superior do painel aberto passou a vir da geometria em vez de ficar cravado no XAML.
  Ele deriva da altura recolhida, e um valor fixo teria silenciosamente desalinhado o conteúdo
  quando essa altura mudou.

### O recorte da janela

Este é o problema que o macOS não tem. Lá, a área transparente de um `NSPanel` deixa o clique
passar. No Windows, uma janela captura o clique em todo o seu retângulo.

A janela da ilha mede 520x224 para acomodar o painel aberto. Sem tratamento, seria um retângulo
invisível engolindo todo clique na faixa superior de cada monitor.

`Win32.ApplyIslandRegion` resolve com `SetWindowRgn`, recortando a janela para a silhueta da
ilha: cantos de cima retos e os de baixo arredondados, como o `UnevenRoundedRectangle` do
SwiftUI. Fora da região a janela deixa de existir para o sistema, e o clique chega ao aplicativo
de baixo.

O recorte é reaplicado a cada quadro da animação, junto com o tamanho do desenho, para a
silhueta e o traçado nunca divergirem.

### O recuo da área cliente

Mesmo sem faixa de título, uma janela do WinAppSDK mantém 3 pixels de área não-cliente em volta,
que o sistema pinta de claro por cima do conteúdo. Isso aparecia como um risco branco na aresta
superior da ilha.

`NotchWindow.PositionOn` dimensiona a janela para que a **área cliente** meça 520x224 e a sobe
em `offsetY`, de modo que o topo dessa área caia exatamente na primeira linha do monitor e a
borda clara fique fora da tela. O recuo é medido em tempo de execução, não fixado em código.

### Detalhes que custaram tempo

- `WS_EX_TOPMOST` é ignorado quando aplicado por `SetWindowLongPtr`. Só `SetWindowPos` com
  `HWND_TOPMOST` promove a janela.
- Enumerar com `foreach` um `IReadOnlyList` projetado pelo WinRT lança `InvalidCastException`.
  O acesso por índice é o caminho suportado, e vale para `DisplayArea.FindAll()` e para as
  sessões do SMTC.
- Os glyphs `E7A6` e `E7A7` do Segoe Fluent Icons desenham as setas ao contrário do que os
  nomes `Undo` e `Redo` sugerem.
- A barra de progresso é desenhada à mão. O `Slider` padrão traz um polegar grande demais e um
  traço grosso que não combinam com a ilha, e restilizá-lo custaria mais que desenhá-lo.

## Notificações

Recurso que não existe na versão macOS. Nasceu do pedido de "abrir a ilha quando chegar
mensagem do Teams", mas foi implementado de outra forma, pelo motivo descrito abaixo.

### Por que a ilha não abre sozinha por padrão

Para a API funcionar, o Teams precisa estar configurado com o estilo de notificação do Windows.
Ou seja, **o usuário já recebe o toast nativo**. Abrir a ilha por cima disso entrega o mesmo
aviso duas vezes, sendo a segunda no ponto de maior roubo de atenção da tela.

Além disso, contraria o desenho que o projeto já tinha: quando a música muda, a ilha não se
abre, mostra uma cápsula colorida. Ela é ambiente, não intrusiva.

O que a ilha faz que o toast não faz é **permanecer**. O toast é efêmero; o indicador fica até
a mensagem ser lida. É nisso que o recurso se apoia.

O comportamento pedido originalmente existe como preferência opcional,
`AutoExpandOnNotification`, desligada por padrão.

### Contagem que se corrige sozinha

A `UserNotificationListener` não entrega só o evento de chegada: entrega a **lista atual** da
Central de Ações. Então o contador é simplesmente quantas notificações do Teams estão lá.

Quando o usuário lê as mensagens no Teams, o próprio Teams remove os toasts, e o contador cai
sem o NotchFlow precisar rastrear leitura. Não há estado a sincronizar.

O `NotificationCoordinator` guarda os ids já vistos para distinguir uma chegada nova de uma
releitura da mesma lista. Ids que somem da Central são esquecidos, o que evita o conjunto
crescer para sempre e permite avisar de novo se a mesma notificação voltar.

A primeira leitura apenas registra o que já estava lá: sem isso, abrir o NotchFlow com mensagens
acumuladas dispararia um aviso para cada uma.

### Várias fontes na mesma coluna

O `NotificationSourceCatalog` acompanha Teams, Outlook e WhatsApp. Como a lista pode misturar
aplicativos, duas decisões de apresentação seguem daí:

- cada linha traz um ponto na cor da origem, que é o que diferencia um e-mail de uma mensagem
  sem repetir o nome do aplicativo em cada item;
- o cabeçalho só nomeia o aplicativo quando **todos** os itens vêm dele, o que
  `NotificationCoordinator.SingleSource` resolve. Com mistura, o rótulo fica genérico e a cor
  neutra: escrever "Teams" numa lista que também traz Outlook seria enganoso.

A prévia ocupa uma linha só. Com três notificações, duas linhas estouram a altura da ilha e a
última fica cortada — quem escreveu importa mais que o texto completo.

### O Teams desktop não entrega notificações ao Windows

O aplicativo desktop do Teams desenha a própria notificação em uma WebView (processo
`ms-teams`, classe de janela `TeamsWebView`) e nunca a publica no sistema. O banner imita o
visual nativo, com caixa de resposta rápida, mas não passa pela Central de Ações.

Confirmado por quatro caminhos: o processo dono do pixel do banner, a ausência do Teams no
registro de origens de notificação após três mensagens, a Central de Ações vazia com o banner
na tela, e um monitor de 10 minutos que capturou outros aplicativos nos mesmos segundos.

Nenhum aplicativo consegue ler essa notificação. A entrada do Teams permanece no catálogo
porque a **versão web funciona**: o identificador do PWA contém "teams" e casa na mesma regra.

### Duas descobertas sobre a API

**Funciona sem identidade de pacote.** A expectativa era que a capability
`userNotificationListener` exigisse um manifesto MSIX, o que obrigaria a empacotar e assinar o
aplicativo. Um spike mostrou que `RequestAccessAsync` devolve `Allowed` num .exe solto. O
NotchFlow continua sendo baixar, descompactar e executar.

**O evento `NotificationChanged` não funciona sem identidade de pacote.** A assinatura lança
`COMException`. Por isso o coordenador trata o evento como um bônus e se apoia numa consulta a
cada 4 segundos, o mesmo padrão de rede de segurança usado no coordenador de mídia.

### Privacidade

A API entrega as notificações de todos os aplicativos: não há como pedir só o Teams.

O `NotificationSourceCatalog` é aplicado **durante** a leitura, em
`SystemNotificationService.TryConvert`: o que não casa é descartado antes de o texto ser
extraído. O que não é do Teams não chega a existir como objeto.

Nada é gravado em disco, nem no log, e o aplicativo não acessa a rede. O recurso vem desligado
e ligá-lo é uma escolha explícita.

## Abertura e fechamento

Igual ao macOS, em duas etapas controladas por `NotchViewModel.SetExpanded`:

1. Ao abrir, `IsExpanded` muda primeiro e a forma cresce. Depois de `ContentInDelay` (110 ms),
   `ShowsContent` liga e o interior aparece.
2. Ao fechar, `ShowsContent` desliga primeiro. Depois de `ShapeCloseDelay` (70 ms), `IsExpanded`
   volta a falso e a forma encolhe.

A etapa pendente é cancelada a cada chamada, então movimentos rápidos do cursor não deixam a
interface num estado intermediário.

## Rastreamento do ponteiro

`NotchWindowController` consulta `GetCursorPos` a cada 60 ms, a mesma cadência do timer usado
na versão macOS. A janela é recortada e não-ativante, então depender do hover do XAML seria
frágil quando outro aplicativo está em primeiro plano.

A histerese abre depois de 55 ms e fecha depois de 300 ms, para o painel não piscar quando o
cursor apenas atravessa a região.

A cada 33 ciclos (cerca de 2 segundos) o controlador confere se os monitores mudaram, o que
evita manter uma janela de mensagens só para receber `WM_DISPLAYCHANGE`.

## Persistência

`AppSettings` grava cinco interruptores em `%APPDATA%\NotchFlow\settings.json`: reduzir nas telas
secundárias, mostrar em todas as telas, mostrar o ícone da bandeja, acompanhar as notificações e
abrir a ilha ao receber. O início automático pertence ao Windows, na chave `Run`.

Metadados de mídia, capas, notificações e posições ficam só em memória.

Um arquivo de preferências corrompido volta ao padrão em vez de impedir a abertura.

## O que ainda não existe

O painel do calendário. O EventKit agrega todas as contas do sistema com uma permissão local e
nenhuma requisição de rede, e o Windows não tem equivalente com as mesmas propriedades. A decisão
está descrita no [README](../../README.md).

`NotchGeometry` já aceita `includesCalendar`: quando o painel existir, a ilha abre a coluna extra
sem que o resto da geometria mude.
