. "$PSScriptRoot\common.ps1"
$screenshots = @('user-guide.png', 'minimap.png', 'search-focus.png', 'search-linked-editing.png', 'explorer.png', 'workspaces.png', 'save-confirmation.png', 'comparison.png', 'comparison-input.png', 'session-restored.png', 'preferences.png', 'licenses.png', 'formatting.png', 'indentation.png')
$destination = Join-Path $RepoRoot 'docs\images'
foreach ($name in $screenshots) {
    if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot ('artifacts\smoke\' + $name)))) {
        throw 'Run scripts/build.ps1 successfully before updating documentation images.'
    }
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($name in $screenshots) {
    Copy-Item -LiteralPath (Join-Path $RepoRoot ('artifacts\smoke\' + $name)) -Destination (Join-Path $destination $name)
}
Write-Host 'Updated the README screenshots from the WPF verification output.'
