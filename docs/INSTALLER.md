# DotNetCadEngine — Estratégia de Empacotamento, Instalador e Splash Screen

Este documento descreve a arquitetura de empacotamento, a escolha técnica do instalador, a splash screen vetorial e os procedimentos para reprodução local e em CI do **DotNetCadEngine** (software CAD 2D técnico para Windows).

---

## 1. Escolha e Justificativa da Ferramenta de Instalação

Foram avaliadas as três principais opções de distribuição para software desktop Windows moderno:

| Critério | Inno Setup 6 (Escolhido) | WiX Toolset v4/v5 | MSIX |
|---|---|---|---|
| **Licenciamento** | Licença permissiva (Jordan Russell), open source, sem custos. | Open source (.NET Foundation). | Proprietário Microsoft. |
| **Instalação Não Elevada (Per-User)** | Nativa (`PrivilegesRequired=lowest`), instala em `%LOCALAPPDATA%\Programs`. | Suportada via MSI, porém propensa a complexidades de UAC e rollback. | Nativa por contêiner. |
| **Detecção de App em Execução** | Suporte nativo elegante (`AppMutex` e `CloseApplications=yes`) solicitando fechamento sem matar processo. | Requer custom actions em C++ ou WiX CloseApplication. | Encerramento via lifecycle do pacote. |
| **Assinatura de Código** | Opcional; instalador funciona imediatamente em dev/test sem forçar certificado. | Opcional em MSI. | **Obrigatória**; sem certificado confiável o Windows bloqueia instalação em ambientes normais. |
| **Complexidade e Manutenção** | Script único declarativo (`.iss`), compilador CLI (`ISCC.exe`) de ~5 MB, reprodutibilidade imediata. | Requer múltiplos arquivos XML/WXS, compilação via CLI `wix`, extensões externas. | Requer manifesto Appx, empacotamento MSIX, configuração de sandbox de rede/pipes. |

### Decisão Técnica: Inno Setup 6 (Versão Fixada: 6.3+ / 6.7.x)
- **Motivação:** Excelente suporte nativo para instalação por usuário sem privilégios administrativos (`PrivilegesRequired=lowest` com opção via diálogo para máquina inteira se desejado), diálogo nativo em português do Brasil, manipulação segura de atualização e desinstalação, registro limpo de atalhos e associação de formato de arquivo `.cad`.

---

## 2. Estratégia de Runtime: Self-Contained x64

- **Modelo Escolhido:** `self-contained` para o target `win-x64`.
- **Justificativa:** O usuário final de engenharia ou arquitetura não deve ser forçado a instalar manualmente o .NET 10 Desktop Runtime ou SDK para executar o software. A aplicação traz suas dependências do runtime .NET compiladas para x64.
- **Trimming:** Desabilitado por segurança para WPF (`PublishTrimmed=false`), garantindo que nenhum tipo dinâmico XAML ou reflection do motor geométrico seja removido acidentalmente.
- **Tamanhos Reais Medidos:**
  - Diretório publicado descompactado: **~140.0 MB**
  - Instalador compactado com LZMA2 Ultra: **43.62 MB**

---

## 3. Identidade, Segurança e Ciclo de Vida

- **Nome Público:** `DotNetCadEngine`
- **Identificador Estável (AppId):** `{{D5C8A714-3F21-4E89-B09A-4E38F9B6D211}}`
- **Mutex de Execução:** `DotNetCadEngine_Running_Mutex`
  - A aplicação adquire este mutex em `App.OnStartup` e o libera em `App.OnExit`.
  - Ao executar o instalador ou atualizador, o instalador detecta a aplicação aberta e exibe uma caixa de diálogo solicitando que o usuário salve seus desenhos e feche o programa normalmente. O instalador **nunca mata o processo silenciosamente**.
- **Associação de Arquivos:**
  - O formato nativo `.cad` é associado como `DotNetCad.Drawing` (opcional durante o assistente).
  - O formato `.dxf` **não é sequestrado** como padrão do sistema.
  - A aplicação suporta receber caminhos de arquivos com espaços e arquivos corrompidos via linha de comando ou duplo clique no Windows Explorer: caso o arquivo seja inválido ou não exista, o desenho atual é protegido e mantido sem corrupção.
