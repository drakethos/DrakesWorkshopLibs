param(
    [Parameter(Mandatory = $true)]
    [string]$DllPath
)

if (-not (Test-Path -LiteralPath $DllPath)) {
    throw "DLL not found: $DllPath"
}

$bytes = [IO.File]::ReadAllBytes($DllPath)
$text = [Text.Encoding]::ASCII.GetString($bytes)

# ILRepack internalizes ServerSync into WorkshopLibs only; consumer mod DLLs must not contain it.
# DrakeConfigSync is allowed; these markers indicate ServerSync IL was merged in.
$markers = @('ServerSync', 'ServerSyncManager', 'ServerSync.ConfigSync')
foreach ($marker in $markers) {
    if ($text.Contains($marker)) {
        throw "Forbidden embed detected ($marker) in $DllPath. ServerSync belongs only in DrakesWorkshopLibs."
    }
}

Write-Host "OK: no ServerSync markers in $(Split-Path -Leaf $DllPath)"
