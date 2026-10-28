# Revisão CAD e prompts para o Sol

Revisão de 28/10/2026, baseada nas capturas fornecidas e no código do Demo, editor e ações de histórico. Não é uma auditoria exaustiva de todos os formatos e algoritmos.

## Ajustes aplicados nesta revisão

- Barra superior com quebra de linha e marca em linha própria; grupos da barra de status também quebram linha.
- Janela com mínimo de 640 × 480 unidades WPF e arredondamento de layout.
- Barra de ferramentas com espaço suficiente para ícones e rolagem de 10 unidades, em tema escuro. O tema também atende rolagem horizontal do console.
- Camadas recolhíveis, lista com rolagem e nomes longos com reticências e tooltip.
- Excluir e modificar ficam desabilitados sem seleção; apagar tudo fica desabilitado com desenho vazio. Reavaliação dos comandos quando seleção ou revisões mudam.
- Rótulos distinguem Excluir seleção, Apagar tudo… e Vista inicial. A confirmação existente de apagar tudo foi mantida.

## Diagnóstico e prioridades

1. **Alta — modificações ainda usam parâmetros fixos.** Em `src/DotNetCad.Demo/ViewModels/MainViewModel.cs`, Move usa (1, 0), Copy usa (1, 1), Rotate usa 90° e Offset usa 0,5. Rotate e Mirror calculam um centro diferente para cada entidade: isso não corresponde a transformar uma seleção como conjunto. Não alterado nesta entrega; requer interação CAD consistente.
2. **Alta — substituição e fechamento do documento.** ImportDxf substitui Document diretamente e LoadSampleDrawing troca o documento sem a proteção usada por Novo/Abrir. MainWindow não possui tratamento de Closing para alterações não salvas. A importação também precisa tratar falhas antes de trocar o documento. São caminhos de possível perda de trabalho.
3. **Alta — semântica de offset.** Em `src/DotNetCad.Core/History/Actions/CadEntityActions.cs`, OffsetEntityAction usa OffsetPolygon inclusive para polilinha aberta; tipos não tratados viram cópias transladadas; círculo é limitado silenciosamente a raio 0,05. É necessário validar a geometria e informar operações não suportadas, sem simular sucesso.
4. **Média — entrada de texto.** AddTextPrompt não abre um prompt: insere “Anotação”, altura 1,0 e última coordenada do mouse arredondada. Usuário precisa escolher texto, altura e ponto de inserção.
5. **Média — hierarquia de comandos.** A quebra de linha resolve o corte, mas muitas ações ainda ocupam a barra principal. Recomenda-se Arquivo para formatos e exemplos, Exibir para vista inicial e Editar para apagar tudo. Enquadrar e Excluir seleção continuam de acesso rápido. Novo cria outro documento; Apagar tudo remove entidades do documento atual com undo; Vista inicial só altera câmera.
6. **Média — área útil.** Console deve poder recolher/redimensionar; ferramenta ativa merece indicação persistente; prompts devem informar etapa atual e como cancelar. Padronizar português e unidades sem traduzir a sintaxe pública dos comandos.

## Prompt 1 — parâmetros e interação CAD (executar primeiro)

```text
No repositório DotNetCadEngine, implemente modificações CAD com parâmetros fornecidos no momento do uso. Preserve as melhorias atuais de layout e examine MainViewModel.cs, CadViewport.cs, CadToolType.cs, CadCommandProcessor.cs e as ações de histórico antes de editar.

Remova os valores fixos dos botões Mover, Copiar, Rotacionar, Espelhar e Offset. Crie um fluxo de operação com seleção, coleta de parâmetros, preview e confirmação, reaproveitando a infraestrutura existente. Mover/Copiar: ponto base e destino, com alternativa numérica dx/dy ou distância + direção. Rotacionar: pivô comum à seleção e ângulo informado. Espelhar: dois pontos do eixo comum e opção explícita de manter o original. Offset: distância e lado escolhidos pelo usuário.

Mostre instruções da etapa atual no viewport ou console. Enter confirma quando válido; Esc cancela sem alterar documento ou histórico. Capture entrada numérica sem disparar atalhos do canvas enquanto o usuário digita. Aceite vírgula decimal na interface pt-BR, valores finitos e unidades coerentes; preserve a sintaxe existente do console/MCP. Use snaps, ortho e polar onde forem aplicáveis.

Cada confirmação deve gerar uma única ação de undo para toda a seleção; preview não deve modificar entidades reais. Valide todos os parâmetros antes de aplicar, sem alterações parciais. Mantenha seleção e comandos coerentes após undo/redo. Não implemente simultaneamente uma reformulação geral de importação/exportação.

Critérios: mover por 2,35 m; copiar por vetor negativo; girar duas entidades 37° em torno de um pivô compartilhado; espelhar em eixo inclinado; cancelar em cada etapa sem efeito; rejeitar NaN/infinito e entradas inválidas; undo/redo restaura o conjunto. Adicione testes relevantes de estado e geometria e valide teclado e mouse. Relate limitações de tipos geométricos em vez de fingir suporte.
```

