# Edição interativa: conclusão por ponto e referências

Revisão em 28/10/2026. Foram preservadas as alterações paralelas do workspace.

## Fluxo implementado

As ferramentas da barra Editar não exigem uma confirmação final separada. Mover/Copiar concluem no destino, Rotacionar na direção final, Offset no lado escolhido. Com feição pré-selecionada, o Offset também aceita digitar a distância e clicar diretamente no lado, sem Enter; o preview acompanha o cursor durante a digitação. A seleção usa amarelo uniforme com contorno escuro, inclusive quando o objeto original já é amarelo. Nas demais entradas numéricas, Enter serve apenas para entregar o parâmetro; o mesmo evento já aplica a operação válida. Esc cancela antes desse ponto sem criar undo. Após a conclusão, undo desfaz toda a seleção em uma única ação.

Com seleção prévia, a ferramenta inicia diretamente na coleta de parâmetros. Sem seleção prévia, selecionam-se entidades e encerra-se a seleção com botão direito ou Enter. Este encerramento da seleção não é uma confirmação adicional da transformação.

Espelhar preserva a escolha explícita sobre o original: dois pontos definem o eixo e um menu oferece Manter original ou Substituir original. Escolher a opção conclui. Fechar o menu sem escolher cancela. Essa escolha é parâmetro da operação, não um diálogo genérico de confirmação.

Texto também passa a concluir no clique de inserção depois de coletar conteúdo e altura.

## Referências e preview

- Mover/Copiar: linha elástica, seta, ponto base/destino, deltas X/Y e distância.
- Rotacionar: pivô comum, raio de referência, raio corrente, arco com seta e ângulo. O preview gira durante Hover, sem modificar entidades reais. R permite informar referência numérica ou apontar uma direção a partir do pivô; o destino define o ângulo novo e aplica a diferença.
- Espelhar: eixo prolongado e preview refletido.
- Offset: distância digitada ou medida por dois pontos; preview no lado do cursor, guia normal/radial e rótulo de distância. A escolha de lado usa o cursor livre, sem snap/ortho que o arraste para o próprio contorno ou para o ponto usado para medir a distância.

Os rótulos de distância usam as unidades de exibição do documento. A sintaxe numérica interativa existente continua explicitamente em metros, e ângulos em graus. Snaps/ortho/polar existentes são reutilizados na captura aplicável. O ghost é claro e tracejado para diferenciá-lo da seleção e da geometria definitiva. Não há interpolação temporal artificial: a atualização acompanha a posição capturada do cursor.

## Comparação com CAD e limites

A referência foi o fluxo documentado de base/destino, pivô/ângulo, eixo/opção do original e distância/lado. Isso não é uma alegação de equivalência completa com AutoCAD. COPY e OFFSET continuam operações simples por invocação; não foram adicionados seus modos de repetição/múltiplas cópias. A opção R usa uma direção com origem no pivô, não o fluxo completo de referência por dois pontos arbitrários. Offset interativo continua restrito a linhas/círculos; espelhamento de glifos de texto continua rejeitado.

Fontes primárias consultadas:

- [MOVE — Autodesk](https://help.autodesk.com/cloudhelp/2024/ENU/AutoCAD-Core/files/GUID-47CE7325-84C0-4414-80A3-29DC98392709.htm)
- [Rotação por referência — Autodesk](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/How-to-rotate-an-object-based-on-an-existing-angle.html)
- [MIRROR — Autodesk](https://help.autodesk.com/cloudhelp/2016/ENU/AutoCAD-MAC-Core/files/GUID-595277C8-9B87-4CFB-A3AF-769537A22F3D.htm)
- [Modify / Offset — Autodesk](https://help.autodesk.com/cloudhelp/2023/ENU/AutoCAD-GettingStarted/files/GUID-29B2169B-01D9-429B-9DCD-5D3552E5E68E.htm)

## Validação

Testes Core: hover de rotação transitório, pivô compartilhado, referência, correção após ponto inválido, distância de offset por dois pontos, preview e undo/redo. Testes WPF: pipeline de pontos usado pelo mouse conclui as quatro operações sem Enter; escolha do original conclui espelhamento; submissão numérica de mover não exige Enter adicional; texto conclui no clique. Camadas bloqueadas e validações de lote permanecem cobertas pelas suítes anteriores.

Foram renderizados previews das quatro operações em janela WPF de teste isolada. Imagens em `.artifacts/edit-flow-tests/bin/DotNetCad.Demo.Tests/debug/edit-validation/`. A imagem de rotação foi inspecionada para pivô, raios e arco. Os fluxos foram exercitados por testes automatizados de estado, geometria e WPF; não se alega teste manual ponta a ponta de cada ferramenta.
