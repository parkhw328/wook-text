. "$PSScriptRoot\common.ps1"
Push-Location $RepoRoot
try { Invoke-Dotnet run --project src/WookText.App/WookText.App.csproj -- @args }
finally { Pop-Location }
