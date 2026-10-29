---
name: cad-conferir-entrega
description: Conferir geometria, cotas, camadas, unidades e resultados de desenhos criados ou alterados pelo MCP antes da entrega.
---

# Conferir entrega CAD

Leia o estado do desenho após cada lote importante; confirme IDs criados/alterados e não repita uma chamada mutante só porque a resposta demorou. Compare limites e contagem com o plano.

- Verifique segmentos de comprimento zero, duplicatas, contornos abertos, interseções inesperadas, raios e coordenadas inválidas. Use consultas/topologia se disponíveis; caso contrário use propriedades das entidades. Não confunda ausência de detecção com ausência de conflito.
- Confira cotas contra geometria, unidade real, arredondamento e escala de texto na impressão; números escritos em TEXT não substituem uma medição.
- Em CAD 2D, cruzamento em planta não prova interferência física nem ausência de conflito em altura. Registre elevação desconhecida como pendência.
- Confira referências de camadas, visibilidade, bloqueios, legenda e identificação dos elementos. Inspecione legibilidade no viewport quando houver acesso visual; declare quando a conferência foi apenas geométrica.
- Compare arquivos exportados com o documento: unidades, limites, quantidade/tipos suportados e textos. Não sobrescreva arquivo existente sem autorização aplicável.
- Entregue arquivo, resumo de alterações, hipóteses e matriz de verificações normativas. Separe atendido, não atendido e não verificado. Anote limitações de formato, dimensionamento e análise.
