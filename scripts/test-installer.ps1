param([string]$PreviousSmokeInstaller)
. "$PSScriptRoot\common.ps1"
[xml]$properties = Get-Content -LiteralPath (Join-Path $RepoRoot 'Directory.Build.props')
$version = $properties.Project.PropertyGroup.Version
$setup = Join-Path $RepoRoot "artifacts\installer\wText-$version-win-x64-setup-smoke.exe"
if (-not (Test-Path -LiteralPath $setup)) { throw 'Build with scripts/package.ps1 -SmokeInstaller first.' }
if ($PreviousSmokeInstaller) {
    $PreviousSmokeInstaller = [IO.Path]::GetFullPath($PreviousSmokeInstaller)
    $allowedInstallers = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts\installer')) + [IO.Path]::DirectorySeparatorChar
    if (-not $PreviousSmokeInstaller.StartsWith($allowedInstallers, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path -Leaf $PreviousSmokeInstaller) -notlike 'wText-*-win-x64-setup-smoke.exe' -or
        -not (Test-Path -LiteralPath $PreviousSmokeInstaller)) { throw 'Upgrade tests require a previous smoke installer in artifacts/installer.' }
}
$testKey = 'HKCU:\Software\wText-install-smoke'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\wText-install-smoke'
$shortcutDirectory = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\wText-install-smoke'
if ((Test-Path $testKey) -or (Test-Path $uninstallKey) -or (Test-Path -LiteralPath $shortcutDirectory)) {
    throw 'A previous installation test exists. Review and uninstall it before running another test.'
}

