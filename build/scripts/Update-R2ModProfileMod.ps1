# Updates an r2modman profile mods.yml entry to match a local Thunderstore-style build deploy.
param(
    [Parameter(Mandatory = $true)]
    [string]$ModsYmlPath,

    [Parameter(Mandatory = $true)]
    [string]$AuthorName,

    [Parameter(Mandatory = $true)]
    [string]$PackageName,

    [Parameter(Mandatory = $true)]
    [string]$DisplayName,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$DescriptionFile,

    [string]$WebsiteUrl = '',

    [string]$Dependencies = ''
)

$ErrorActionPreference = 'Stop'

function Get-VersionParts {
    param([string]$VersionString)
    $parts = ($VersionString -split '\.')[0..2]
    while ($parts.Count -lt 3) { $parts += '0' }
    [pscustomobject]@{
        Major = [int]$parts[0]
        Minor = [int]$parts[1]
        Patch = [int]$parts[2]
    }
}

function Format-YamlDescription {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) {
        return "  description: ''"
    }
    $normalized = ($Text -replace "`r`n", "`n").Trim()
    if ($normalized -notmatch "`n") {
        $escaped = $normalized -replace "'", "''"
        return "  description: '$escaped'"
    }
    $lines = ($normalized -split "`n") | ForEach-Object { $_.TrimEnd() }
    $body = ($lines | ForEach-Object { "    $_" }) -join "`n"
    return "  description: >-`n$body"
}

function Format-YamlDependencyList {
    param([string[]]$Items)
    if (-not $Items -or $Items.Count -eq 0) {
        return "  dependencies: []"
    }
    $lines = $Items | ForEach-Object { "    - $_" }
    return ("  dependencies:" + [Environment]::NewLine + ($lines -join [Environment]::NewLine))
}

function New-ModYamlBlock {
    param(
        [string]$FullName,
        [string]$Author,
        [string]$Display,
        [string]$SiteUrl,
        [string]$DescriptionYaml,
        [string]$DependencyYaml,
        [object]$VersionParts,
        [long]$InstalledAt
    )
    @"
- manifestVersion: 1
  name: $FullName
  authorName: $Author
  websiteUrl: $SiteUrl
  displayName: $Display
$DescriptionYaml
  gameVersion: '0'
  networkMode: both
  packageType: other
  installMode: managed
  installedAtTime: $InstalledAt
  loaders: []
$DependencyYaml
  incompatibilities: []
  optionalDependencies: []
  versionNumber:
    major: $($VersionParts.Major)
    minor: $($VersionParts.Minor)
    patch: $($VersionParts.Patch)
  enabled: true
"@
}

if (-not (Test-Path -LiteralPath $DescriptionFile)) {
    throw "Description file not found: $DescriptionFile"
}

$utf8 = [System.Text.UTF8Encoding]::new($false)
$description = [IO.File]::ReadAllText($DescriptionFile, $utf8).Trim()
$fullName = "$AuthorName-$PackageName"
$versionParts = Get-VersionParts -VersionString $Version
$depList = @()
if (-not [string]::IsNullOrWhiteSpace($Dependencies)) {
    $depList = $Dependencies.Split(';', [StringSplitOptions]::RemoveEmptyEntries) |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ }
}

if ([string]::IsNullOrWhiteSpace($WebsiteUrl)) {
    $WebsiteUrl = "https://thunderstore.io/c/valheim/p/$AuthorName/$PackageName/"
}

$installedAt = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$descriptionYaml = Format-YamlDescription -Text $description
$dependencyYaml = Format-YamlDependencyList -Items $depList
$newBlock = (New-ModYamlBlock -FullName $fullName -Author $AuthorName -Display $DisplayName `
    -SiteUrl $WebsiteUrl -DescriptionYaml $descriptionYaml -DependencyYaml $dependencyYaml `
    -VersionParts $versionParts -InstalledAt $installedAt).TrimEnd()

$parent = Split-Path -Parent $ModsYmlPath
if ($parent -and -not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

$existing = ''
if (Test-Path -LiteralPath $ModsYmlPath) {
    $existing = [IO.File]::ReadAllText($ModsYmlPath)
}

$blockPattern = '(?ms)^- manifestVersion:.*?(?=\r?\n- manifestVersion:|\z)'
$blocks = @()
if ([string]::IsNullOrWhiteSpace($existing)) {
    $blocks = @()
}
else {
    $blocks = [regex]::Matches($existing.TrimEnd(), $blockPattern) |
        ForEach-Object { $_.Value.TrimEnd() }
}

$updated = $false
$outBlocks = [System.Collections.Generic.List[string]]::new()
foreach ($block in $blocks) {
    if ($block -match '(?m)^\s+name:\s+(.+?)\s*$') {
        $entryName = $Matches[1].Trim()
        if ($entryName -eq $fullName) {
            $outBlocks.Add($newBlock)
            $updated = $true
            continue
        }
    }
    $outBlocks.Add($block)
}

if (-not $updated) {
    $outBlocks.Add($newBlock)
}

$content = ($outBlocks -join [Environment]::NewLine) + [Environment]::NewLine
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($ModsYmlPath, $content, $utf8NoBom)

$action = if ($updated) { 'Updated' } else { 'Added' }
Write-Host "$action mods.yml entry: $fullName ($Version)"
