. "$PSScriptRoot\common.ps1"
$ProgressPreference = 'SilentlyContinue'
$toolchain = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
$toolsDirectory = Join-Path $RepoRoot '.tools'
New-Item -ItemType Directory -Path $toolsDirectory -Force | Out-Null

foreach ($tool in $toolchain.tools) {
    $executable = Join-Path $toolsDirectory $tool.executable
    if (Test-Path -LiteralPath $executable) { continue }
    $archive = Join-Path $toolsDirectory $tool.archive
    Write-Host "Downloading $($tool.name) $($tool.version)..."
    & curl.exe --fail --location --silent --show-error --output $archive $tool.url
    if ($LASTEXITCODE -ne 0) { throw "Download failed: $($tool.name)" }
    if ((Get-FileHash -LiteralPath $archive -Algorithm $tool.hashAlgorithm).Hash -ne $tool.hash) {
        throw "Checksum mismatch: $($tool.name)"
    }
    $destination = Join-Path $toolsDirectory $tool.destination
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
    if (-not (Test-Path -LiteralPath $executable)) { throw "Missing executable: $executable" }
}
Write-Host 'Repository-local toolchain is ready. System PATH was not changed.'