## Prompt 2 — integridade do documento e offset

```text
Revise e corrija a integridade das operações no DotNetCadEngine, preservando as alterações existentes. Antes de editar, confirme os achados em MainViewModel.cs, MainWindow.xaml.cs, CadEntityActions.cs, CadConstructionMath e UndoRedoManager.

Unifique a proteção de alterações não salvas em Novo, Abrir, Importar DXF, Carregar exemplo e fechar janela. Ofereça Salvar/Descartar/Cancelar; se o usuário cancelar o diálogo de salvar ou o salvamento falhar, a substituição/saída deve ser cancelada. Carregue e valide um arquivo temporariamente antes de substituir o documento atual. Defina se Importar substitui ou incorpora; o rótulo e o fluxo devem explicitar a decisão. Preserve o caminho nativo e histórico se houver falha. Trate erros de leitura/escrita com mensagens úteis. Revise dirty tracking e o estado salvo ao desfazer/refazer.

Corrija OffsetEntityAction: não usar translação genérica como fallback de offset; distinguir polilinhas abertas e fechadas; não limitar silenciosamente raio inválido a 0,05. Declare tipos suportados, rejeite casos impossíveis antes de mutações e preserve a atomicidade e undo/redo. Examine orientação, segmentos degenerados, cantos e auto-interseções e delimite suporte honestamente.

Teste cancelamento e falha de salvar/importar, substituição com alterações pendentes, fechamento cancelado, offset positivo/negativo, raio que colapsa, polilinha aberta, tipo não suportado e histórico após falha. Evite refatorações sem relação com esses problemas.
```

## Prompt 3 — acabamento da experiência e entrada de texto

```text
Melhore a experiência WPF do DotNetCadEngine sem reescrever o motor CAD. Preserve barras com quebra de linha, rolagem escura e camadas recolhíveis já implementadas. Organize comandos por Arquivo, Editar e Exibir; deixe ferramentas frequentes acessíveis. Enquadrar ajusta entidades visíveis, Vista inicial restaura câmera, Excluir seleção remove selecionadas, Apagar tudo remove entidades com confirmação e undo, Novo inicia outro documento com proteção de alterações não salvas. Dê a cada ação habilitação coerente e tooltip claro.

Permita recolher e redimensionar console e inspector sem esconder o acesso para reabri-los. Indique persistentemente ferramenta ativa, modo de captura e etapa do comando. Padronize os textos de interface em português; preserve nomes públicos de comandos e unidades do documento. Transforme AddTextPrompt em entrada real de conteúdo, altura e ponto de inserção, com preview e cancelamento.

Valide redimensionamento em 640×480, 800×600, 1100×720 e janela maximizada, além de escalas de tela de 100%, 150% e 200%. Verifique barras horizontal/vertical com mouse e teclado, lista de muitas camadas, nomes longos, foco visível, tabulação e ausência de sobreposição. Não basta compilar: abra a interface e registre os tamanhos e fluxos realmente verificados. Preserve sessões e desenhos existentes ao testar.
```

Use os prompts em etapas, revisando o resultado de cada uma antes da próxima. Os itens maiores acima permanecem pendentes; esta entrega alterou apenas a interface e a habilitação de comandos.
