# Decisões de Arquitetura e Diretrizes Técnicas

**DotNetCadEngine — Registro Consolidado de Decisões e Contratos de Engenharia**

Este documento consolida as diretrizes arquiteturais, políticas de integridade de dados e padrões de interação do sistema, servindo como especificação canônica para evoluções futuras.

---

## 1. Política de Camadas (Layers)

* **Serviço Unificado (`CadLayerOperations`):** Ponto único de validação e execução para operações de camada acionadas pelo painel WPF, comandos de console ou ferramentas MCP. Cada operação gera exatamente uma ação reversível no `UndoRedoManager`.
* **Camadas Protegidas:**
  - A **Camada 0** e camadas com flag `IsProtected == true` não podem ser excluídas nem renomeadas.
  - Proteção contra exclusão não impede desenhar ou editar entidades contidas na camada.
* **Exclusão de Camadas Ocupadas:**
  - O sistema nunca adota a exclusão destrutiva silenciosa por padrão.
  - O usuário/chamador deve escolher explicitamente entre:
    1. **Transferir entidades** para outra camada válida e excluir a camada vazia.
    2. **Excluir camada e todas as entidades nela contidas**.
    3. **Cancelar**.
* **Camada Ativa e Camada Mínima Utilizável:**
  - O documento deve manter sempre ao menos uma camada visível e desbloqueada apta a receber desenho.
  - Se a camada ativa for excluída, ocultada ou bloqueada, uma camada sucessora utilizável deve ser definida de forma atômica.
  - A restauração via `Undo` recupera a camada ativa e os estados de visibilidade/bloqueio anteriores.
* **Bloqueio (`IsLocked == true`):**
  - Entidades em camadas bloqueadas podem ser visualizadas, capturadas por Snaps de precisão e inspecionadas em relatórios.
  - Mutações (edição por grips, exclusão, transformações, corte/extensão ou desenho) são estritamente bloqueadas com feedback explícito.

---

## 2. Console CAD e Interação Textual

* **Separação de Responsabilidades (`CadConsoleModel`):**
  - O histórico de mensagens estruturadas (log com horário, origem e severidade) é desacoplado do histórico de comandos digitados (navegação com `↑` e `↓`).
  - Limpar a saída de mensagens nunca invoca o comando `CLEAR` nem afeta as entidades do desenho ou o histórico de `Undo/Redo`.
* **Catálogo Central de Comandos (`CadCommandCatalog`):**
  - Fonte única de sintaxe para execução, autocompletion (`Tab`), dicas contextuais e o comando `HELP`.
  - Suporte a aliases universais e bilíngues: `TR`, `TRIM`, `APARAR`, `EX`, `EXTEND`, `ESTENDER`.
* **Proteção Contra Reentrância durante Interações no Viewport:**
  - Enquanto uma ferramenta interativa estiver ativa no viewport (`InteractiveOperationActive == true`), comandos mutantes vindos do console ou MCP são bloqueados até a conclusão ou cancelamento (`Esc`) da operação atual. Comandos de inspeção (`HELP`, `LIST`, `PROPERTIES`, `ZOOM`) permanecem disponíveis.

---

## 3. Ferramentas de Modificação Gráfica (Viewport)

* **Ciclo de Vida das Ferramentas:**
  - **Etapa 0 (Seleção de Entidades / Limites):** Se entidades já estiverem previamente selecionadas, a ferramenta aceita a seleção imediatamente e avança para a próxima etapa. Caso contrário, permite selecionar interativamente e confirmar com `Enter` ou botão direito.
  - **Etapa 1 (Aplicação / Parâmetros):** As ferramentas operam de forma direta no viewport:
    - `Mover` / `Copiar`: Concluem no ponto de destino.
    - `Rotacionar`: Conclui na direção ou ângulo final.
    - `Espelhar`: Dois pontos definem o eixo e um menu contextual define se mantém ou substitui o original.
    - `Offset`: Aceita medição por dois pontos ou distância digitada com escolha de lado pelo cursor.
    - `Trim` / `Extend`: Permitem cliques sucessivos para múltiplos cortes ou extensões, mantendo a ferramenta ativa até confirmação com `Enter`.
* **Pré-visualizações Não-Mutativas (*Zero-Mutation Previews*):**
  - O cálculo de *hover* e guias visuais ocorre estritamente em memória durante `MouseMove`.
  - Nunca altera o `CadDocument` nem empilha ações no `UndoRedoManager` antes do clique definitivo.
  - Guias padronizadas:
    - **Limites de Corte/Extensão:** Linha tracejada âmbar.
    - **Trecho a Remover (Trim):** Linha tracejada vermelha com ponto de corte.
    - **Extensão Proposta (Extend):** Linha tracejada verde com ponto de interseção projetado.

---

## 4. Precisão Geométrica & Política de Tolerâncias (Core)

* **Tolerâncias Unificadas (`GeometricTolerancePolicy`):**
  - Elimina números mágicos ou tolerâncias arbitrárias espalhadas no código.
  - Configurações por unidade de desenho:
    - `PointCoincidenceTolerance`: Coincidência de pontos (`1e-6` m).
    - `CollinearTolerance`: Paralelismo e colinearidade angular.
    - `MathEpsilon`: Determinantes e limites de singularidade (`1e-9`).
    - `MinEntityDimension`: Comprimento mínimo de segmentos para evitar geometrias degeneradas.
* **Política de Preservação de IDs em Subdivisões:**
  - Em operações de divisão ou corte (`Trim`, `Break`), o primeiro fragmento (ou fragmento principal) preserva o `Guid` da entidade original.
  - Quaisquer novos subsegmentos criados recebem novos identificadores `Guid.NewGuid()`.
  - Garante rastreabilidade contínua de entidades em sessões de automação e AI/MCP.

---

## 5. CAD Orientado por IA e Transações (Sprint 4)

* **Sessão de Staging de IA (`CadAiStagingSession`):**
  - Permite que agentes externos proponham lotes de adições, modificações e exclusões em um buffer de pré-visualização na tela sem comitar de imediato no documento de trabalho.
  - O usuário pode inspecionar visualmente a proposta do agente antes de acionar **Commit** ou **Reject**.
* **Processamento em Lote com Rollback Atômico (`CadBatchProcessor`):**
  - Execução transacional de múltiplos comandos textuais.
  - Se qualquer comando do lote falhar (ex.: tentativa de editar entidade em camada bloqueada ou sintaxe inválida), todo o lote é revertido atomicamente, sem alterações parciais no desenho.
* **Consultas Semânticas Espaciais (`CadSemanticQueryEngine`):**
  - Suporte a filtros semânticos avançados para LLMs (ex.: "todas as linhas na camada PAREDES com comprimento maior que 5 metros").
* **Trilha de Auditoria (`CadAiAuditTrail`):**
  - Registro de eventos com carimbo de tempo, autor (usuário vs. agente MCP), comando executado e reversibilidade.
