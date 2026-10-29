# Skills de engenharia para o MCP

As nove skills ficam em `.agents/skills/<nome>/SKILL.md`, para descoberta local no projeto. O build e publish do DotNetCad.Mcp copiam os arquivos para `EngineeringSkills` ao lado do servidor. Isso permite que clientes sem acesso ao repositório também leiam as orientações.

## Uso pelo agente

1. Chame `list_engineering_skills`.
2. Leia `cad-preparar-projeto` com `read_engineering_skill`, passando o argumento `name`.
3. Leia `nbr-consulta-e-evidencias` e as skills de disciplina aplicáveis.
4. Inspecione o desenho e confirme unidades e entradas; execute o trabalho autorizado.
5. Leia e aplique `cad-conferir-entrega` e registre pendências.

O handshake de inicialização do servidor inclui essa orientação. Ela é uma instrução ao agente: o servidor não bloqueia chamadas de desenho de quem não leu as skills, nem comprova que o conteúdo foi aplicado. Uma barreira obrigatória exigiria outra implementação, com sessões, seleção de escopo e validações; um simples registro de leitura não provaria conformidade.

## Conteúdo

| Skill | Uso / referências para consulta |
| --- | --- |
| cad-preparar-projeto | Escopo, unidades, camadas e planejamento das operações |
| cad-conferir-entrega | Geometria, cotas, exportação e limitações |
| nbr-consulta-e-evidencias | Edição, aplicabilidade, fontes e matriz de verificação |
| nbr-representacao-arquitetonica | Representação/documentação; NBR 6492 |
| nbr-acessibilidade | Acessibilidade; NBR 9050 e NBR 16537 quando aplicável |
| nbr-eletrica-baixa-tensao | Instalações de baixa tensão; NBR 5410 |
| nbr-agua-predial | Água fria/quente; NBR 5626 |
| nbr-esgoto-predial | Esgoto sanitário; NBR 8160 |
| nbr-estruturas-concreto | Detalhamento a partir de projeto calculado; NBR 6118 |

São roteiros originais, sem reprodução de normas, tabelas ou dimensões normativas. Referências identificam assuntos; edição vigente e requisitos específicos não foram certificados neste pacote. A consulta de requisitos depende de exemplar autorizado e das condições do projeto. Fontes oficiais de identificação/acesso: [ABNT Catálogo](https://www.abntcatalogo.com.br/) e [ABNT Coleção](https://www.abntcolecao.com.br/). A [orientação do catálogo](https://abntcatalogo.com.br/faq.aspx) descreve busca e aquisição; não substitui o conteúdo técnico.

## Atualizar e ativar

Depois de editar skills ou ferramentas, recompile/publique o servidor e reinicie a conexão MCP no cliente para descobrir o novo catálogo. Confira o caminho do executável configurado no cliente: publicar outra pasta não troca automaticamente a conexão atual. Distribua `EngineeringSkills` junto com os binários, conforme o publish já faz.

Validação local da integração, sem abrir ou modificar desenho:

```powershell
dotnet publish src/DotNetCad.Mcp/DotNetCad.Mcp.csproj -o .artifacts/engineering-skills-review
python -X utf8 tests/mcp_engineering_skills_smoke.py .artifacts/engineering-skills-review/DotNetCad.Mcp.dll
```

O smoke test verifica instruções de inicialização, descoberta das ferramentas, leitura das nove skills publicadas e rejeição de nomes/caminhos inválidos. Isso não valida um projeto de engenharia nem substitui ensaios por disciplina.
