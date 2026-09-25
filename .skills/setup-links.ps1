# Execute na raiz do repositorio:
#   powershell -ExecutionPolicy Bypass -File .skills\setup-links.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sharedRoot = Join-Path $repoRoot '.skills'
$claudeRoot = Join-Path $repoRoot '.claude\skills'
$codexRoot = Join-Path $repoRoot '.agents\skills'

function Get-FileManifest {
    param([string]$Directory)

    $root = [System.IO.Path]::GetFullPath($Directory).TrimEnd('\', '/')
    @(
        Get-ChildItem -LiteralPath $root -Force -Recurse -File |
            ForEach-Object {
                $relative = $_.FullName.Substring($root.Length + 1)
                "$relative|$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
            } |
            Sort-Object
    )
}

function Get-ExistingItem {
    param([string]$Path)
    Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
}

function Assert-LinkTarget {
    param([System.IO.FileSystemInfo]$Link, [string]$ExpectedTarget)

    if ($Link.LinkType -ne 'SymbolicLink') {
        throw "Ja existe um item que nao e link simbolico: $($Link.FullName)"
    }

    $actualTarget = [string]$Link.Target
    if (-not [System.IO.Path]::IsPathRooted($actualTarget)) {
        $actualTarget = Join-Path $Link.Parent.FullName $actualTarget
    }
    $actualTarget = [System.IO.Path]::GetFullPath($actualTarget).TrimEnd('\', '/')
    $expected = [System.IO.Path]::GetFullPath($ExpectedTarget).TrimEnd('\', '/')
    if (-not [string]::Equals($actualTarget, $expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Link aponta para outro destino: $($Link.FullName) -> $actualTarget"
    }
}

$skills = @(Get-ChildItem -LiteralPath $sharedRoot -Directory -Force |
    Where-Object { -not $_.LinkType } |
    Sort-Object Name)
if ($skills.Count -eq 0) {
    throw "Nenhuma pasta de skill encontrada em $sharedRoot"
}

# Confere todos os caminhos antes de remover uma copia antiga.
$oldCopies = @()
foreach ($skill in $skills) {
    if (-not (Test-Path -LiteralPath (Join-Path $skill.FullName 'SKILL.md') -PathType Leaf)) {
        throw "SKILL.md ausente em $($skill.FullName)"
    }

    foreach ($location in @($claudeRoot, $codexRoot)) {
        $linkPath = Join-Path $location $skill.Name
        $existing = Get-ExistingItem $linkPath
        if ($null -eq $existing) { continue }

        if ($existing.LinkType) {
            Assert-LinkTarget -Link $existing -ExpectedTarget $skill.FullName
            continue
        }

        if ($location -ne $claudeRoot -or -not $existing.PSIsContainer) {
            throw "Destino ocupado; resolva manualmente antes de continuar: $linkPath"
        }

        $difference = @(Compare-Object (Get-FileManifest $existing.FullName) (Get-FileManifest $skill.FullName))
        if ($difference.Count -ne 0) {
            throw "As pastas diferem; nenhuma copia sera removida: $linkPath e $($skill.FullName)"
        }
        $oldCopies += $existing.FullName
    }
}

New-Item -ItemType Directory -Path $claudeRoot, $codexRoot -Force | Out-Null

# Testa a permissao de criar symlinks antes de substituir qualquer pasta do Claude.
$probe = Join-Path $codexRoot ('.symlink-probe-' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType SymbolicLink -Path $probe -Target $skills[0].FullName -ErrorAction Stop | Out-Null
} catch {
    throw "Windows bloqueou a criacao de links simbolicos. Ative o Modo de Desenvolvedor ou execute o PowerShell como administrador. Detalhe: $($_.Exception.Message)"
} finally {
    if (Get-ExistingItem $probe) { Remove-Item -LiteralPath $probe -Force }
}

foreach ($oldCopy in $oldCopies) {
    $resolved = [System.IO.Path]::GetFullPath($oldCopy)
    $allowedPrefix = [System.IO.Path]::GetFullPath($claudeRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Remocao fora de .claude/skills bloqueada: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

foreach ($skill in $skills) {
    foreach ($location in @($claudeRoot, $codexRoot)) {
        $linkPath = Join-Path $location $skill.Name
        if (-not (Get-ExistingItem $linkPath)) {
            New-Item -ItemType SymbolicLink -Path $linkPath -Target $skill.FullName -ErrorAction Stop | Out-Null
        }
        Assert-LinkTarget -Link (Get-ExistingItem $linkPath) -ExpectedTarget $skill.FullName
    }
    Write-Host "OK: $($skill.Name)"
}

Write-Host "Skills compartilhadas: $sharedRoot"
