# Console WPF — implementação e validação

Registro de 28/10/2026. Implementado a partir da proposta em `cad-usabilidade-camadas-console.md`, preservando trabalho paralelo de marca/instalador/splash.

## Entrega

- `CadConsole` é um UserControl próprio. `RunCadCommand(string, bool)` e a bridge MCP permanecem como entrada de execução. Não há janela flutuante nem dependências novas.
- Cabeçalho curto **Console**; o tooltip explica o histórico de comandos e mensagens. Entrada sempre disponível, inclusive recolhido.
- GridSplitter horizontal usa uma única linha, permite mouse e setas. A alça das camadas também usa uma linha, sem o antigo relevo duplo. O botão **Apagar tudo…** foi removido; `CLEAR` continua existindo como comando explícito de desenho.
- Preferências em `%LOCALAPPDATA%/DotNetCadEngine/layout.json`: altura/recolhimento do console e largura/recolhimento das camadas. Não há comandos, mensagens ou dados CAD nesse arquivo. Preferências inválidas usam valores padrão. Há restauração de tamanho no menu do cabeçalho.
- O viewport reserva 80 unidades WPF de altura; o console limita sua altura conforme o espaço disponível e mantém a entrada dentro da janela. Em 640×480, o histórico aberto fica compacto; recolhê-lo libera área de desenho.

## Conteúdo e interação

`CadConsoleModel` separa mensagens estruturadas (horário, origem usuário/MCP/sistema, severidade, indicação de comando) do histórico navegável de entrada. Somente a sessão mantém conteúdo: até 1000 mensagens/2 milhões de caracteres, 32768 caracteres por mensagem com truncamento indicado, e 128 entradas de até 8192 caracteres. As mais antigas são descartadas ao atingir os limites.

Há seleção e cópia nativa por TextBox, cópia de mensagens selecionadas/comando, pesquisa, filtro de erros e exportação explícita das mensagens filtradas. Limpar mensagens não chama CLEAR nem altera documento/undo; também não remove o histórico navegável da entrada.

↑/↓ restauram o rascunho; Tab completa nomes de comandos; a dica mostra argumentos; Enter executa. Erro mantém a entrada e o foco. Ctrl+C, Ctrl+Z e Delete continuam comandos de edição do TextBox. Não se persiste automaticamente conteúdo potencialmente sensível.

`CadCommandCatalog` é a fonte única de sintaxe para HELP, autocomplete e dicas, incluindo os comandos de arquivo existentes. A entrada do console inicia um comando novo; operações interativas recebem pontos/parâmetros no viewport. Um guard rejeita novos comandos mutantes durante interação/reentrância, inclusive pela bridge MCP. Há cancelamento explícito da operação. HELP/LIST/PROPERTIES/ZOOM continuam disponíveis durante previews.

Mensagens de outras threads passam pelo dispatcher. A bridge enfileira execução em prioridade Background, preservando a serialização de acesso ao documento. Isso não transforma as rotinas existentes de leitura/gravação de arquivos em I/O assíncrono.

Autoscroll acompanha somente quem estava no final. Ao ler o passado, a posição é preservada e há contador de novas mensagens. Ao descartar mensagens antigas por limite de memória, a primeira mensagem visível é mantida como âncora enquanto ela ainda existir. Conteúdo já descartado não é recuperável.

## Testes automatizados

- Navegação com rascunho, histórico independente de mensagens MCP, limites de memória, truncamento e exportação legível.
- Catálogo compartilhado por HELP/completion/dicas.
- Erro preservando entrada; limpar saída preservando desenho e undo; guard de operação interativa.
- Preferências em arquivo temporário isolado e fallback de arquivo inválido.
- UserControl aberto: 500 mensagens longas, virtualização de containers, pesquisa/filtro, mensagem MCP enquanto lê o passado, contador e âncora após descarte de mensagens antigas.
- Recolher/expandir mantendo entrada, foco após erro, ↑/↓, autocomplete, Delete e undo nativos. O teste de cópia verifica os dados do evento nativo e cancela a escrita para preservar o clipboard do usuário.
- Mensagem enviada por outra thread é aplicada no dispatcher.
- Janela WPF isolada: 640×480, 800×600, 1100×720 e maximizada, histórico aberto e fechado; entrada não recortada, viewport mínimo e GridSplitter pelo teclado. Renderizações em 96/144/192 DPI, em ambiente de DPI real 192 (200%). Saídas de 100% e 150% são renderizações, não mudanças de DPI do Windows.

Artefatos e medidas: `.artifacts/console-tests/bin/DotNetCad.Demo.Tests/debug/layout-validation/` (inclui versões `-expanded.png` e `results.txt`).

## Validação visual separada

Foi aberto o build isolado `.artifacts/console-visual/`, sem substituir arquivos CAD. Inspecionados console expandido em 1100×720 e maximizado, ausência de Apagar tudo e as divisórias únicas de console/camadas. Foi identificado e corrigido contraste do cabeçalho marcado. O rótulo foi depois reduzido para **Console**, conforme solicitação, e conferido na renderização da matriz.

A janela recebeu entrada física durante a inspeção; não foram enviadas novas ações nela para não interferir na sessão em uso. Fluxos completos de erro, cópia, histórico, leitura do passado e redimensionamento foram validados por testes WPF, sem alegação de execução manual de todos eles. O transporte MCP externo e a gravação pelo diálogo Exportar não foram exercitados ponta a ponta; a bridge foi preservada e a lógica compartilhada/serialização da saída foram testadas.
