# PLANO DE EXECUÇÃO OPERACIONAL — EVOLUÇÃO DO DOTNETCAD ENGINE & SPRINT 4 (CAD ORIENTADO POR IA)

**Repositório Alvo:** `C:\Users\adria\source\repos\DotNetCadEngine` (`https://github.com/led-21/DotNetCadEngine`)  
**Stack:** C# 13 / .NET 10, WPF (MVVM), Named Pipes IPC, Model Context Protocol (`DotNetCad.Mcp`), xUnit  
**Objetivo:** Elevar o `DotNetCadEngine` de um protótipo funcional para um motor CAD 2D de interoperabilidade profissional, precisão geométrica parametrizada, resiliência desktop comprovada e arquitetura nativa para agentes de IA (Sprint 4).

---

## 1. MATRIZ DE RECOMENDAÇÃO DE MODELO DE IA POR WORKSTREAM

A pergunta estratégica central é: **Qual modelo utilizar em cada etapa (Gemini 4 / Pro, Sol, Astra ou Gemini 3.8 Flash)?**

| Workstream / Fase | Complexidade Cognitiva | Modelo Principal Recomendado | Papel do Gemini 3.8 Flash | Justificativa Técnica |
| :--- | :--- | :--- | :--- | :--- |
| **Épico 1 (P1): Interoperabilidade DXF Profissional** | Média (Especificação de formato + Parser de estados) | **Gemini 3.8 Flash** (com este plano detalhado) | Execução integral do parser, exporter e testes de round-trip | Como a matriz de códigos DXF (grupos `0`, `2`, `8`, `10-31`, `40-51`, `62`, `70`, `90`, `VERTEX`/`SEQEND`, `DIMENSION`) já está formalizada abaixo, um modelo rápido e econômico executa com alta precisão. |
| **Épico 2 (P1): Precisão e Operações Geométricas** | **Muito Alta** (Álgebra linear, interseções curva-curva, offset não-convexo, estabilidade numérica IEEE 754) | **Gemini 4 (Pro / Thinking)** ou **Astra** | Geração de casos de teste parametrizados (`[Theory]`) após o núcleo matemático estar pronto | Operações como `Offset` de polilinhas côncavas (auto-interseção de bissetrizes), `Trim`/`Extend` contra arcos/círculos e tolerâncias relativas à escala exigem raciocínio matemático profundo. Modelos leves tendem a introduzir bugs sutis de sinais ou divisão por zero em casos degenerados. |
| **Épico 3 (P1): Testes de Integração e Experiência WPF** | Média-Alta (Threading STA, ciclo de vida WPF, simulação de IO e DPI) | **Gemini 3.8 Flash** (guiado por contratos) + revisão pontual **Gemini 4** | Escrita massiva das suítes de estresse, cancelamento `ESC`, falhas de disco e matriz de DPI | O padrão de testes STA já existe em `DotNetCad.Demo.Tests`. O Flash 3.8 é excelente para expandir cobertura de testes sistemáticos seguindo templates claros. |
| **Sprint 4: CAD Orientado por IA (MCP, Transações, Preview, Semântica e Auditoria)** | **Crítica / Arquitetural** (Concorrência IPC, transações atômicas, máquina de estados de Staging/Preview na UI e grafo espacial) | **Gemini 4 (Arquitetura & Core)** + **Gemini 3.8 Flash (Tools & UI Bindings)** | Implementação dos DTOs, comandos de console secundários, serialização de auditoria e testes | A Sprint 4 transforma o CAD em um ambiente transacional para agentes. O desenho do `CadTransactionManager`, do buffer de `StagingEntities` (Preview/Commit/Reject) e do motor de consultas semânticas espaciais deve ser feito pelo modelo de maior capacidade (**Gemini 4 / Astra**). |

