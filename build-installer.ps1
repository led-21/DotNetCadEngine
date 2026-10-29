param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipTests = $false,
    [string]$SigningCertPath = $env:SIGNING_CERT_PATH,
    [string]$SigningCertPassword = $env:SIGNING_CERT_PASSWORD
)

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  DotNetCadEngine -- Build e Empacotamento do Instalador x64" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Localizar compilador Inno Setup (ISCC.exe)
$isccPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$iscc = $null
$cmdIscc = Get-Command iscc -ErrorAction SilentlyContinue
if ($cmdIscc) { $iscc = $cmdIscc.Source }
if (-not $iscc) {
    foreach ($candidate in $isccPaths) {
        if (Test-Path $candidate) {
            $iscc = $candidate
            break
        }
    }
}

if (-not $iscc) {
    Write-Error "Inno Setup 6 (ISCC.exe) nao foi encontrado no sistema. Instale o Inno Setup 6 ou configure no PATH."
}
Write-Host "Compilador Inno Setup: $iscc" -ForegroundColor Gray

# 2. Executar suite de testes antes do empacotamento
if (-not $SkipTests) {
    Write-Host "`n[1/5] Executando suite completa de testes..." -ForegroundColor Yellow
    dotnet test "$RepoRoot\DotNetCadEngine.slnx" -c $Configuration --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Falha na execucao dos testes unitarios. Build interrompido."
    }
    Write-Host "Todos os testes passaram com sucesso." -ForegroundColor Green
} else {
    Write-Host "`n[1/5] Testes ignorados (-SkipTests)." -ForegroundColor Yellow
}

# 3. Garantir ativos visuais tecnicos (.ico, .bmp)
Write-Host "`n[2/5] Verificando ativos graficos e icones..." -ForegroundColor Yellow
& "$RepoRoot\scripts\generate-assets.ps1"

# 4. Publicar aplicativo WPF como self-contained x64
$PublishDir = "$RepoRoot\artifacts\publish\x64"
$InstallerDir = "$RepoRoot\artifacts\installer"

Write-Host "`n[3/5] Publicando DotNetCad.Demo (Config: $Configuration, Runtime: $Runtime, self-contained)..." -ForegroundColor Yellow
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
New-Item -ItemType Directory -Path $InstallerDir -Force | Out-Null

dotnet publish "$RepoRoot\src\DotNetCad.Demo\DotNetCad.Demo.csproj" `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Falha ao publicar DotNetCad.Demo."
}

# Copiar licenca para a pasta de publicacao
Copy-Item "$RepoRoot\LICENSE" -Destination "$PublishDir\LICENSE" -Force

Write-Host "`nPublicando DotNetCad.Mcp (Servidor MCP de IA)..." -ForegroundColor Yellow
$McpPublishDir = "$PublishDir\mcp"
New-Item -ItemType Directory -Path $McpPublishDir -Force | Out-Null
& dotnet publish "$RepoRoot\src\DotNetCad.Mcp\DotNetCad.Mcp.csproj" -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=false -o $McpPublishDir
if ($LASTEXITCODE -ne 0) { Write-Error "Falha ao publicar DotNetCad.Mcp." }

$mainExe = "$PublishDir\DotNetCad.Demo.exe"
if (-not (Test-Path $mainExe)) {
    Write-Error "O executavel principal nao foi gerado em '$mainExe'."
}

$publishSizeMb = [math]::Round(((Get-ChildItem -Recurse $PublishDir | Measure-Object -Property Length -Sum).Sum / 1MB), 2)
Write-Host "Publish concluido com sucesso. Tamanho total da distribuicao: $publishSizeMb MB." -ForegroundColor Green

# 5. Compilar instalador Inno Setup
Write-Host "`n[4/5] Compilando instalador Inno Setup..." -ForegroundColor Yellow
$issScript = "$RepoRoot\installer\DotNetCadEngine.iss"
& $iscc $issScript

if ($LASTEXITCODE -ne 0) {
    Write-Error "Falha na compilacao do script Inno Setup."
}

# Localizar instalador gerado
$installerExe = $null
$foundInstallers = @(Get-ChildItem "$InstallerDir\DotNetCadEngine-*-Setup-x64.exe" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)
if ($foundInstallers.Count -gt 0) {
    $installerExe = $foundInstallers[0].FullName
}
if (-not $installerExe -or -not (Test-Path $installerExe)) {
    Write-Error "Instalador nao encontrado em '$InstallerDir'."
}

# 6. Assinatura de codigo (se certificado for fornecido)
Write-Host "`n[5/5] Verificando assinatura digital de codigo..." -ForegroundColor Yellow
if ($SigningCertPath -and (Test-Path $SigningCertPath)) {
    $signtool = $null
    $cmdSigntool = Get-Command signtool -ErrorAction SilentlyContinue
    if ($cmdSigntool) { $signtool = $cmdSigntool.Source }
    if ($signtool) {
        Write-Host "Assinando executavel com certificado '$SigningCertPath'..." -ForegroundColor Cyan
        $passArg = if ($SigningCertPassword) { "/p `"$SigningCertPassword`"" } else { "" }
        & $signtool sign /fd SHA256 /f "$SigningCertPath" $passArg /tr http://timestamp.digicert.com /td SHA256 "$installerExe"
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Artefato assinado digitalmente com sucesso." -ForegroundColor Green
        } else {
            Write-Warning "Falha na execucao do signtool. O artefato permanece sem assinatura."
        }
    } else {
        Write-Warning "signtool.exe nao encontrado no PATH. O instalador foi gerado sem assinatura."
    }
} else {
    Write-Host "Certificado nao informado (SIGNING_CERT_PATH ausente)." -ForegroundColor Gray
    Write-Host "Declaracao: Artefato gerado sem assinatura digital (ambiente de desenvolvimento/teste)." -ForegroundColor Gray
}

# 7. Gerar e gravar checksum SHA-256
$hash = (Get-FileHash -Path $installerExe -Algorithm SHA256).Hash
$checksumFile = "$InstallerDir\checksums.txt"
$fileName = [System.IO.Path]::GetFileName($installerExe)
"$hash  $fileName" | Out-File -FilePath $checksumFile -Encoding utf8 -Force

$installerSizeMb = [math]::Round(((Get-Item $installerExe).Length / 1MB), 2)

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  EMPACOTAMENTO CONCLUIDO COM SUCESSO" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host "Instalador : $installerExe" -ForegroundColor White
Write-Host "Tamanho    : $installerSizeMb MB" -ForegroundColor White
Write-Host "SHA-256    : $hash" -ForegroundColor White
Write-Host "Checksum   : $checksumFile" -ForegroundColor White
Write-Host "============================================================" -ForegroundColor Green
