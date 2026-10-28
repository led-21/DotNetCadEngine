# CAD usável: camadas, console e próximos incrementos

Revisão complementar de 28/10/2026. Baseada no código local atual, sem validação visual nesta rodada. Há alterações locais novas em interação CAD, offset e proteção de documentos; os achados antigos não devem ser reaplicados sem verificar essas alterações. Esta rodada entrega revisão e prompts, sem modificar o aplicativo.

## Prioridades verificadas

| Prioridade | Evidência atual | Correção proposta |
| --- | --- | --- |
| P1 | CadCommandProcessor oferece criar/ativar, mostrar/ocultar e cor; falta remover/renomear camada | Gerenciador de camadas com operações reversíveis e regras para camadas ocupadas |
| P1 | LAYER name insere diretamente em Document.Layers, fora do UndoManager | Criação, remoção e renomeação em ações de histórico |
| P1 | Checkbox altera IsVisible diretamente; comando LAYER HIDE usa SetLayerVisibilityAction e impede ocultar a última camada visível | Unificar regras e histórico entre painel, console e MCP |
| P1 | IsLocked existe, mas não encontrei sua aplicação nas rotas de edição pesquisadas | Bloqueio real no serviço de operações, inclusive grips, Delete e MCP |
| P1 | Console tem Height=142; não há splitter ou recolhimento | Console acoplado, recolhível e redimensionável |
| P1 | MainWindow faz ScrollIntoView a cada alteração do histórico | Só acompanhar novas mensagens quando usuário está no final |
| P2 | ConsoleHistory é coleção de strings limitada a 200 linhas; não há histórico navegável da entrada | Separar histórico de comandos e mensagens estruturadas, com limites claros |
| P2 | NewLayerInput vira LAYER seguido do nome; parser separa por espaços | Suportar nomes com espaços via argumentos estruturados ou tokenizer com aspas |
| P2 | SetLayerColorAction infere ByLayer comparando cor da entidade à cor anterior da camada | Modelar explicitamente herança de estilo; cores iguais não provam herança |
| P2 | Não há editor gráfico de propriedades no painel atual | Editar camada, cor, dimensões e propriedades de entidades selecionadas |
| P2 | Seleção por ponto aceita interior de círculo/retângulo/polígono fechado | Avaliar preferência por contorno e alternância de entidades sobrepostas para evitar seleção surpresa |
| P2 | Título da janela é fixo; salvamento e estado do documento pouco visíveis | Exibir nome do arquivo, indicador de alteração e Salvar como |

IsPlottable e LineThickness também existem nas camadas. Antes de expor controles, verificar se renderização e exportação respeitam seus valores. Não apresentar opções sem efeito.

## Regras propostas para camadas

- Camada vazia: excluir com uma ação de undo.
- Camada ocupada: informar quantidade de entidades e oferecer **Mover entidades para outra camada e excluir**, **Excluir camada e entidades** e **Cancelar**. Não escolher a opção destrutiva por padrão.
- Camada 0 e IsProtected: impedir exclusão e renomeação, explicando o motivo. Proteção contra remover a camada não significa bloquear toda edição de suas entidades.
- Camada ativa: pedir/definir uma sucessora válida antes de remover; restauração por undo deve recuperar a camada ativa anterior.
- Manter pelo menos uma camada visível e desbloqueada apta a receber desenho. Se faltar uma, orientar o usuário; nunca desenhar silenciosamente em camada invisível/bloqueada.
- Renomear atualiza entidades e camada ativa de forma atômica; rejeitar nomes vazios e duplicados sem distinguir maiúsculas/minúsculas.
- Exibir nome, cor, visibilidade, bloqueio e quantidade de entidades; usar menu de contexto para ações secundárias e evitar uma fileira excessiva de botões.
- Separar camada selecionada no gerenciador de camada ativa para desenho.

## Prompt para o Sol — gerenciador de camadas

```text
Implemente o ciclo completo de camadas no DotNetCadEngine. Primeiro leia o código atual e o git diff para preservar o trabalho existente, inclusive mudanças ainda não commitadas. Examine CadDocument, CadLayer, CadLayerActions, CadCommandProcessor, LayerItemViewModel, MainViewModel, CadViewport e serialização/exportação.

Crie uma camada de operações comum ao painel, console e MCP. Implemente criar, renomear, excluir, ativar, mostrar/ocultar, bloquear/desbloquear e transferir entidades selecionadas para outra camada. Use UndoRedoManager: uma operação lógica = um undo, inclusive exclusão com transferência de entidades. Não trate cor igual à da camada como evidência suficiente de ByLayer; não amplie o sistema de estilos sem delimitar esse trabalho.

Proteja camada 0 e IsProtected de exclusão/renomeação. Para camada ocupada, mostre contagem e escolha explícita entre transferir entidades para destino válido, excluir também as entidades ou cancelar. Antes de remover camada ativa, defina sucessora visível e desbloqueada. Preserve ao menos uma camada utilizável. Não deixe referências a nomes removidos. Undo/redo restaura entidades, ordem, propriedades e camada ativa.

Bloqueio deve impedir mutações via mouse, grips, atalhos, console e MCP; ainda pode permitir visualizar, inspecionar e usar snap, desde que a regra fique consistente. Valide operações em lotes antes de alterar qualquer entidade. Não basta desabilitar um botão. Undo/redo deve restaurar estados válidos mesmo após mudanças de bloqueio.

No painel, diferencie seleção da linha e camada ativa, ofereça menu de contexto, edição de nome/cor, contagem e filtros simples. Suporte nomes com espaços na UI e console; rejeite vazios/duplicados case-insensitive. Corrija a divergência entre checkbox e LAYER HIDE ao ocultar a última camada visível. Atualize HELP e a exposição MCP pertinente sem quebrar comandos existentes.

Teste camada vazia/ocupada/ativa/protegida, cancelamento, destino inválido, nomes com espaços, duplicados, bloqueio em todos os canais, undo/redo e salvar/reabrir. Preserve o desenho em toda falha. Execute os testes relevantes e registre o que foi validado visualmente.
```