### Resumo da Escolha para a Sprint 4 (CAD IA):
1. **Melhor escolha absoluta para liderar a Sprint 4 e a Matemática Geométrica (P1):** **Gemini 4** (ou **Astra**, caso configurado no seu ambiente como o perfil de raciocínio avançado/arquitetura). Ele evita condições de corrida entre o servidor `NamedPipe` e a `Dispatcher` thread do WPF durante o Preview/Commit de transações em lote.
2. **Como otimizar custos (Estratégia Híbrida):** Use **Gemini 4** para implementar o núcleo transacional (`CadTransactionScope`, `CadStagingManager`, `CadSemanticQueryEngine` e `CadConstructionMath`) e delegue ao **Gemini 3.8 Flash** a exposição das 15+ ferramentas em `CadTools.cs`, os testes de contrato JSON e a suíte de round-trip DXF.

---

## 2. ÉPICO 1 (P0/P1) — EVOLUIR DXF PARA INTEROPERABILIDADE PROFISSIONAL

### 2.1 Diagnóstico do Código Atual (`DxfCadExporter.cs` e `DxfCadImporter.cs`)
1. **Inconsistência de Versão no Header:** `DxfCadExporter` declara `$ACADVER = AC1009` (AutoCAD R12), mas exporta `LWPOLYLINE` (código `90`), que só existe a partir do AutoCAD R14 (`AC1014` / `AC1015`).
2. **Perda de Cor Explícita por Entidade:** `DxfCadExporter` grava a cor apenas na tabela `LAYER`, omitindo o código de grupo `62` (cor ACI por entidade) e `420` (TrueColor 24-bit) nas entidades `LINE`, `CIRCLE`, `ARC`, `LWPOLYLINE` e `TEXT`.
3. **Explosão Semântica de Cotas (`CadDimension`):** No exportador atual (linhas 61–69 de `DxfCadExporter.cs`), `CadDimension` é decomposta em 3 `LINE` + 1 `TEXT`. Ao reimportar o DXF, a entidade deixa de ser uma cota editável.
4. **Falha em `POLYLINE` Clássica (R12):** `DxfCadImporter` (linha 203) trata `POLYLINE` igual a `LWPOLYLINE`, ignorando que `POLYLINE` armazena vértices em sub-entidades `0\nVERTEX` finalizadas por `0\nSEQEND`.
5. **Unidades (`$INSUNITS`) Ignoradas na Importação:** `DxfCadExporter` escreve `$INSUNITS` no `HEADER`, mas `DxfCadImporter` ignora a seção `HEADER` completamente.
6. **Ausência de Relatório Estruturado de Perda/Avisos:** `DxfImportPayload` existe no arquivo mas não é retornado nem populado com estatísticas de entidades ignoradas, propriedades degradadas ou avisos de compatibilidade.

### 2.2 Matriz Formal de Compatibilidade DXF (Criar em `docs/DXF-COMPATIBILITY-MATRIX.md`)

