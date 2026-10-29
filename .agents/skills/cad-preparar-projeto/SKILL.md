---
name: cad-preparar-projeto
description: Preparar desenhos de engenharia pelo DotNetCad MCP, definindo escopo, unidades, referências e skills de disciplina antes de criar geometria.
---

# Preparar projeto CAD

Antes da primeira mutação, consulte `list_engineering_skills`, leia esta skill e as skills da disciplina com `read_engineering_skill`. O servidor disponibiliza orientações; é o agente que deve aplicá-las.

1. Consulte capacidades e descreva o desenho aberto. Descubra ferramentas realmente disponíveis no cliente; não suponha que todos os builds tenham batch, preview ou análise topológica.
2. Identifique disciplina, etapa (conceito/anteprojeto/executivo), local/uso da edificação, escopo autorizado e entregáveis. Pergunte apenas pelos dados ausentes que mudem a geometria ou o dimensionamento. Não invente medidas de fotografias sem escala.
3. Defina unidade de coordenadas, origem, orientação, referência altimétrica quando pertinente, escala de apresentação, precisão e tolerância. Confirme a semântica de unidades da API: não confunda coordenadas de desenho com campos nomeados em metros. Faça conversão explícita e não mude unidades de documento existente silenciosamente.
4. Separe geometria existente, proposta e anotações em camadas por disciplina/função. Exemplos são convenções locais, não nomes impostos por NBR: ARQ-PAREDES, ARQ-PORTAS, ELE-CIRCUITOS, HID-AGUA, ESG-TUBOS, COTAS, TEXTOS. Preserve bloqueios e não limpe o desenho para começar sem autorização.
5. Leia `nbr-consulta-e-evidencias` e selecione as skills pertinentes: representação, acessibilidade, elétrica, água, esgoto ou estruturas. Registre entradas, referências e hipóteses num memorial do projeto fora do SKILL.md.
6. Planeje uma pequena sequência reversível de comandos. Se preview/batch estiver disponível, prepare uma proposta; confirme transação e IDs antes de qualquer commit. Caso contrário aplique incrementos curtos com conferência, sem prometer rollback atômico.
7. Execute `cad-conferir-entrega` ao terminar. Não declare desenho executivo quando os dados forem apenas conceituais.
