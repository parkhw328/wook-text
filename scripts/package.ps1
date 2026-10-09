param([switch]$SmokeInstaller)
. "$PSScriptRoot\common.ps1"
Push-Location $RepoRoot
try {
    [xml]$properties = Get-Content -LiteralPath 'Directory.Build.props'
    $version = $properties.Project.PropertyGroup.Version
    $suffix = if ($SmokeInstaller) { '-smoke' } else { '' }
    $publish = Join-Path $RepoRoot 'artifacts\publish\win-x64'
    $installerDirectory = Join-Path $RepoRoot 'artifacts\installer'
    $generated = Join-Path $RepoRoot 'artifacts\nsis'
    $compiler = Join-Path $RepoRoot '.tools\nsis-3.13\makensis.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { throw 'Run scripts/bootstrap.ps1 to install NSIS.' }
    $expectedPublish = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts\publish\win-x64'))
    if ([System.IO.Path]::GetFullPath($publish) -ne $expectedPublish) { throw 'Unexpected publish directory.' }
    if (Test-Path -LiteralPath $publish) {
        if ((Get-Item -LiteralPath $publish).Attributes -band [System.IO.FileAttributes]::ReparsePoint) { throw 'Publish directory must not be a link.' }
        Remove-Item -LiteralPath $publish -Recurse -Force
    }
    foreach ($directory in @($publish, $installerDirectory, $generated)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    Invoke-Dotnet publish src/WookText.App/WookText.App.csproj -c Release --self-contained true -o $publish -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -p:RestoreLockedMode=true

    # Preserve the notices shipped by the exact runtime packages in this build.
    $nugetRoot = Join-Path $env:USERPROFILE '.nuget\packages'
    if ($env:NUGET_PACKAGES) { $nugetRoot = $env:NUGET_PACKAGES }
    $runtimeConfig = Get-Content -LiteralPath (Join-Path $publish 'wText.runtimeconfig.json') -Raw | ConvertFrom-Json
    $runtimeFolders = $runtimeConfig.runtimeOptions.includedFrameworks
    foreach ($runtime in $runtimeFolders) {
        $runtimeDirectory = Join-Path $nugetRoot ($runtime.name.ToLowerInvariant() + '.runtime.win-x64\' + $runtime.version)
        $runtimeLicense = @('LICENSE.TXT', 'LICENSE') | ForEach-Object { Join-Path $runtimeDirectory $_ } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $runtimeLicense) { throw "Runtime license missing: $runtimeDirectory" }
        Copy-Item -LiteralPath $runtimeLicense -Destination (Join-Path $publish ('licenses\' + $runtime.name + '-LICENSE.txt'))
        $runtimeNotice = Join-Path $runtimeDirectory 'THIRD-PARTY-NOTICES.TXT'
        if (Test-Path -LiteralPath $runtimeNotice) {
            Copy-Item -LiteralPath $runtimeNotice -Destination (Join-Path $publish ('licenses\' + $runtime.name + '-THIRD-PARTY-NOTICES.txt'))
        } elseif ($runtime.version -ne '10.0.12') {
            throw 'Review and update the bundled WPF/Windows Forms notices for the new runtime version.'
        }
    }
    if (@($runtimeFolders).Count -ne 2) { throw 'Expected both .NET and Windows Desktop runtime notices.' }

    $files = @(Get-ChildItem -LiteralPath $publish -Recurse -File | Sort-Object FullName)
    $uninstallLines = [System.Collections.Generic.List[string]]::new()
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($publish.Length + 1)
        $uninstallLines.Add('Delete "$INSTDIR\' + $relative + '"')
    }
    Get-ChildItem -LiteralPath $publish -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
        $relative = $_.FullName.Substring($publish.Length + 1)
        $uninstallLines.Add('RMDir "$INSTDIR\' + $relative + '"')
    }
    $uninstallInclude = Join-Path $generated 'uninstall-files.nsh'
    [System.IO.File]::WriteAllLines($uninstallInclude, $uninstallLines, [System.Text.UTF8Encoding]::new($true))
    $output = Join-Path $installerDirectory "wText-$version-win-x64-setup$suffix.exe"
    $compilerArguments = @('/V2', "/DAPP_VERSION=$version", "/DPAYLOAD=$publish", "/DOUTPUT=$output", "/DUNINSTALL_FILES=$uninstallInclude")
    if ($SmokeInstaller) { $compilerArguments += '/DSMOKE_INSTALLER' }
    & $compiler @compilerArguments (Join-Path $RepoRoot 'installer\wText.nsi')
    if ($LASTEXITCODE -ne 0) { throw 'NSIS failed to create the installer.' }

    if (-not $SmokeInstaller) {
        $portable = Join-Path $RepoRoot "artifacts\wText-$version-win-x64-portable.zip"
        Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $portable -Force
        Get-FileHash -LiteralPath $output, $portable -Algorithm SHA256 |
            ForEach-Object { $_.Hash.ToLowerInvariant() + '  ' + (Split-Path -Leaf $_.Path) } |
            Set-Content -LiteralPath (Join-Path $RepoRoot 'artifacts\SHA256SUMS.txt') -Encoding ASCII
    }
    Write-Host "Created: $output"
} finally { Pop-Location }