| Categoria | Recurso / Entidade DXF | Versões Alvo (`AC1009` R12 / `AC1015` R2000) | Suporte na Importação | Suporte na Exportação | Fidelidade de Round-Trip & Perdas Documentadas |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Header** | `$ACADVER`, `$INSUNITS`, `$EXTMIN`, `$EXTMAX` | R12 / R2000 (`AC1015` padrão na exportação) | Lê `$ACADVER` e `$INSUNITS` (mapeia para `CadUnit`) | Escreve `AC1015`, `$INSUNITS`, `$EXTMIN`/`$EXTMAX` | 100% preservado para unidades suportadas (`mm`, `cm`, `m`, `in`, `ft`). |
| **Tables** | `LAYER` (`2` Nome, `62` ACI, `420` TrueColor, `70` Flags, `6` Linetype) | R12 / R2000 | Lê nome, visibilidade (`62 < 0`), lock (`flag & 4`), cor ACI e TrueColor (`420`) | Escreve nome, visibilidade, lock, ACI (`62`), TrueColor (`420`) e `CONTINUOUS` | Linetypes customizados (`DASHED`, `CENTER`) são normalizados para `CONTINUOUS` com aviso no `DxfImportReport`. |
| **Entity** | `LINE` (`8`, `10/20/30`, `11/21/31`, `62`, `420`) | R12 / R2000 | Completo (2D; `Z` descartado com aviso se `!= 0`) | Completo (precisão `R` / `G17` ou `F6` configurável) | Coordenadas `Z` (`30`, `31`) projetadas no plano `Z = 0`. |
| **Entity** | `CIRCLE` (`8`, `10/20`, `40`, `62`, `420`) | R12 / R2000 | Completo | Completo | 100% em 2D. |
| **Entity** | `ARC` (`8`, `10/20`, `40`, `50`, `51`, `62`, `420`) | R12 / R2000 | Completo | Completo (precisão angular aumentada de `F2` para `F6`) | 100% em 2D. |
| **Entity** | `LWPOLYLINE` (`8`, `70`, `90`, `10/20`, `42` Bulge) | R14 / R2000 | Lê vértices e flag `Closed` (`70 & 1`); se `Bulge (42) != 0`, tesselar ou registrar aviso | Exporta `CadPolyline` e `CadRectangle` (com `XData` opcional ou preservação de polilinha fechada) | `CadRectangle` exportado como `LWPOLYLINE` fechada de 4 vértices ortogonais (ou reconstruído como `CadPolyline` fechada). |
| **Entity** | `POLYLINE` + `VERTEX` + `SEQEND` | R12 / R2000 | Lê sequência completa de `VERTEX` até `SEQEND` | Convertido para `LWPOLYLINE` no export R2000 | Compatibilidade total de leitura com arquivos legados R12. |
| **Entity** | `TEXT` & `MTEXT` (`8`, `10/20`, `40`, `50`, `1`, `3`) | R12 / R2000 | `TEXT` completo; `MTEXT` converte códigos de formatação simples (`\P` -> espaço) em `CadText` | Exporta como `TEXT` | Formatação rica de `MTEXT` (fontes inline) é limpa e registrada em `Warnings`. |
| **Entity** | `DIMENSION` (Aligned / Linear) | R12 / R2000 | Lê pontos de definição (`10/20`, `13/23`, `14/24`) e reconstrói `CadDimension` | Exporta entidade `DIMENSION` padrão + geometria visual compatível | Preserva a semântica de `CadDimension` no round-trip! |

### 2.3 Tarefas de Implementação (Passo a Passo)
1. **Criar `src/DotNetCad.Infrastructure/Dxf/DxfCompatibilityReport.cs`:**
   - Definir `public sealed record DxfImportWarning(string Code, string EntityType, int LineOrPairIndex, string Message);`
   - Definir `public sealed class DxfImportResult { public required CadDocument Document { get; init; } public string DetectedAcadVersion { get; init; } = "UNKNOWN"; public int ImportedEntityCount { get; init; } public int SkippedEntityCount { get; init; } public List<DxfImportWarning> Warnings { get; init; } = []; }`
2. **Atualizar `src/DotNetCad.Infrastructure/Dxf/DxfCadExporter.cs`:**
   - Alterar `$ACADVER` para `AC1015` (AutoCAD 2000) por padrão, mantendo opção `DxfExportVersion.R12` (onde polilinhas usam `POLYLINE`/`VERTEX`/`SEQEND`).
   - Aumentar a precisão numérica de `F4`/`F2` para `F6` (evita erro de truncamento de `0.01°` em arcos e `0.0001m` em coordenadas).
   - Gravar os códigos `62` (ACI) e `420` (RGB inteiro 24-bit) em **todas** as entidades exportadas para que cores customizadas Hex (`#RRGGBB`) sobrevivam 100% ao round-trip sem depender apenas dos 9 códigos ACI aproximados.
   - Exportar `CadDimension` como entidade `0\nDIMENSION` (`10/20` linha de cota, `13/23` `StartPoint`, `14/24` `EndPoint`, `1` `OverrideText`, `70` `1` = Aligned), preservando o objeto `CadDimension` ao reimportar.
