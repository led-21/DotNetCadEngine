# Prompt para Gemini — instalador e splash screen do DotNetCadEngine

Copie o bloco abaixo para o Gemini com acesso ao repositório. Se ele estiver apenas no chat, forneça os arquivos solicitados antes de aceitar código que dependa da estrutura do projeto. O objetivo é entregar implementação e pacote verificáveis, não apenas imagens conceituais.

```text
Atue como engenheiro de distribuição Windows e designer de produto desktop. Crie um instalador profissional e uma splash screen profissional para o DotNetCadEngine, um CAD 2D Windows em C# / WPF. Trabalhe sobre o repositório existente e preserve alterações locais. Faça primeiro a inspeção do código e depois implemente, sem redesenhar o motor CAD.

CONTEXTO A CONFIRMAR NO REPOSITÓRIO
- Solução: DotNetCadEngine.slnx.
- Aplicativo: src/DotNetCad.Demo/DotNetCad.Demo.csproj, atualmente net10.0-windows, UseWPF=true, WinExe.
- Inicialização: App.xaml usa StartupUri=MainWindow.xaml; App.xaml.cs ainda é mínimo.
- MainWindow inicia o host MCP em Loaded e o descarta em Closed. Preserve uma única inicialização e o encerramento correto.
- Tema: src/DotNetCad.Demo/Themes/DarkCadTheme.xaml.
- Identidade atual: fundo #0F172A, superfícies #1E293B, contornos #334155, destaque #38BDF8, texto #F8FAFC / #94A3B8.
- Nome público: DotNetCadEngine. Não invente empresa, certificado, domínio, suporte ou copyright. Consulte LICENSE e metadados existentes; use configuração documentada para dados ausentes.
- O aplicativo tem formato nativo .cad, importação/exportação DXF, exportação SVG e conexão MCP local. Não anuncie recursos inexistentes.

1. ESCOLHA E CONSTRUÇÃO DO INSTALADOR
Inspecione automações e empacotamento existentes. Consulte documentação oficial atual para selecionar uma ferramenta mantida (compare de forma breve WiX, Inno Setup ou MSIX à luz dos requisitos reais), valide licenciamento e fixe uma versão da ferramenta. Entregue uma única estratégia coerente, com justificativa curta; não mantenha três instaladores.

Produza um script reproduzível de publish Release e empacotamento. Comece por Windows x64. Avalie self-contained versus dependência do .NET Desktop Runtime compatível: prefira uma experiência que não peça ao usuário instalar o SDK; documente tamanho e estratégia de atualização do runtime. Não habilite trimming em WPF sem comprovar suporte e testar. Não assuma que single-file resolve todos os arquivos de distribuição.

Use diretório de saída próprio (artifacts/installer ou convenção existente), sem empacotar arquivos pessoais, fontes, segredos, testes ou logs. Inclua dependências necessárias e ativos de licença. Tenha uma fonte consistente de versão para assembly, instalador e splash. Use identidade estável para atualizações; não gere um novo identificador de produto incompatível a cada build sem compreender as regras da ferramenta.

Ofereça instalação por usuário sem elevação se atender aos requisitos; se escolher instalação por máquina, justifique e solicite UAC apenas quando necessário. Não faça instalação silenciosa forçada. Inclua entrada em Aplicativos instalados, ícone, versão e desinstalador; atalho no menu Iniciar e atalho na área de trabalho opcional. Instale binários em local apropriado e grave preferências/logs/cache em diretório de dados do usuário, sem depender de escrita na pasta de instalação.

Suporte atualização preservando preferências e desenhos. Detecte aplicativo em execução e solicite fechamento normal; nunca mate o processo silenciosamente, pois pode haver desenho não salvo. Evite instalações paralelas incompatíveis e downgrade acidental. A desinstalação remove os arquivos do produto e atalhos, preservando desenhos e dados pessoais por padrão; não faça exclusão recursiva genérica de diretórios do usuário.

Associação de .cad deve ser opcional e respeitar associações existentes. Não tome .dxf como padrão sem opção explícita. Se oferecer abrir arquivos pelo Explorer, implemente e teste o processamento real de argumentos na aplicação, inclusive caminhos com espaços e arquivo inválido, sem perder desenho aberto. Não registre associação que a aplicação ainda não consegue atender.

Prepare integração opcional de assinatura de código via variáveis/segredos de CI. Não invente assinatura, certificado ou reputação SmartScreen. Se não houver certificado, declare artefato não assinado e documente como assinar. Não enfraqueça proteções do Windows. Preserve o comportamento e as restrições de acesso do MCP; não exponha uma porta de rede para viabilizar o instalador.

2. DESIGN DO INSTALADOR
Crie uma experiência sóbria de software técnico: tipografia legível, grid consistente, bom contraste, margens amplas e destaque ciano moderado. Evite neon exagerado, excesso de gradientes e aparência de mockup web.

Use o mecanismo visual suportado pela ferramenta escolhida, evitando uma interface customizada frágil apenas por estética. Forneça banner/ícone/logotipo originais e arquivos-fonte editáveis, com variantes e resoluções apropriadas. Prefira vetor para a marca; gere .ico com resoluções adequadas. Não copie marcas de outros CADs.

Textos em português do Brasil: instalação, destino/opções quando aplicáveis, progresso, conclusão e erros úteis. Diferencie progresso determinado e atividade sem percentual conhecido. Não invente etapas nem exiba percentuais falsos. Ao concluir, ofereça abrir o aplicativo; não o execute automaticamente com privilégios elevados herdados do instalador.

3. SPLASH SCREEN WPF
Implemente uma janela de splash leve em XAML vetorial, coerente com o tema, aproximadamente 640×360 DIPs e adaptável a DPI alto. Mostre marca, “CAD 2D”, versão real e status de inicialização. Composição sugerida: marca à esquerda e geometria técnica discreta à direita (linhas, arco, cota, grade), sem parecer que existe um desenho de usuário carregado. Não dependa de download de imagens ou acesso à internet.

Animação discreta e opcional, respeitando preferências de movimento quando disponíveis. Não use atrasos artificiais para prolongar a splash. Se a inicialização é rápida, a splash pode ser breve. Use indicador indeterminado quando não houver progresso mensurável; mostre etapas reais e não prometa recursos ainda não inicializados.

Refatore App.xaml/App.xaml.cs cuidadosamente: evite abrir duas MainWindow ao remover/substituir StartupUri; configure MainWindow e ShutdownMode para fechar a splash sem encerrar o aplicativo. Faça a transição quando a janela principal estiver pronta para exibição, sem roubar foco desnecessariamente. Não execute objetos WPF fora do dispatcher; trabalho pesado independente pode ser assíncrono, e tarefas devem ser observadas/canceladas corretamente. Não deixe thread ou tarefa órfã.

Se ocorrer falha, feche a splash, mostre erro acionável e caminho de log local com detalhes técnicos, sem loop ou janela invisível. Trate encerramento durante a inicialização. Preserve carregamento de recursos, prompts de documento e ciclo de vida MCP. Não faça trabalho pesado no construtor da splash nem bloqueie seu primeiro frame.

4. ENTREGÁVEIS
- Arquivos de instalação, publish e build reproduzível, com ferramentas e versões documentadas.
- Splash integrada ao startup real, ícones e ativos com fontes editáveis.
- Metadados/versionamento consistentes.
- Pipeline de release manual ou por tag, se compatível com a CI existente; sem publicar release ou enviar artefatos a terceiros automaticamente nesta tarefa.
- Documento de build local, assinatura opcional, distribuição, atualização e limitações.
- Pacote instalável e checksum, se houver ambiente capaz de gerá-los. Se faltar ferramenta, certificado ou Windows adequado, diga exatamente o que foi e não foi produzido; não simule sucesso.
- Capturas reais do instalador e da splash quando puder executar o aplicativo. Não apresente mockup como captura de execução.

5. CRITÉRIOS DE ACEITAÇÃO
Teste em ambiente Windows de teste: instalação limpa sem SDK, iniciar pelo menu, atualização de uma versão anterior de teste, cancelamento, app já aberto, desinstalação preservando desenhos e reinstalação. Valide caminhos com espaços, usuário sem admin quando suportado, DPI de 100/150/200%, ausência de internet na estratégia offline escolhida e falha de startup. Teste associação de arquivos apenas se implementada.

Verifique que não há duas instâncias de MainWindow/host MCP criadas pela splash, que ela não fica presa e que o aplicativo não termina ao fechá-la. Confirme que abrir arquivos e sair continuam protegendo alterações não salvas. Não execute instalação/desinstalação destrutiva na máquina de trabalho do usuário: use VM/ambiente de teste apropriado.

Ao finalizar, descreva arquivos alterados, comandos para reproduzir o pacote, cenários efetivamente testados e limitações. Separe implementação concluída de validações pendentes. Entregue código funcional e design integrado, não apenas recomendações.
```
