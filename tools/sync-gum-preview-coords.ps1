<#
.SYNOPSIS
Sincroniza as coordenadas de corte (.atlas) nos arquivos de UI do Gum (.gusx / .gucx).

.DESCRIPTION
O Gum Tool nao le arquivos de atlas (.atlas) nativamente em tempo de edicao; ele renderiza
baseado nas coordenadas TextureLeft, TextureTop, TextureWidth e TextureHeight salvas no XML.
Este script le os arquivos .atlas gerados pelo empacotador e injeta automaticamente as
coordenadas exatas em cada elemento do Gum que utiliza o sprite, permitindo visualizacao
fiel no Gum Tool sem necessidade de rodar o jogo.

.PARAMETER AtlasDir
Diretorio onde estao os arquivos .atlas e .png gerados (ex: src/MyGame/Content/Atlases).

.PARAMETER GumDir
Diretorio onde estao os arquivos do Gum (.gusx, .gucx, GumProject.gumx).

.EXAMPLE
powershell -File tools/sync-gum-preview-coords.ps1 -AtlasDir "Content/Atlases" -GumDir "Content/Gum"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AtlasDir,

    [Parameter(Mandatory = $true)]
    [string]$GumDir
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $AtlasDir)) {
    Write-Warning "Diretorio de atlas nao encontrado: $AtlasDir"
    exit 0
}

if (-not (Test-Path $GumDir)) {
    Write-Warning "Diretorio do Gum nao encontrado: $GumDir"
    exit 0
}

# 1. Carrega todos os sprites dos arquivos .atlas
$atlasFiles = Get-ChildItem -Path $AtlasDir -Filter "*.atlas" -File
if ($atlasFiles.Count -eq 0) {
    Write-Host "Nenhum arquivo .atlas encontrado em: $AtlasDir"
    exit 0
}

$sprites = @{}

foreach ($atlasFile in $atlasFiles) {
    $lines = Get-Content -Path $atlasFile.FullName
    $currentSprite = $null

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i].Trim()
        if ([string]::IsNullOrWhiteSpace($line)) { continue }

        if (-not $line.StartsWith(' ') -and -not $line.Contains(':')) {
            $currentSprite = $line
            continue
        }

        if ($currentSprite -and $line.StartsWith('xy:')) {
            $coords = $line.Substring(3).Trim().Split(',')
            $x = [int]$coords[0].Trim()
            $y = [int]$coords[1].Trim()

            # Proxima linha tipicamente e 'size:'
            if ($i + 1 -lt $lines.Count -and $lines[$i + 1].Trim().StartsWith('size:')) {
                $sizes = $lines[$i + 1].Trim().Substring(5).Trim().Split(',')
                $w = [int]$sizes[0].Trim()
                $h = [int]$sizes[1].Trim()

                $sprites[$currentSprite] = @{
                    X = $x
                    Y = $y
                    Width = $w
                    Height = $h
                    Atlas = [System.IO.Path]::GetFileNameWithoutExtension($atlasFile.Name)
                }
            }
        }
    }
}

Write-Host "Carregados $($sprites.Count) sprites dos atlas em: $AtlasDir"

# 2. Atualiza arquivos .gusx e .gucx no GumDir
$gumFiles = Get-ChildItem -Path $GumDir -Include "*.gusx", "*.gucx" -Recurse -File
$updatedCount = 0

function Set-Variable([System.Xml.XmlDocument]$xml, [System.Xml.XmlElement]$stateElement, [string]$varName, [string]$varType, [string]$value) {
    $existing = $stateElement.SelectSingleNode("Variable[Name='$varName']")
    if ($existing) {
        $valNode = $existing.SelectSingleNode('Value')
        if ($valNode) {
            $valNode.InnerText = $value
        } else {
            $existing.SetAttribute('Value', $value)
        }
    } else {
        $newVar = $xml.CreateElement('Variable')
        $varTypeAttr = $xml.CreateAttribute('xsi', 'type', 'http://www.w3.org/2001/XMLSchema-instance')
        $varTypeAttr.Value = 'VariableSave'
        $newVar.Attributes.Append($varTypeAttr) | Out-Null

        $typeElem = $xml.CreateElement('Type')
        $typeElem.InnerText = $varType
        $newVar.AppendChild($typeElem) | Out-Null

        $nameElem = $xml.CreateElement('Name')
        $nameElem.InnerText = $varName
        $newVar.AppendChild($nameElem) | Out-Null

        $valElem = $xml.CreateElement('Value')
        $valTypeAttr = $xml.CreateAttribute('xsi', 'type', 'http://www.w3.org/2001/XMLSchema-instance')
        $valTypeAttr.Value = 'xsd:int'
        $valElem.Attributes.Append($valTypeAttr) | Out-Null
        $valElem.InnerText = $value
        $newVar.AppendChild($valElem) | Out-Null

        $stateElement.AppendChild($newVar) | Out-Null
    }
}

foreach ($gumFile in $gumFiles) {
    [xml]$xml = Get-Content -Path $gumFile.FullName -Raw -Encoding UTF8
    $modified = $false
    $defaultState = $xml.SelectSingleNode('//State[Name="Default"]')
    if (-not $defaultState) { continue }

    # Procura variaveis SourceFile existentes para saber quais sprites sao usados
    $sourceVars = $defaultState.SelectNodes("Variable[contains(Name, 'SourceFile') and contains(Value, '.png')]")
    foreach ($v in $sourceVars) {
        $rawPath = $v.SelectSingleNode('Value').InnerText
        $fileName = [System.IO.Path]::GetFileNameWithoutExtension($rawPath)

        if ($sprites.ContainsKey($fileName)) {
            $info = $sprites[$fileName]
            $prefix = ''
            if ($v.Name.Contains('.')) {
                $prefix = $v.Name.Substring(0, $v.Name.LastIndexOf('.') + 1)
            }

            Set-Variable $xml $defaultState "${prefix}TextureLeft" "int" $info.X.ToString()
            Set-Variable $xml $defaultState "${prefix}TextureTop" "int" $info.Y.ToString()
            Set-Variable $xml $defaultState "${prefix}TextureWidth" "int" $info.Width.ToString()
            Set-Variable $xml $defaultState "${prefix}TextureHeight" "int" $info.Height.ToString()
            $modified = $true
        }
    }

    if ($modified) {
        $xml.Save($gumFile.FullName)
        Write-Host "    [OK] $($gumFile.Name) atualizado com coordenadas do atlas." -ForegroundColor Green
        $updatedCount++
    }
}

Write-Host "Sincronizacao de coordenadas concluida ($updatedCount arquivos atualizados)." -ForegroundColor Cyan