3. **Atualizar `src/DotNetCad.Infrastructure/Dxf/DxfCadImporter.cs`:**
   - Adicionar `ImportFromDxfWithReport(string dxfContent, string documentTitle = "ImportedDrawing.dxf") -> DxfImportResult` (mantendo `ImportFromDxf` como wrapper compatível).
   - Adicionar `ParseHeaderSection` para ler `$ACADVER` e `$INSUNITS` (atualizando `document.Preferences.DisplayUnit`).
   - Suportar `POLYLINE` multi-record (`VERTEX` ... `SEQEND`), `MTEXT` básico e `DIMENSION` (`10/20`, `13/23`, `14/24`).
   - Ler o código `420` (TrueColor) além do `62` (ACI) em layers e entidades.
4. **Expandir `tests/DotNetCad.Core.Tests/DxfRoundTripTests.cs`:**
   - Adicionar testes de round-trip cobrindo simultaneamente: múltiplas layers (visíveis, ocultas, bloqueadas, cores RGB arbitrárias), `CadLine`, `CadCircle`, `CadArc` (com ângulos fracionários), `CadPolyline` (aberta e fechada), `CadText` (com rotação e acentuação UTF-8), `CadDimension` (verificando que volta como `CadDimension` e não linhas soltas) e leitura de `POLYLINE`/`VERTEX`/`SEQEND` legado.

---

## 3. ÉPICO 2 (P1) — APRIMORAR A PRECISÃO E AS OPERAÇÕES GEOMÉTRICAS

### 3.1 Diagnóstico dos Limites Fixos Atuais
- Em `GeometryValidator.cs`: `minLengthMeters = 0.01`, `minDimension = 0.05`, `minRadius = 0.05`, `minArea = 0.01`, `DefaultEpsilon = 1e-4`.
- Em `CadConstructionMath.cs`:
  - `IsPointOnSegment` usa `Math.Abs((dist1 + dist2) - distTotal) <= 0.05` (5 cm de folga e baixa precisão numérica!).
  - `OffsetPolygon` exige polígono estritamente convexo (`ValidateConvex`), rejeitando qualquer polilinha aberta, retângulo com vértice intermediário colinear ou geometria em "L" / "U".
  - `TrimSegment` e `ExtendSegment` só operam entre dois segmentos de reta (`Point2D p1, p2` vs `boundA, boundB`), não suportando `CadCircle`, `CadArc` ou `CadPolyline` como fronteira de corte/extensão.

### 3.2 Política Explícita de Tolerâncias Geométricas (`GeometricTolerancePolicy`)
Criar `src/DotNetCad.Core/Geometry/GeometricTolerancePolicy.cs`:

```csharp
using DotNetCad.Core.Measurements;

namespace DotNetCad.Core.Geometry;

public sealed record GeometricTolerancePolicy
{
    /// <summary>Tolerância numérica para colinearidade, paralelismo e determinantes (adimensional/relativa).</summary>
    public double MathEpsilon { get; init; } = 1e-9;

    /// <summary>Distância mínima entre dois pontos distintos em unidades do desenho.</summary>
    public double PointCoincidenceTolerance { get; init; } = 1e-5;

    /// <summary>Tolerância de pertinência de ponto a segmento/arco (projeção ortogonal).</summary>
    public double OnCurveTolerance { get; init; } = 1e-4;

    /// <summary>Comprimento mínimo de segmento válido em unidades do desenho.</summary>
    public double MinEntityDimension { get; init; } = 1e-3;

    /// <summary>Área mínima para polígonos fechados em unidades quadradas do desenho.</summary>
    public double MinPolygonArea { get; init; } = 1e-4;

    /// <summary>Ângulo mínimo de varredura de arco em graus.</summary>
    public double MinArcSweepDegrees { get; init; } = 0.05;

    public static GeometricTolerancePolicy Default { get; } = new();

    public static GeometricTolerancePolicy ForUnit(CadUnit unit) => unit switch
    {
        CadUnit.Millimeters => new()
        {
            MathEpsilon = 1e-9,
            PointCoincidenceTolerance = 1e-3, // 0.001 mm
            OnCurveTolerance = 1e-2,          // 0.01 mm
            MinEntityDimension = 0.1,         // 0.1 mm
            MinPolygonArea = 0.01
        },
        CadUnit.Centimeters => new()
        {
            MathEpsilon = 1e-9,
            PointCoincidenceTolerance = 1e-4,
            OnCurveTolerance = 1e-3,
            MinEntityDimension = 0.01,
            MinPolygonArea = 1e-4
        },
        CadUnit.Meters or _ => Default
    };
}
```