$sandbox = Join-Path $RepoRoot ('artifacts\install-smoke\' + [Guid]::NewGuid().ToString('N'))
$target = Join-Path $sandbox 'wText test'
New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
$occupied = Join-Path $sandbox 'occupied folder'
New-Item -ItemType Directory -Path $occupied | Out-Null
$existingFile = Join-Path $occupied 'keep.txt'
[System.IO.File]::WriteAllText($existingFile, 'Existing unrelated data.')
$process = Start-Process -FilePath $setup -ArgumentList "/S /D=$occupied" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Non-empty directory check timed out.' }
if ($process.ExitCode -eq 0 -or (Get-Content -LiteralPath $existingFile -Raw) -ne 'Existing unrelated data.') { throw 'Installer did not protect a non-empty folder.' }
if (Test-Path -LiteralPath (Join-Path $occupied 'wText.exe')) { throw 'Installer wrote into an unrelated folder.' }
Write-Host "Testing installation in: $target"
$sentinel = Join-Path $target 'user-note.txt'
if ($PreviousSmokeInstaller) {
    $process = Start-Process -FilePath $PreviousSmokeInstaller -ArgumentList "/S /D=$target" -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Previous version installation timed out.' }
    if ($process.ExitCode -ne 0) { throw "Previous installer failed: $($process.ExitCode)" }
    if ((Get-Content -LiteralPath (Join-Path $target '.wtext-install') -Raw) -ne 'wText-install-smoke') { throw 'Expected an isolated smoke installation.' }
    [IO.File]::WriteAllText($sentinel, 'Preserve this user file.')
}
$process = Start-Process -FilePath $setup -ArgumentList "/S /D=$target" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Installer timed out.' }
if ($process.ExitCode -ne 0) { throw "Installer failed: $($process.ExitCode)" }
foreach ($requiredFile in @('wText.exe', 'Uninstall.exe', 'LICENSE', 'THIRD-PARTY-NOTICES.md', '.wtext-install', 'licenses\DiffPlex.txt', 'licenses\NotoSansKR-OFL.txt', 'licenses\JetBrainsMono-OFL.txt', 'Assets\Fonts\JetBrainsMono-Regular.ttf', 'Assets\Fonts\NotoSansKR-Regular.otf')) {
    if (-not (Test-Path -LiteralPath (Join-Path $target $requiredFile))) { throw "Missing installed file: $requiredFile" }
}
if ((Get-ItemProperty -LiteralPath $testKey).InstallDir -ne $target) { throw 'The install directory was not registered.' }
if ((Get-ItemProperty -LiteralPath $uninstallKey).DisplayVersion -ne $version) { throw 'The uninstall entry has the wrong version.' }
if ((Get-ItemProperty -LiteralPath $uninstallKey).Publisher -ne 'Hyunwook Park') { throw 'The installer publisher is incorrect.' }
if ((Get-ItemProperty -LiteralPath $uninstallKey).URLInfoAbout -ne 'https://github.com/parkhw328/wook-text') { throw 'The installer project URL is incorrect.' }
if (-not (Test-Path -LiteralPath (Join-Path $shortcutDirectory 'wText.lnk'))) { throw 'Start menu shortcut missing.' }
if ($PreviousSmokeInstaller -and (Get-Content -LiteralPath $sentinel -Raw) -ne 'Preserve this user file.') { throw 'Upgrade changed a user file.' }

# Exercise the actual installed binaries and bundled fonts in a path with spaces.
# The harness injects isolated settings; it never changes the user's preferences.
$harness = Join-Path $RepoRoot 'tests\WookText.App.SmokeTests\bin\Release\net10.0-windows\win-x64\WookText.App.SmokeTests.dll'
if (-not (Test-Path -LiteralPath $harness)) { throw 'Run scripts/build.ps1 before installer verification.' }
$installedHarness = Join-Path $target 'WookText.App.SmokeTests.dll'
Copy-Item -LiteralPath $harness -Destination $installedHarness
try {
    Invoke-Dotnet exec --runtimeconfig (Join-Path $target 'wText.runtimeconfig.json') --depsfile (Join-Path $target 'wText.deps.json') $installedHarness (Join-Path $sandbox 'ui')
} finally { Remove-Item -LiteralPath $installedHarness -Force }

# A user's file must survive a reinstall and uninstall.
[System.IO.File]::WriteAllText($sentinel, 'Preserve this user file.')
$process = Start-Process -FilePath $setup -ArgumentList "/S /D=$target" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Reinstallation timed out.' }
if ($process.ExitCode -ne 0 -or (Get-Content -LiteralPath $sentinel -Raw) -ne 'Preserve this user file.') { throw 'Reinstallation failed or changed a user file.' }

# Copy the generated uninstaller outside the payload. _?= prevents NSIS from
# spawning a detached temporary uninstaller, so this test can wait for completion.
$uninstaller = Join-Path $sandbox 'Uninstall-test.exe'
Copy-Item -LiteralPath (Join-Path $target 'Uninstall.exe') -Destination $uninstaller
$marker = Join-Path $target '.wtext-install'
[System.IO.File]::WriteAllText($marker, 'not-wText')
$process = Start-Process -FilePath $uninstaller -ArgumentList "/S _?=$target" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Uninstall marker check timed out.' }
if ($process.ExitCode -eq 0 -or -not (Test-Path -LiteralPath (Join-Path $target 'wText.exe'))) { throw 'Uninstaller did not protect an unrecognized installation.' }
[System.IO.File]::WriteAllText($marker, 'wText-install-smoke')
$process = Start-Process -FilePath $uninstaller -ArgumentList "/S _?=$target" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Uninstaller timed out.' }
if ($process.ExitCode -ne 0) { throw "Uninstaller failed: $($process.ExitCode)" }
if (Test-Path -LiteralPath (Join-Path $target 'wText.exe')) { throw 'Application executable remains after uninstall.' }
if ((Test-Path $testKey) -or (Test-Path $uninstallKey) -or (Test-Path -LiteralPath $shortcutDirectory)) { throw 'Installer registry or shortcut entries remain.' }
if ((Get-Content -LiteralPath $sentinel -Raw) -ne 'Preserve this user file.') { throw 'Uninstaller changed a user file.' }
$remaining = @(Get-ChildItem -LiteralPath $target -Recurse -File)
if ($remaining.Count -ne 1 -or $remaining[0].FullName -ne $sentinel) { throw 'Unexpected installed files remain.' }
Write-Host 'PASS: per-user installation, registration, shortcut, reinstall, uninstall, directory/marker protection, and preservation of user files.'
if ($PreviousSmokeInstaller) { Write-Host "PASS: upgrade from $PreviousSmokeInstaller to $version." }
