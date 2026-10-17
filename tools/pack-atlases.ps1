<#
.SYNOPSIS
Script padronizado para empacotar pastas de sprites em atlases libGDX (.atlas).

.DESCRIPTION
Empacota colecoes de sprites a partir de subpastas de origem em arquivos .atlas + .png,
com padding, sem rotacao (evitando distorcao no MonoGame) e opcionalmente sincroniza
o preview de coordenadas para projetos Gum UI.

.PARAMETER SourceDir
Diretorio base contendo subpastas com sprites (ex: tools/AssetsSource). Cada subpasta vira um atlas com o mesmo nome.

.PARAMETER OutputDir
Diretorio de destino para os arquivos .atlas e .png (ex: src/MyGame/Content/Atlases).

.PARAMETER PackerProject
Caminho para o projeto de console do SpriteAtlasPacker (ex: lib/Nez/Nez.SpriteAtlasPacker/SpriteAtlasPacker.Console).

.PARAMETER GumDir
Diretorio opcional contendo arquivos Gum para sincronizar automaticamente as coordenadas de preview.

.PARAMETER Padding
Espacamento entre sprites com bleed extrusion (padrao: 2).

.EXAMPLE
powershell -File tools/pack-atlases.ps1 -SourceDir "tools/AssetsSource" -OutputDir "Content/Atlases" -PackerProject "lib/Nez/SpriteAtlasPacker.Console" -GumDir "Content/Gum"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [Parameter(Mandatory = $true)]
    [string]$PackerProject,

    [Parameter(Mandatory = $false)]
    [string]$GumDir = "",

    [Parameter(Mandatory = $false)]
    [int]$Padding = 2
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $SourceDir)) {
    Write-Error "Diretorio de origem nao encontrado: $SourceDir"
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$atlasFolders = Get-ChildItem -Path $SourceDir -Directory

foreach ($folder in $atlasFolders) {
    $name = $folder.Name.ToLowerInvariant()
    Write-Host "==> Empacotando atlas: $name..." -ForegroundColor Yellow

    $img = Join-Path $OutputDir "$name.png"
    $map = Join-Path $OutputDir "$name.atlas"

    dotnet run --project $PackerProject --no-launch-profile -- "-image:$img" "-map:$map" "-pad:$Padding" "-dontCreateAnimations" "$($folder.FullName)"
    Write-Host "    [OK] $name.atlas gerado com sucesso." -ForegroundColor Green
}

# Se fornecido GumDir, sincroniza o preview
if (-not [string]::IsNullOrWhiteSpace($GumDir) -and (Test-Path $GumDir)) {
    $syncScript = Join-Path $PSScriptRoot "sync-gum-preview-coords.ps1"
    if (Test-Path $syncScript) {
        Write-Host "==> Sincronizando coordenadas de preview para Gum UI..." -ForegroundColor Yellow
        powershell -File $syncScript -AtlasDir $OutputDir -GumDir $GumDir
    }
}

Write-Host "==> Processo de empacotamento concluido com exito!" -ForegroundColor Green