### 3.3 Revisão Matemática das Operações Críticas (`CadConstructionMath.cs`)
1. **Substituir `IsPointOnSegment` por Projeção Paramétrica + Distância Ortogonal:**
   - Calcular o parâmetro $t = \frac{(P - A) \cdot (B - A)}{\|B - A\|^2}$.
   - O ponto pertence ao segmento se $- \varepsilon_t \le t \le 1 + \varepsilon_t$ (onde $\varepsilon_t = \text{OnCurveTolerance} / \|B - A\|$) **e** a distância perpendicular $\|P - (A + t(B - A))\| \le \text{OnCurveTolerance}$. Isso elimina completamente o erro da desigualdade triangular perto das extremidades.
2. **Motor Unificado de Interseções 2D (`src/DotNetCad.Core/Geometry/CadIntersectionEngine.cs`):**
   - Implementar interseções exatas e testadas em múltiplas escalas ($10^{-3}$ a $10^{6}$):
     - `IntersectSegmentSegment(Point2D a1, Point2D a2, Point2D b1, Point2D b2, GeometricTolerancePolicy tol)` (incluindo detecção de sobreposição colinear / endpoints compartilhados).
     - `IntersectLineCircle(Point2D p1, Point2D p2, Point2D center, double radius, bool segmentOnly, GeometricTolerancePolicy tol) -> IReadOnlyList<Point2D>` (equação quadrática estabilizada com discriminante $\Delta \ge -\varepsilon$).
     - `IntersectLineArc(Point2D p1, Point2D p2, CadArc arc, bool segmentOnly, GeometricTolerancePolicy tol) -> IReadOnlyList<Point2D>` (filtra pontos da interseção reta-círculo pelo intervalo angular CCW `[StartAngle, EndAngle]` do arco).
     - `IntersectEntityEntity(ICadEntity a, ICadEntity b, GeometricTolerancePolicy tol) -> IReadOnlyList<Point2D>` (suporta `CadLine`, `CadPolyline`, `CadRectangle`, `CadCircle`, `CadArc`).
3. **Evoluir `Offset`, `Trim` e `Extend`:**
   - **Offset de Polilinhas Abertas e Polígonos Côncavos (`OffsetPolyline`):**
     - Permitir `Offset` em `CadPolyline` aberta (calculando bissetrizes nos vértices internos e projetando as extremidades abertas perpendicularmente) e em polígonos simples côncavos (como plantas em "L" ou "U"), removendo vértices consecutivos colineares dentro de `PointCoincidenceTolerance` e detectando auto-interseção de arestas resultantes.
     - Suportar `OFFSET` de `CadArc` (raio $\pm d$ mantendo `StartAngle`/`EndAngle`, validando $R + d \ge \text{MinEntityDimension}$) e `CadRectangle` no `CadCommandProcessor`.
   - **Trim e Extend Polimórficos:**
     - Permitir que a fronteira (`boundaryId`) no comando `TRIM` e `EXTEND` seja qualquer entidade geométrica (`CadLine`, `CadPolyline`, `CadRectangle`, `CadCircle`, `CadArc`), escolhendo a interseção mais próxima na direção da extensão (`EXTEND`) ou dividindo o segmento no ponto de corte mais próximo ao `pickPoint` (`TRIM`).
4. **Suíte de Testes de Precisão e Escala (`tests/DotNetCad.Core.Tests/GeometryScaleAndPrecisionTests.cs`):**
   - Testar `Offset`, `Trim`, `Extend` e `Intersect` em 3 escalas: micro ($0.001$ a $1.0$), arquitetônica ($1.0$ a $100.0$) e geodésica/infraestrutura ($10\,000.0$ a $500\,000.0$), além de casos degenerados (segmentos quase paralelos a $10^{-7}\text{ rad}$, tangências exatas reta-círculo, cantos a $179.99^\circ$).

