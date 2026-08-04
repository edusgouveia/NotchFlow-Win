# Política de Segurança

## Versões suportadas

| Versão | Suporte de segurança |
| --- | --- |
| 0.2.x | Sim |
| 0.1.x | Somente correções críticas |
| anteriores | Não |

## Relatando uma vulnerabilidade

Não publique detalhes sensíveis em uma issue aberta.

Use **Security → Report a vulnerability** no repositório quando o recurso estiver disponível. Se essa opção não aparecer, abra uma issue sem detalhes técnicos solicitando um canal privado e aguarde a resposta do mantenedor.

Inclua no relato privado:

- versão do macOS e do NotchFlow;
- impacto observado;
- passos mínimos para reprodução;
- evidências sem dados pessoais, tokens ou conteúdo real de calendário;
- sugestão de correção, se houver.

## Escopo relevante

- execução inesperada de AppleScript ou Apple Events;
- exposição de eventos, metadados de mídia ou arquivos locais;
- requisições de rede fora do carregamento de capas;
- persistência não documentada;
- bypass das permissões do macOS;
- alteração do processo de build ou release.

## Modelo de segurança atual

- sem backend, conta ou telemetria;
- sem dependências externas do Swift Package Manager;
- calendário em modo somente leitura no código da aplicação;
- comandos de mídia definidos por enum, sem entrada de script fornecida pelo usuário;
- capas do Spotify e do navegador restritas a HTTPS, formatos permitidos e 8 MB durante a transferência;
- redirecionamentos inseguros, hosts locais e redes privadas bloqueados no carregamento de capas;
- logs sem títulos, URLs, eventos ou mensagens externas dinâmicas;
- scripts de segurança verificam arquivos rastreados e novos, segredos, caminhos pessoais e configurações perigosas;
- release com hardened runtime e assinatura local ad hoc;
- GitHub Actions com permissões mínimas e ações de terceiros fixadas por commit.

## Limitações conhecidas

A versão atual não é notarizada e não usa App Sandbox. Ela solicita acesso completo de leitura ao Calendário porque essa é a autorização disponibilizada pelo EventKit para ler eventos no macOS 14. O aplicativo deve ser instalado somente a partir das releases deste repositório ou compilado diretamente do código-fonte revisado.
