# Wipe DestDir and extract a Thunderstore zip into it (same layout as Gale/r2modman import).
param(
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,

    [Parameter(Mandatory = $true)]
    [string]$DestDir
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not (Test-Path -LiteralPath $ZipPath)) {
    throw "Zip not found: $ZipPath"
}

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("tszip-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    [System.IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path -LiteralPath $ZipPath).Path, $tmp)

    $kids = @(Get-ChildItem -LiteralPath $tmp)
    $from = $tmp
    if ($kids.Count -eq 1 -and $kids[0].PSIsContainer) {
        $from = $kids[0].FullName
    }

    if (Test-Path -LiteralPath $DestDir) {
        Remove-Item -LiteralPath $DestDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $DestDir | Out-Null
    Copy-Item -Path (Join-Path $from '*') -Destination $DestDir -Recurse -Force
}
finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Installed zip -> $DestDir"
