# Privacidade do NotchFlow

Última atualização: 3 de agosto de 2026.

O NotchFlow foi projetado para funcionar localmente, sem conta, servidor próprio, banco de dados ou telemetria.

## Dados acessados

| Dado | Origem | Finalidade | Persistência | Compartilhamento |
| --- | --- | --- | --- | --- |
| Título, artista, álbum e estado de reprodução | Apple Music ou Spotify, via Apple Events | Exibir e controlar a reprodução | Somente memória | Nenhum |
| Capa do Apple Music | Apple Music | Exibir a arte da faixa | Somente memória | Nenhum |
| URL e imagem da capa do Spotify | Spotify e servidor HTTPS indicado pelo Spotify | Exibir a arte da faixa | Cache somente em memória | Requisição direta ao servidor da imagem |
| Título, horário e calendário dos eventos | EventKit | Exibir o mês e o dia selecionado | Somente memória | Nenhum |
| Preferência de inicialização | ServiceManagement do macOS | Abrir o NotchFlow após o login | Gerenciada pelo macOS | Nenhum |

## Rede

O aplicativo não se comunica com servidores do NotchFlow. A única operação de rede implementada é o download da capa indicada pelo Spotify. Essa operação aceita apenas HTTPS, exige uma resposta de imagem e limita o arquivo a 8 MB.

## Logs

Os logs registram somente eventos operacionais e mensagens de erro. O código não registra títulos de músicas, nomes de eventos, conteúdo do calendário ou capas.

## Permissões

O acesso ao Calendário e a automação de outros aplicativos dependem da autorização do usuário no macOS. Essas permissões podem ser revogadas a qualquer momento nos Ajustes do Sistema.

## Exclusão de dados

Como o NotchFlow não cria uma base própria, não existe conta ou arquivo de dados pessoais para excluir. Encerrar o aplicativo remove da memória os dados temporários. O item de início pode ser desativado no menu do NotchFlow ou nos Ajustes do Sistema.

## Alterações

Mudanças que adicionem persistência, telemetria ou novas integrações de rede devem atualizar este documento antes de uma nova versão.
