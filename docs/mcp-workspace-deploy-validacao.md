# MCP, workspace do servidor e consolidação de branches

Validação em 28/10/2026.

## Consolidação Git

Branch canônica do DotNetCadEngine: `InovaMutua`. `MutuaConecta` era ancestral, não tinha checkout independente e não continha commits exclusivos. Todo código local foi preservado no commit `345f911`, incluindo ferramentas/skills MCP e testes. InovaMutua foi publicada antes de remover MutuaConecta local e remotamente. A remoção remota usou lease com o SHA esperado para não descartar alterações concorrentes.

O repositório separado `C:/Projects/MutuaConecta` e a plataforma em produção não foram removidos. Alterações paralelas das páginas web foram preservadas.

## .gitignore

DotNetCadEngine: caches/builds `.artifacts/`, artefatos de validação, ambientes Python, arquivos transitórios/logs e `.env` locais; templates `.env.example`/`.env.*.example` permanecem permitidos. As skills em `.agents/skills`, fontes MCP, assets e releases intencionais não foram excluídos do versionamento.

Plataforma web: regras adicionais para bundles de deploy `/*-build.tar`, caches, testes e logs. `git ls-files -ci --exclude-standard` não encontrou arquivos rastreados que correspondessem às regras de ignore nos dois repositórios.

## Integração e correções

- O workspace tinha uma sincronização fictícia: dados fixos e reutilização do SVG anterior. O botão agora busca revisões reais; novas revisões vêm de publicação explícita no desktop/MCP.
- A página busca atualizações a cada 15 segundos quando visível, ao recuperar foco e ao voltar à aba. Requisições usam no-store e respostas atrasadas não sobrescrevem as mais recentes. Falhas de refresh preservam o conteúdo carregado.
- O preview escolhe a revisão recente e não inventa contagem de entidades ou camadas na ausência de metadados.
- O catálogo público é gerado pela descoberta MCP `tools/list`, não por uma lista manual de ferramentas.
- MCP ganhou `get_capabilities` e `mutua_sync_cad_document`. A publicação exige projeto compatível, sessão desktop autenticada e `confirmPublish=true`; não foi publicado desenho automaticamente.
- A leitura do projeto vinculado consulta o servidor real, em vez de retornar disciplinas fixas de demonstração. O requisito informado para busca de especialistas é respeitado.
- Snapshots de sincronização incluem propriedades reais de camadas, unidades, capacidades e SVG. O hash usa a representação nativa sem seleção transitória. Desenhos vazios produzem metadados válidos.
- Skills de engenharia incorporadas: 9, expostas por `list_engineering_skills` e `read_engineering_skill`. São orientações de fluxo, não texto normativo ou certificação.

## Deploy seguro

Uma publicação concorrente do frontend foi detectada e a implantação original foi interrompida antes de sobrescrevê-la. A versão concorrente já incluía as correções de refresh e catálogo, com 48 ferramentas. Foi então construída uma imagem derivada exclusivamente para atualizar o arquivo do catálogo para 50 ferramentas, preservando todos os arquivos do frontend recém-publicado.

Deployment: `mutua-conecta/mutua-web`.
Imagem validada: `localhost:32000/mutua-conecta-web:catalog-50-b6e3d104d5f8-fbd269dd1e4c`.
Digest do catálogo atualizado: `sha256:fd14a3e4e1de9bc8cbb209e54b235f6e456355ce682f97dc50ff3dae8d79b9b2`.
Imagem-base preservada: `sha256:fbd269dd1e4c263b8b1ee012f43da911c20a22740bbe8231ab5533a11cfd66b8`.

Patch com teste de resourceVersion e verificação prévia da imagem; rollout concluído com uma réplica pronta. API, banco, portas e documentos não foram alterados.

## Evidências

- TypeScript: `tsc --noEmit` passou.
- Build Next.js no servidor: compilação, tipos e geração de páginas concluídos.
- 203 testes Core e 65 testes WPF passaram na validação desta integração.
- MCP stdio: initialize, tools/list e get_capabilities passaram; 50 ferramentas descobertas. Smoke test leu as 9 skills e rejeitou nomes/caminhos inválidos, sem mutar o CAD.
- `https://mutua.grifocorretora.com/api/health/ready`: Healthy / Connected.
- `https://mutua.grifocorretora.com/cad-mcp-capabilities.json`: 50 ferramentas, incluindo skills e sincronização.
- Projeto `40000000-0000-0000-0000-000000000001`: PRJ-2026-001; um documento, versão 1, preservado.
- JavaScript público do workspace contém setInterval de 15 segundos e listeners de foco/visibilidade, sem a simulação antiga.

Não houve teste de publicação autenticada de um novo desenho em produção: isso criaria uma revisão real. As chamadas antigas do conector local a describe/HELP retornaram erro durante o diagnóstico; o servidor atualizado foi verificado separadamente por stdio. Clientes de IA precisam reconectar ao build MCP atualizado para descobrir os novos schemas.

O build web existente apresentou avisos de dependências do Next.js no npm. A versão não foi alterada neste escopo; atualização de dependências exige sua própria validação.
