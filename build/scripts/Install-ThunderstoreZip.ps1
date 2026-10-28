# Wipe DestDir and install a Thunderstore zip into it the way Gale / r2modman do, so a local build lands exactly where a
# store install would. DestDir is the package's plugin folder (BepInEx\plugins\<Author-Name>).
#   plugins\<x>    -> DestDir\<x>          (sub-folders kept)
#   patchers\<x>   -> BepInEx\patchers\<Author-Name>\<x>      monomod\ and core\ likewise
#   config\<x>     -> BepInEx\config\<x>
#   anything else  -> DestDir\<file name>  (FLATTENED: why packages must keep their files under plugins\)
#   manifest.json, icon.png, README.md, CHANGELOG.md at the zip root are copied to DestDir too (harmless, as before).
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

$DestDir = [System.IO.Path]::GetFullPath($DestDir).TrimEnd('\', '/')
$pkgName = Split-Path -Leaf $DestDir
$bepinex = Split-Path -Parent (Split-Path -Parent $DestDir)   # ...\BepInEx
$rootOnly = @('manifest.json', 'icon.png', 'readme.md', 'changelog.md')
$subdirs = @{ plugins = $DestDir; patchers = (Join-Path (Join-Path $bepinex 'patchers') $pkgName); monomod = (Join-Path (Join-Path $bepinex 'monomod') $pkgName); core = (Join-Path (Join-Path $bepinex 'core') $pkgName); config = (Join-Path $bepinex 'config') }

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("tszip-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    [System.IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path -LiteralPath $ZipPath).Path, $tmp)

    $from = $tmp
    $kids = @(Get-ChildItem -LiteralPath $tmp)
    if ($kids.Count -eq 1 -and $kids[0].PSIsContainer -and -not $subdirs.ContainsKey($kids[0].Name.ToLowerInvariant())) {
        $from = $kids[0].FullName   # a single wrapper folder around the package
    }

    if (Test-Path -LiteralPath $DestDir) {
        Remove-Item -LiteralPath $DestDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $DestDir -Force | Out-Null

    Get-ChildItem -LiteralPath $from -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($from.Length).TrimStart('\', '/') -replace '\\', '/'
        $parts = $rel.Split('/')
        if ($parts.Length -eq 1 -and ($rootOnly -contains $parts[0].ToLowerInvariant())) {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $DestDir $parts[0]) -Force
            return
        }
        $target = $null
        for ($i = 0; $i -lt $parts.Length - 1; $i++) {
            $key = $parts[$i].ToLowerInvariant()
            if ($subdirs.ContainsKey($key)) {
                $rest = if ($i + 1 -lt $parts.Length) { $parts[($i + 1)..($parts.Length - 1)] } else { @() }
                $target = Join-Path $subdirs[$key] ($rest -join [System.IO.Path]::DirectorySeparatorChar)
                break
            }
        }
        if (-not $target) {
            $target = Join-Path $DestDir $parts[$parts.Length - 1]   # flattened, exactly like Gale
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }
}
finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Installed zip -> $DestDir"