## Console proposto

O console deve ser uma ferramenta de trabalho, não um painel de log permanentemente grande. Manter uma linha de comando compacta visível e permitir abrir o histórico acima dela. Reservar “Limpar histórico” para mensagens; CLEAR continua sendo uma operação no desenho e nunca deve ser chamado pelo botão de limpeza do console.

- Arrastar divisor para ajustar altura, recolher/expandir, restaurar tamanho e lembrar preferência.
- Histórico de entrada com ↑/↓ e preservação do rascunho; Enter executa; Esc fecha sugestões/cancela entrada conforme foco, sem apagar desenho.
- Autocomplete de comandos, ajuda de sintaxe e exemplos conforme o comando.
- Mensagens com horário, origem (usuário/MCP/sistema), severidade e resultado, sem depender apenas de cor.
- Selecionar/copiar texto, copiar comando, pesquisar, filtrar erros e exportar log explicitamente.
- Autoscroll somente no final; indicador de mensagens novas quando o usuário navega para cima.
- Erro preserva a entrada para correção; executar vazio não faz nada; foco permanece na entrada após executar.
- Distinguir prompt de operação interativa e log de comandos; não executar dois comandos mutantes simultâneos nem confundir destino da entrada.
- Não chamar de chat de IA se o painel apenas executa comandos CAD e mostra ações MCP; conexão e capacidade devem ser descritas honestamente.

## Prompt para o Sol — console profissional

```text
Refatore o console WPF do DotNetCadEngine em um componente próprio, preservando a API RunCadCommand e a integração MCP. Examine o estado atual e não sobrescreva melhorias em andamento. Aplique a proposta de console em docs/cad-usabilidade-camadas-console.md.

Implemente histórico recolhível e redimensionável com GridSplitter e uma linha de entrada compacta sempre acessível. Preserve área mínima do viewport; limite altura do painel ao redimensionar a janela. Guarde preferências de layout por usuário, sem colocá-las no documento CAD. Evite janela flutuante nesta primeira etapa.

Separe histórico de comandos digitados e mensagens de saída. Use mensagens estruturadas com horário, origem e severidade; preserve texto legível, seleção/cópia, filtro, pesquisa e exportação explícita. Limpar histórico não pode executar CLEAR nem afetar desenho/undo. Limite memória, use virtualização e não persista automaticamente conteúdo potencialmente sensível dos comandos.

Implemente ↑/↓ preservando rascunho, autocomplete de comandos existentes, ajuda contextual de argumentos, Enter para executar e foco correto. Preserve texto após erro. Não roube Ctrl+C/Ctrl+Z/Delete da edição de texto para o canvas. Diferencie o comando interativo ativo da execução de um novo comando e impeça mutações concorrentes incoerentes.

Faça autoscroll apenas se o usuário estava no final; caso contrário mantenha posição e mostre quantidade de novas mensagens. Trate atualizações MCP no dispatcher, sem travar UI. Crie um mecanismo único de catálogo de comandos para ajuda/autocomplete, evitando listas divergentes.

Valide muitas mensagens, linhas longas, recolher/expandir, redimensionar, copiar, erro de sintaxe, histórico com rascunho, foco, chegada de mensagens enquanto lê o passado e janela pequena com DPI alto. Teste a lógica relevante; registre separadamente validação visual e testes automatizados. Não adicione dependências grandes sem necessidade demonstrada.
```

## Próximos incrementos para uso diário

1. **Propriedades e precisão:** painel para coordenadas, comprimento, raio, texto, camada e cor; entrada numérica para criação de geometria, além das modificações. Unidades configuráveis e indicação inequívoca em medidas/exportação.
2. **Seleção:** contorno consistente, alternância entre sobrepostos, selecionar tudo/limpar seleção e filtros por tipo/camada; tolerância visual em pixels convertida para mundo.
3. **Documentos:** Salvar como, recentes, nome/estado no título, recuperação após falha e autosave separado do arquivo original. Não tratar autosave como autorização para sobrescrever o documento.
4. **Edição:** acabamento de texto/cotas; acesso gráfico a aparar, estender e quebrar já disponíveis no console; depois avaliar escala, aparar múltiplos e propriedades em lote.
5. **Saída:** impressão/PDF, escalas, área de impressão e aviso claro sobre subconjunto DXF suportado; testes de interoperabilidade com arquivos reais.
6. **Desempenho:** medir latência de seleção, redraw e carregamento com desenhos representativos antes de escolher indexação espacial ou renderização incremental.

Esses incrementos são propostas; não se afirma que foram implementados ou testados nesta rodada. Concluir camadas e console antes de ampliar indiscriminadamente o conjunto de ferramentas.