---

## 4. ÉPICO 3 (P1) — TESTES DE INTEGRAÇÃO E EXPERIÊNCIA WPF

### 4.1 Cenários Críticos a Implementar em `tests/DotNetCad.Demo.Tests/`

1. **`ProlongedSessionStressTests.cs` (Sessões Prolongadas & Integridade de Histórico):**
   - Executar um loop determinístico de **500 operações consecutivas** misturando: criação de entidades (`LINE`, `CIRCLE`, `ARC`, `POLYLINE`, `TEXT`, `DIM`), criação/renomeação/bloqueio de layers, modificações (`MOVE`, `COPY`, `ROTATE`, `OFFSET`, `TRIM`, `EXTEND`), seguidas de **250 `Undo()`**, **150 `Redo()`**, **100 novas edições** (que truncam a pilha de Redo) e **Undo total até a origem**.
   - **Asserções:** Verificar que a contagem de entidades, camadas e o hash estrutural do `CadDocument` coincidem bit a bit com os checkpoints esperados e que nenhuma referência órfã permanece na seleção (`SelectedEntities`).
2. **`CommandCancellationAndFocusTests.cs` (Cancelamento `ESC` & Foco de Teclado):**
   - Simular início de ferramentas interativas (`Line`, `Polyline` com 2 pontos já clicados, `Move`/`Rotate`/`Offset` no meio da `CadModificationSession`) e disparar `Key.Escape` / `CancelActiveTool()`.
   - **Asserções:** Garantir que nenhuma entidade fantasma parcial foi adicionada ao `CadDocument`, o `UndoRedoManager` não recebeu ações incompletas, o prompt de status volta a `"Pronto"` e o foco de teclado no console de comando (`CadConsoleModel`) vs. `Viewport` trata teclas de atalho (`Delete`, `Escape`, `Enter`, `Up`/`Down` de histórico) sem engolir caracteres de digitação.
3. **`DiskAndIoResilienceTests.cs` (Falhas de Disco, Arquivos Corrompidos e Bloqueados):**
   - Testar `CadDrawingSerializer`, `DxfCadImporter`, `CadRecoveryService` e `CadRecentFilesService` contra:
     - Arquivo `.cad` truncado no meio do JSON ou com schema inválido;
     - Arquivo `.dxf` com pares código/valor ímpares (truncado) ou coordenadas `NaN`/`1e999`;
     - Arquivo aberto com `FileShare.None` (bloqueio exclusivo de outro processo) durante `SAVE` / `OPEN` e diretório sem permissão/inexistente;
   - **Asserções:** Garantir que o documento aberto atual na memória **nunca** é corrompido ou zerado quando uma operação `OPEN` ou `SAVE` falha em disco, retornando mensagem de erro clara para o usuário.
4. **`DpiAndResolutionMatrixTests.cs` (Múltiplas Resoluções e Escalas de DPI):**
   - Parametrizar `[Theory]` combinando resoluções (`1280x720`, `1366x768`, `1920x1080`, `2560x1440`, `3840x2160`) e escalas de DPI do Windows (`1.0` = 100%, `1.25` = 125%, `1.5` = 150%, `2.0` = 200%).
   - **Asserções:** Validar `ViewportTransform` (`WorldToScreen` ↔ `ScreenToWorld` round-trip sob diferentes DPIs), `ZoomExtents`, hit-testing do `CadSnapEngine` / `CadSelectionEngine` em pixels lógicos WPF (DIPs) e ausência de clipping/sobreposição nos painéis do `MainWindow` (`LayoutMatrixTests`).

---

## 5. SPRINT 4 — CAD ORIENTADO POR IA (ARQUITETURA & EXECUÇÃO)

### 5.1 Contratos MCP Estruturados (`src/DotNetCad.Infrastructure/Mcp/CadMcpContracts.cs`)
Substituir o retorno puramente textual por envelopes JSON fortemente tipados (mantendo compatibilidade retroativa):