- **Desinstalação:**
  - Entrada registrada no Painel de Controle e Configurações (*Aplicativos Instalados*).
  - Remove exclusivamente os binários instalados e atalhos criados.
  - **Preserva intactos os desenhos do usuário e logs** em `%LOCALAPPDATA%\DotNetCadEngine`.

---

## 4. Splash Screen Vetorial WPF

- **Janela:** `src/DotNetCad.Demo/Views/SplashScreenView.xaml` (~640×360 DIPs).
- **Design:** Tema técnico escuro sóbrio (`#0F172A`, superfícies `#1E293B`, contornos `#334155`, destaque `#38BDF8`, texto `#F8FAFC`).
  - **Lado Esquerdo:** Identidade da marca, badges "CAD 2D" e "MOTOR VETORIAL", versão dinâmica lida do assembly, mensagem de status de inicialização e barra de progresso técnica.
  - **Lado Direito:** Composição geométrica discreta em vetor XAML (grade de pontos, eixos de coordenadas $X/Y$, arco de construção, polígono mecânico de desenho técnico, cota linear com setas e snaps de precisão).
- **Orquestração de Inicialização (`App.xaml.cs`):**
  - `ShutdownMode = ShutdownMode.OnExplicitShutdown` inicial para permitir fechar a splash sem finalizar a aplicação.
  - Inicialização de subsistemas assíncrona com transição suave.
  - Instanciação de `MainWindow` e transferência de foco.
  - Fechamento da splash e restauração para `ShutdownMode.OnMainWindowClose`.
  - Tratamento de exceções: qualquer falha grava log detalhado em `%LOCALAPPDATA%\DotNetCadEngine\Logs\startup-error.log` e exibe mensagem amigável antes de encerrar.

---

## 5. Procedimentos de Build e Empacotamento

### Pré-requisitos:
- .NET 10 SDK instalado.
- Inno Setup 6 instalado (local padrão: `C:\Program Files (x86)\Inno Setup 6\ISCC.exe` ou no `PATH`).

### Build Local Reproduzível:
Execute no terminal PowerShell:

```powershell
# Execução completa (testes + ativos + publish + instalador Inno Setup)
.\build-installer.ps1

# Ou para compilar rapidamente pulando a execução dos testes:
.\build-installer.ps1 -SkipTests
```

### Assinatura Digital de Código (Opcional):
Para assinar o instalador em ambientes de produção ou CI, forneça o certificado PFX e senha via variáveis de ambiente:

```powershell
$env:SIGNING_CERT_PATH = "C:\Caminho\Certificado.pfx"
$env:SIGNING_CERT_PASSWORD = "SenhaDoCertificado"
.\build-installer.ps1
```
Se as variáveis não estiverem configuradas, o script gera o artefato devidamente identificado como **não assinado** (sem falhas e sem inventar certificados).

---

## 6. Artefatos Produzidos e Checksum

| Arquivo | Localização | Descrição |
|---|---|---|
| **Instalador x64** | `artifacts/installer/DotNetCadEngine-1.0.0-Setup-x64.exe` | Instalador standalone LZMA2 (43.62 MB) |
| **Checksums** | `artifacts/installer/checksums.txt` | Hash SHA-256 de integridade |
| **Ícone da Aplicação** | `src/DotNetCad.Demo/Assets/dotnetcad.ico` | Ícone Windows multi-resolução (16 a 256 px) |
| **Ativos do Instalador** | `installer/assets/wizard-banner.bmp` | Banner lateral do assistente de instalação |
| **Ativos do Instalador** | `installer/assets/wizard-small.bmp` | Logo do cabeçalho do instalador |
| **Vetor Editável** | `installer/assets/brand-logo.svg` | Marca gráfica vetorial original em SVG |

### Hash SHA-256 Verificado:
```
11F04927AA47870ECA0AF0CD8E6808D26D31B7C3FC548CE142583B5127C3BB92  DotNetCadEngine-1.0.0-Setup-x64.exe
```

---

## 7. Integração Contínua (GitHub Actions)

O repositório conta com dois workflows:
1. `.github/workflows/ci.yml`: Valida compilação e suíte completa de testes no Windows e Linux em todos os PRs e pushes na branch `main`.
2. `.github/workflows/package-installer.yml`: Empacota o instalador x64 via Inno Setup 6 e publica os artefatos (.exe e checksums) quando acionado manualmente (`workflow_dispatch`) ou via tag de release (`v*`).
