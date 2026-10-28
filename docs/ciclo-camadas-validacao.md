# Ciclo de camadas — validação

Implementação em 28/10/2026. As alterações anteriores do workspace foram preservadas.

## Política

`CadLayerOperations` é o serviço comum do painel, console e bridge MCP. Cada operação gera um undo. Uma camada utilizável é visível e desbloqueada; ocultar, bloquear ou excluir a ativa exige sucessora utilizável. `0` e `IsProtected` impedem renomeação/exclusão.

Bloqueio permite visualizar, selecionar para inspeção e capturar pontos, mas impede mutações de entidades, inclusive lotes mistos, transferência, exclusão, grips e operações interativas. Undo/redo reaplica os estados previamente validados; não depende de revalidar permissões atuais para restaurar o passado.

As cores atuais das entidades são explícitas. Cor igual ao RGB de camada não é ByLayer. Não foi criado um novo sistema de estilos. A cor da camada passa a valer para novas entidades criadas nela; cores explícitas anteriores são preservadas. Arquivos nativos preservam propriedades completas. DXF preserva visibilidade e bloqueio pelas flags convencionais; `IsProtected` personalizado não tem equivalente implementado em DXF (camada 0 continua protegida pelo nome). Arquivos nativos antigos com ativa bloqueada recebem sucessora utilizável na leitura.

## Console e MCP

Nomes com espaços usam aspas no console. `LAYER nome` continua criando/ativando; `LAYER CREATE nome` cria sem ativar. Operações: `ACTIVE`, `SHOW`, `HIDE`, `LOCK`, `UNLOCK`, `COLOR`, `RENAME`, `SHOWALL`, `TRANSFER` e `DELETE`.

Exemplos:

```text
LAYER CREATE "Paredes novas"
LAYER RENAME "Paredes novas" "Paredes finais"
LAYER TRANSFER "Paredes finais"
LAYER DELETE "Paredes finais" TRANSFER Geometry
LAYER DELETE "Paredes finais" ENTITIES
LAYER DELETE "Paredes finais" CANCEL
```

`TRANSFER` sem IDs usa a seleção; pode receber IDs separados por espaços para automação. MCP expõe ferramentas de criação, renomeação, bloqueio, exclusão e transferência, além das existentes; describe inclui bloqueio, proteção, seleção de entidades e contagens. O transporte externo MCP não foi exercitado ponta a ponta: testes usam o mesmo processador chamado pela bridge.

## Testes automatizados

Core: ciclo criar/renomear/excluir, camada ocupada/ativa/protegida, transferência, destino inválido, cancelamento, nomes com espaços, duplicatas case-insensitive, última utilizável, bloqueio de operações, undo/redo, cores explícitas e roundtrip nativo/DXF.

WPF: seleção de linha distinta de ativa, filtro, contagens, checkbox recusado retornando ao estado real, última utilizável pelo painel/console, origem MCP, lote bloqueado, Delete, ausência de grips editáveis e desenho em camada bloqueada.

A matriz WPF abre janela isolada com MCP desabilitado, 60 camadas com nomes longos e renderiza 640×480, 800×600, 1100×720 e maximizada. Artefatos em `.artifacts/layers-tests/bin/DotNetCad.Demo.Tests/debug/layout-validation/`. DPI nativo observado: 192 (200%); saída em 96/144/192 DPI é renderização, não mudança da escala do Windows.

## Verificação visual real

Foi aberto build separado em `.artifacts/layers-visual/`, em 1100×720. Foram inspecionados painel, contagem Geometry=4, Dimensions=1 e Hidden=2, indicação de ativa, bloqueio, campo de cor e rolagem. Menu de contexto aberto com botão direito em Geometry: ativar, renomear, bloquear/desbloquear, transferir seleção e excluir.

Foi aberto o diálogo de exclusão sem confirmar remoção. Mostrou Geometry com 4 entidades, destino de transferência, opção explícita de excluir também as 4 entidades e Cancelar. Esc fechou o diálogo; contagens permaneceram iguais e Desfazer continuou desabilitado. A instância de teste foi fechada sem salvar ou substituir arquivos existentes. Os demais cenários são cobertos por testes automatizados, sem alegação de verificação manual de todos os diálogos.