```csharp
namespace DotNetCad.Infrastructure.Mcp;

public sealed record CadMcpStructuredResponse<T>(
    bool Success,
    string Code,              // Ex: "OK", "LAYER_LOCKED", "GEOMETRY_DEGENERATE", "ENTITY_NOT_FOUND"
    string Message,
    T? Data = default,
    string? SuggestedFix = null,
    Guid? TransactionId = null,
    IReadOnlyList<Guid>? AffectedEntityIds = null
);
```

- Quando qualquer comando MCP cria ou altera entidades, `AffectedEntityIds` retorna exatamente os `Guid` gerados/modificados, eliminando a necessidade de o agente chamar `describe_drawing` após cada linha desenhada.

### 5.2 Operações em Lote e Transações Atômicas (`execute_batch`)
1. **Criar `src/DotNetCad.Core/Editor/CadBatchProcessor.cs`:**
   - Recebe uma lista de comandos (`IReadOnlyList<string> Commands`), um rótulo de transação (`string TransactionLabel`) e metadados do agente (`string AgentName`).
   - Clona o estado ou executa cada comando acumulando as `IUndoableAction` em uma única `CompositeUndoableAction`.
   - **Atomicidade (`All-or-Nothing`):** Se o comando #7 de um lote de 20 comandos falhar (ex.: linha degenerada ou layer bloqueada), o processador reverte imediatamente os comandos #1 a #6, restaura o documento ao estado exato anterior ao lote e retorna o erro estruturado indicando o índice `FailedCommandIndex = 6`.
   - **Undo Único:** Se todos os 20 comandos tiverem sucesso, apenas **um** item (`CompositeUndoableAction` com descrição `"[IA] Subestação 300kVA (20 operações)"`) entra na pilha de `UndoRedoManager`. Um único `Ctrl+Z` do engenheiro desfaz o lote inteiro da IA.
2. **Expor Tool MCP `execute_batch` em `src/DotNetCad.Mcp/CadTools.cs`:**
   - `Task<string> ExecuteBatch(string[] commands, string transactionName, bool stageForPreview = false)`

### 5.3 Preview e Confirmação de Ações da IA (`Staging / Ghost Preview`)
Para que o engenheiro tenha controle humano sobre o que a IA propõe antes de alterar definitivamente o desenho:
1. **Criar `src/DotNetCad.Core/Editor/CadAiStagingSession.cs`:**
   - Mantém uma proposta pendente (`Guid ProposalId`, `string Description`, `string AgentId`, `List<ICadEntity> AddedEntities`, `List<Guid> RemovedEntityIds`, `List<(Guid OriginalId, ICadEntity Replacement)> ModifiedEntities`).
   - Enquanto houver uma sessão de staging ativa, `CadDocument.StagedProposal` expõe as entidades propostas.
2. **Renderização Visual no WPF (`CadViewportControl` / `MainViewModel`):**
   - Entidades em `StagedProposal.AddedEntities` são desenhadas no canvas com estilo tracejado ciano/esmeralda brilhante (`StrokeDashArray = "4, 2"`, opacidade `0.80`) e um banner superior no viewport:
     - **`Proposta da IA pendente: "{Description}" (N entidades)`** com botões **`[Aceitar (Commit)]`** e **`[Rejeitar (Discard)]`**.
3. **Tools MCP de Staging:**
   - `stage_batch(string[] commands, string summary)` -> Cria o preview visual no CAD sem alterar o documento definitivo.
   - `commit_staged_proposal(string proposalId)` -> Aplica a proposta como uma única `CompositeUndoableAction` e registra na auditoria.
   - `discard_staged_proposal(string proposalId)` -> Limpa o preview sem tocar no documento.

