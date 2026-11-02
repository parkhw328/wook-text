param([switch]$SkipTests)
. "$PSScriptRoot\common.ps1"
& "$PSScriptRoot\prepare-formatter.ps1"
& "$PSScriptRoot\prepare-webview.ps1"
Push-Location $RepoRoot
try {
    Invoke-Dotnet restore WookText.slnx --locked-mode
    Invoke-Dotnet build WookText.slnx -c Release --no-restore
    if (-not $SkipTests) {
        & (Join-Path $RepoRoot '.tools\formatter\node.exe') --test tests/generate-installer.test.cjs
        if ($LASTEXITCODE -ne 0) { throw 'Installer ownership catalog checks failed.' }
        Invoke-Dotnet test tests/WookText.Core.Tests/WookText.Core.Tests.csproj -c Release --no-build --no-restore
        Invoke-Dotnet run --project tests/WookText.App.SmokeTests/WookText.App.SmokeTests.csproj -c Release --no-build --no-restore -- artifacts/smoke
    }
} finally { Pop-Location }
