# Mirror GitHub Actions: copy Pfhoenix stubs into a fake Valheim tree used as compile refs.
# Live publicized assemblies hide Thunderstore-only type mismatches (Hoverable.GetHoverOffset, Character.Message).
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir,

    [Parameter(Mandatory = $true)]
    [string]$OutRoot
)

$ErrorActionPreference = 'Stop'

function Find-PfhoenixLib {
    $roots = @(
        (Join-Path $ProjectDir "packages"),
        (Join-Path (Split-Path $ProjectDir) "DrakesRenameIt\packages"),
        (Join-Path (Split-Path $ProjectDir) "DrakeModsLibs\packages")
    )
    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        $hit = Get-ChildItem -Path $root -Directory -Filter "Pfhoenix.Valheim.ModProjectReferences.*" -ErrorAction SilentlyContinue |
            ForEach-Object { Join-Path $_.FullName "lib\net46" } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
        if ($hit) { return $hit }
    }
    return $null
}

$lib = Find-PfhoenixLib
if (-not $lib) {
    Write-Host "STAGE_CI_REFS_SKIP=1"
    exit 0
}

$managed = Join-Path $OutRoot "valheim_Data\Managed"
$pub = Join-Path $managed "publicized_assemblies"
$bep = Join-Path $OutRoot "BepInEx\core"
New-Item -ItemType Directory -Force -Path $managed, $pub, $bep | Out-Null

Copy-Item (Join-Path $lib "*.dll") $managed -Force
Copy-Item (Join-Path $lib "assembly_valheim.dll") (Join-Path $pub "assembly_valheim_publicized.dll") -Force
if (Test-Path (Join-Path $lib "assembly_utils.dll")) {
    Copy-Item (Join-Path $lib "assembly_utils.dll") (Join-Path $pub "assembly_utils_publicized.dll") -Force
}
if (Test-Path (Join-Path $lib "gui_framework.dll")) {
    Copy-Item (Join-Path $lib "gui_framework.dll") (Join-Path $pub "gui_framework_publicized.dll") -Force
}
if (Test-Path (Join-Path $lib "0Harmony.dll")) {
    Copy-Item (Join-Path $lib "0Harmony.dll") (Join-Path $bep "0Harmony.dll") -Force
}
if (Test-Path (Join-Path $lib "bepinex.dll")) {
    Copy-Item (Join-Path $lib "bepinex.dll") (Join-Path $bep "BepInEx.dll") -Force
}

Write-Host "STAGE_CI_REFS_OK=$OutRoot"
Write-Host "Pfhoenix lib: $lib"
