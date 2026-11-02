$ErrorActionPreference = 'Stop'
$previewRoot = Join-Path (Split-Path -Parent $PSScriptRoot) '.tools\webview2'
$runtime = Join-Path $previewRoot 'runtime'
$version = '156.0.4314.58'
$archive = Join-Path $previewRoot "Microsoft.WebView2.FixedVersionRuntime.$version.x64.cab"
$archiveHash = '857dcfd6f7035508a29645fa2978c3a6a841f220df77cc366d318894e19f4792'
$executableHash = '8b6fabdf9f4ebb331cb7325bb9947e4e3f9a2b5345602feeacf277b1f9f355be'
$executable = Join-Path $runtime 'msedgewebview2.exe'
$inventoryPath = Join-Path $previewRoot 'runtime-inventory.json'
New-Item -ItemType Directory -Path $previewRoot -Force | Out-Null
$valid = (Test-Path -LiteralPath $executable) -and (Test-Path -LiteralPath $inventoryPath)
if ($valid) {
    $valid = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash -eq $executableHash
    if ($valid) {
        $inventory = Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json
        foreach ($entry in $inventory) {
            $file = Join-Path $runtime $entry.Path
            if (-not (Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Hash) { $valid = $false; break }
        }
    }
}
if (-not $valid) {
    if (-not (Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $archiveHash) {
        Invoke-WebRequest 'https://msedge.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/b7919cc9-680d-4253-bf32-732988e68aa6/Microsoft.WebView2.FixedVersionRuntime.156.0.4314.58.x64.cab' -OutFile $archive -UseBasicParsing
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $archiveHash) { throw 'WebView2 runtime checksum mismatch.' }
    $staging = Join-Path $previewRoot ('extract-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $staging | Out-Null
    try {
        & "$env:SystemRoot\System32\expand.exe" '-F:*' $archive $staging | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not extract WebView2 runtime.' }
        $extracted = Join-Path $staging "Microsoft.WebView2.FixedVersionRuntime.$version.x64"
        if ((Get-FileHash -LiteralPath (Join-Path $extracted 'msedgewebview2.exe') -Algorithm SHA256).Hash -ne $executableHash) { throw 'Unexpected WebView2 executable.' }
        if (Test-Path -LiteralPath $runtime) {
            if ((Get-Item -LiteralPath $runtime).Attributes -band [System.IO.FileAttributes]::ReparsePoint) { throw 'Runtime directory must not be a link.' }
            Remove-Item -LiteralPath $runtime -Recurse -Force
        }
        Move-Item -LiteralPath $extracted -Destination $runtime
        $inventory = @(Get-ChildItem -LiteralPath $runtime -File -Recurse | ForEach-Object {
            @{ Path = $_.FullName.Substring($runtime.Length + 1); Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        })
        $inventory | ConvertTo-Json | Set-Content -LiteralPath $inventoryPath -Encoding UTF8
    } finally { Remove-Item -LiteralPath $staging -Recurse -Force }
}
Write-Host "Verified WebView2 Fixed Version $version (Windows x64)."