### 5.4 Consultas Semânticas ao Documento (`src/DotNetCad.Core/Queries/CadSemanticQueryEngine.cs`)
Agentes de IA sofrem quando precisam ler centenas de linhas de texto bruto em `describe_drawing`. Criar consultas estruturadas de alto nível:
1. **`query_entities` (Filtro Espacial e por Atributos):**
   - Filtra por `layer`, `entityTypes` (`LINE`, `CIRCLE`, `TEXT`, etc.), `boundingBox` (`minX, minY, maxX, maxY` — *Window* ou *Crossing*), e `textContains`.
2. **`find_intersections` & `find_spatial_conflicts` (Detecção de Colisões / Interferências 2D):**
   - Retorna todos os pares de entidades (ex.: entre a camada `ELETRICA` e `DRENAGEM` ou `ESTRUTURA`) que se interceptam ou cuja distância mínima é inferior a um `minClearance` especificado!
3. **`analyze_drawing_topology` (Resumo Semântico por Camada e Regiões Fechadas):**
   - Retorna estatísticas por camada (contagem por tipo, comprimento linear total de linhas/polilinhas para quantitativos de engenharia, área total de polígonos fechados e textos contidos dentro de cada região fechada).

### 5.5 Auditoria das Alterações Realizadas por Agentes (`src/DotNetCad.Core/Audit/CadAiAuditTrail.cs`)
Para conformidade técnica (rastreabilidade de autoria humano vs. assistente MCP):
1. **Modelo `CadAiAuditEntry`:**
   ```csharp
   public sealed record CadAiAuditEntry(
       Guid AuditId,
       DateTimeOffset TimestampUtc,
       string ActorType,          // "HumanConsole", "HumanUi", "McpAgent"
       string ActorIdentifier,    // Ex: "DotNetCad.Mcp / Claude-Desktop"
       string OperationName,      // Ex: "execute_batch", "add_line", "trim_line"
       Guid? TransactionId,
       string Summary,
       IReadOnlyList<Guid> CreatedEntityIds,
       IReadOnlyList<Guid> ModifiedEntityIds,
       IReadOnlyList<Guid> DeletedEntityIds
   );
   ```
2. **Persistência no Arquivo `.cad` (`CadDrawingSerializer.cs`):**
   - Incluir `List<CadAiAuditEntry> AiAuditHistory` em `CadDocument` e serializar junto com o arquivo `.cad` nativo.
   - Expor comando `AUDIT` no console e tool MCP `get_audit_history(int limit = 50)`.

---

## 6. ORDEM DE EXECUÇÃO E COMANDOS DE VERIFICAÇÃO

Cada etapa deve manter `0 erros` de compilação e `100%` dos testes passando:

1. **Fase 1 (DXF):** Implementar `DxfCompatibilityReport`, atualizar `DxfCadExporter` / `DxfCadImporter` e rodar:
   ```powershell
   dotnet test C:\Users\adria\source\repos\DotNetCadEngine\tests\DotNetCad.Core.Tests\DotNetCad.Core.Tests.csproj --filter "FullyQualifiedName~Dxf"
   ```
2. **Fase 2 (Geometria P1):** Implementar `GeometricTolerancePolicy`, `CadIntersectionEngine`, evoluir `Offset`/`Trim`/`Extend` e rodar:
   ```powershell
   dotnet test C:\Users\adria\source\repos\DotNetCadEngine\tests\DotNetCad.Core.Tests\DotNetCad.Core.Tests.csproj
   ```
3. **Fase 3 (Testes WPF P1):** Adicionar `ProlongedSessionStressTests`, `CommandCancellationAndFocusTests`, `DiskAndIoResilienceTests` e `DpiAndResolutionMatrixTests`:
   ```powershell
   dotnet test C:\Users\adria\source\repos\DotNetCadEngine\DotNetCadEngine.slnx
   ```
4. **Fase 4 (Sprint 4 — CAD IA):** Implementar `CadMcpContracts`, `CadBatchProcessor`, `CadAiStagingSession`, `CadSemanticQueryEngine`, `CadAiAuditTrail` e expandir `CadTools.cs` no `DotNetCad.Mcp`:
   ```powershell
   dotnet test C:\Users\adria\source\repos\DotNetCadEngine\DotNetCadEngine.slnx
   ```
