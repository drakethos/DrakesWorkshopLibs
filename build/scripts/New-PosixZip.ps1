# Create a zip with forward-slash entries (Thunderstore / unix zip -r).
# Windows Compress-Archive uses backslashes; Gale then flattens folders into the plugin root.
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDir,

    [Parameter(Mandatory = $true)]
    [string]$ZipPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$source = (Resolve-Path -LiteralPath $SourceDir).Path.TrimEnd('\', '/')
$zipFull = $ZipPath
if (-not [System.IO.Path]::IsPathRooted($zipFull)) {
    $zipFull = Join-Path (Get-Location) $ZipPath
}

$parent = Split-Path -Parent $zipFull
if ($parent -and -not (Test-Path $parent)) {
    New-Item -ItemType Directory -Path $parent | Out-Null
}
if (Test-Path $zipFull) {
    Remove-Item -LiteralPath $zipFull -Force
}

$zip = [System.IO.Compression.ZipFile]::Open($zipFull, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($source.Length).TrimStart('\', '/').Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $_.FullName,
            $rel,
            [System.IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Posix zip: $zipFull"
