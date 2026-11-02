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
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'wText-install-smoke.lnk'
if ((Test-Path $testKey) -or (Test-Path $uninstallKey) -or (Test-Path -LiteralPath $shortcutDirectory) -or (Test-Path -LiteralPath $desktopShortcut)) {
    throw 'A previous installation test exists. Review and uninstall it before running another test.'
}

$sandbox = Join-Path $RepoRoot ('artifacts\install-smoke\' + [Guid]::NewGuid().ToString('N'))
$target = Join-Path $sandbox 'wText test'
$defaultTarget = Join-Path $env:LOCALAPPDATA 'Programs\wText-install-smoke'
if (Test-Path -LiteralPath $defaultTarget) { throw 'The default smoke installation folder already exists. Review it before testing orphan recovery.' }
function Assert-DesktopShortcut([bool]$Expected) {
    if ((Test-Path -LiteralPath $desktopShortcut) -ne $Expected) { throw "Desktop shortcut presence does not match $Expected." }
    if ((Get-ItemProperty -LiteralPath $testKey).DesktopShortcut -ne [int]$Expected) { throw 'Desktop shortcut preference was not saved.' }
    if ($Expected) {
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($desktopShortcut)
        try {
            if ($shortcut.TargetPath -ne (Join-Path $target 'wText.exe')) { throw 'Desktop shortcut points to the wrong executable.' }
            if ($shortcut.WorkingDirectory -ne $target) { throw 'Desktop shortcut has the wrong working directory.' }
            if ($shortcut.IconLocation -ne ((Join-Path $target 'wText.exe') + ',0')) { throw 'Desktop shortcut does not use the wText icon.' }
        } finally {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut)
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
        }
    }
}
function Install-ShortcutOption([string]$Option, [bool]$Expected) {
    $process = Start-Process -FilePath $setup -ArgumentList "/S $Option /D=$target" -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(180000)) { $process.Kill(); throw 'Shortcut option installation timed out.' }
    if ($process.ExitCode -ne 0) { throw "Shortcut option installation failed: $($process.ExitCode)" }
    Assert-DesktopShortcut $Expected
}
New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
$occupied = Join-Path $sandbox 'occupied folder'
New-Item -ItemType Directory -Path $occupied | Out-Null
$existingFile = Join-Path $occupied 'keep.txt'
[System.IO.File]::WriteAllText($existingFile, 'Existing unrelated data.')
$process = Start-Process -FilePath $setup -ArgumentList "/S /D=$occupied" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(180000)) { $process.Kill(); throw 'Non-empty directory check timed out.' }
if ($process.ExitCode -eq 0 -or (Get-Content -LiteralPath $existingFile -Raw) -ne 'Existing unrelated data.') { throw 'Installer did not protect a non-empty folder.' }
if (Test-Path -LiteralPath (Join-Path $occupied 'wText.exe')) { throw 'Installer wrote into an unrelated folder.' }
Write-Host "Testing installation in: $target"
$sentinel = Join-Path $target 'user-note.txt'
if ($PreviousSmokeInstaller) {
    $process = Start-Process -FilePath $PreviousSmokeInstaller -ArgumentList "/S /D=$target" -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(180000)) { $process.Kill(); throw 'Previous version installation timed out.' }
    if ($process.ExitCode -ne 0) { throw "Previous installer failed: $($process.ExitCode)" }
    if ((Get-Content -LiteralPath (Join-Path $target '.wtext-install') -Raw) -ne 'wText-install-smoke') { throw 'Expected an isolated smoke installation.' }
    [IO.File]::WriteAllText($sentinel, 'Preserve this user file.')
}
$process = Start-Process -FilePath $setup -ArgumentList "/S /D=$target" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(180000)) { $process.Kill(); throw 'Installer timed out.' }
if ($process.ExitCode -ne 0) { throw "Installer failed: $($process.ExitCode)" }
foreach ($requiredFile in @('wText.exe', 'Uninstall.exe', 'LICENSE', 'THIRD-PARTY-NOTICES.md', '.wtext-install', 'licenses\DiffPlex.txt', 'licenses\NotoSansKR-OFL.txt', 'licenses\JetBrainsMono-OFL.txt', 'Assets\Fonts\JetBrainsMono-Regular.ttf', 'Assets\Fonts\NotoSansKR-Regular.otf', 'Formatting\node.exe', 'Formatting\format.cjs', 'Formatting\prettier\standalone.js', 'licenses\Prettier.txt', 'licenses\Prettier-THIRD-PARTY-NOTICES.md', 'licenses\Nodejs.txt', 'pdfium.dll', 'WebView2Runtime\msedgewebview2.exe', 'WebView2Runtime\msedge.dll', 'licenses\PDFium-NOTICES.txt', 'licenses\WebView2-Runtime.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $target $requiredFile))) { throw "Missing installed file: $requiredFile" }
}
if ((Get-ItemProperty -LiteralPath $testKey).InstallDir -ne $target) { throw 'The install directory was not registered.' }
if ((Get-ItemProperty -LiteralPath $uninstallKey).DisplayVersion -ne $version) { throw 'The uninstall entry has the wrong version.' }
if ((Get-ItemProperty -LiteralPath $uninstallKey).Publisher -ne 'Hyunwook Park') { throw 'The installer publisher is incorrect.' }
if ((Get-ItemProperty -LiteralPath $uninstallKey).URLInfoAbout -ne 'https://github.com/parkhw328/wook-text') { throw 'The installer project URL is incorrect.' }
if (-not (Test-Path -LiteralPath (Join-Path $shortcutDirectory 'wText.lnk'))) { throw 'Start menu shortcut missing.' }
Assert-DesktopShortcut $true
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

# Old 1.6.x packages contained debug files; an upgrade must remove only those owned leftovers.
$obsoleteFile = Join-Path $target 'wText.pdb'
[IO.File]::WriteAllText($obsoleteFile, 'Old packaged debug symbols.')
# Observe the largest unchanged runtime file during reinstall; timestamps alone
# cannot prove that an installer skipped rewriting a file.
$watcher = [IO.FileSystemWatcher]::new((Join-Path $target 'WebView2Runtime'), 'msedge.dll')
$watcher.NotifyFilter = [IO.NotifyFilters]::LastWrite
$watchId = 'wText-reinstall-' + [Guid]::NewGuid().ToString('N')
$subscription = Register-ObjectEvent -InputObject $watcher -EventName Changed -SourceIdentifier $watchId
$watcher.EnableRaisingEvents = $true
$elapsed = [Diagnostics.Stopwatch]::StartNew()
# A user's file must survive a reinstall and uninstall.
[System.IO.File]::WriteAllText($sentinel, 'Preserve this user file.')
$process = Start-Process -FilePath $setup -ArgumentList "/S /D=$target" -WindowStyle Hidden -PassThru
try {
    if (-not $process.WaitForExit(180000)) { $process.Kill(); throw 'Reinstallation timed out.' }
    Start-Sleep -Milliseconds 250
    if (@(Get-Event -SourceIdentifier $watchId -ErrorAction SilentlyContinue).Count -ne 0) { throw 'Reinstallation rewrote the unchanged WebView2 runtime.' }
    Write-Host ('Reinstall duration (this PC): {0:0.00} seconds; unchanged WebView2 DLL was not rewritten.' -f $elapsed.Elapsed.TotalSeconds)
} finally {
    Unregister-Event -SourceIdentifier $watchId
    Get-Event -SourceIdentifier $watchId -ErrorAction SilentlyContinue | Remove-Event
    $watcher.Dispose()
}
if ($process.ExitCode -ne 0 -or (Get-Content -LiteralPath $sentinel -Raw) -ne 'Preserve this user file.') { throw 'Reinstallation failed or changed a user file.' }
if (Test-Path -LiteralPath $obsoleteFile) { throw 'An obsolete owned file survived the upgrade.' }
Assert-DesktopShortcut $true
Install-ShortcutOption '/DesktopShortcut=0' $false
Install-ShortcutOption '' $false
Install-ShortcutOption '/DesktopShortcut=1' $true
Install-ShortcutOption '' $true
if (-not (Test-Path -LiteralPath (Join-Path $shortcutDirectory 'wText.lnk'))) { throw 'Desktop preference removed the Start menu shortcut.' }
Write-Host 'PASS: desktop shortcut target/icon, opt-out removal, opt-in creation, and remembered choices across reinstall.'

# Copy the generated uninstaller outside the payload. _?= prevents NSIS from
# spawning a detached temporary uninstaller, so this test can wait for completion.
$uninstaller = Join-Path $sandbox 'Uninstall-test.exe'
Copy-Item -LiteralPath (Join-Path $target 'Uninstall.exe') -Destination $uninstaller
# Locked dependencies must prevent ANY partial removal and preserve registration.
$marker = Join-Path $target '.wtext-install'
$payloadBefore = @(Get-ChildItem -LiteralPath $target -Recurse -File).Count
$locked = [IO.File]::Open((Join-Path $target 'WookText.Core.dll'), 'Open', 'Read', 'Read')
try {
    foreach ($action in @(@($setup, "/S /D=$target"), @($uninstaller, "/S _?=$target"))) {
        $process = Start-Process -FilePath $action[0] -ArgumentList $action[1] -WindowStyle Hidden -PassThru
        if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Locked-file preflight did not fail promptly.' }
        if ($process.ExitCode -ne 3) { throw "Locked-file preflight returned $($process.ExitCode), expected 3." }
        if (@(Get-ChildItem -LiteralPath $target -Recurse -File).Count -ne $payloadBefore -or -not (Test-Path $uninstallKey) -or -not (Test-Path -LiteralPath $marker)) { throw 'A locked dependency left a partially removed installation.' }
    }
} finally { $locked.Dispose() }
$running = [Threading.Mutex]::new($false, 'Local\wText.Running')
try {
    $process = Start-Process -FilePath $setup -ArgumentList "/S /D=$target" -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Running-app preflight timed out.' }
    if ($process.ExitCode -ne 3) { throw 'Installer did not reject an active editor.' }
} finally { $running.Dispose() }
# Keep one genuinely owned DLL for the old markerless-remnant recovery case below.
$orphanFile = Join-Path $sandbox 'WookText.Core.dll'
Copy-Item -LiteralPath (Join-Path $target 'WookText.Core.dll') -Destination $orphanFile

[System.IO.File]::WriteAllText($marker, 'not-wText')
$process = Start-Process -FilePath $uninstaller -ArgumentList "/S _?=$target" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(180000)) { $process.Kill(); throw 'Uninstall marker check timed out.' }
if ($process.ExitCode -eq 0 -or -not (Test-Path -LiteralPath (Join-Path $target 'wText.exe'))) { throw 'Uninstaller did not protect an unrecognized installation.' }
[System.IO.File]::WriteAllText($marker, 'wText-install-smoke')
$process = Start-Process -FilePath $uninstaller -ArgumentList "/S _?=$target" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(180000)) { $process.Kill(); throw 'Uninstaller timed out.' }
if ($process.ExitCode -ne 0) { throw "Uninstaller failed: $($process.ExitCode)" }
if (Test-Path -LiteralPath (Join-Path $target 'wText.exe')) { throw 'Application executable remains after uninstall.' }
if ((Test-Path $testKey) -or (Test-Path $uninstallKey) -or (Test-Path -LiteralPath $shortcutDirectory) -or (Test-Path -LiteralPath $desktopShortcut)) { throw 'Installer registry or shortcut entries remain.' }
if ((Get-Content -LiteralPath $sentinel -Raw) -ne 'Preserve this user file.') { throw 'Uninstaller changed a user file.' }
$remaining = @(Get-ChildItem -LiteralPath $target -Recurse -File)
$receipt = Join-Path $target '.wtext-retained'
if ($remaining.Count -ne 2 -or -not (Test-Path -LiteralPath $receipt) -or (Get-Content -LiteralPath $receipt -Raw) -ne 'wText-install-smoke') { throw 'Unexpected installed files remain; only the user file and ownership receipt are allowed.' }
# Reinstall over deliberately retained user files, then remove without losing them.
Install-ShortcutOption '/DesktopShortcut=0' $false
$process = Start-Process -FilePath $uninstaller -ArgumentList "/S _?=$target" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(180000)) { $process.Kill(); throw 'Retained-folder uninstall timed out.' }
if ($process.ExitCode -ne 0 -or (Get-Content -LiteralPath $sentinel -Raw) -ne 'Preserve this user file.') { throw 'Reinstallation over user-retained files failed.' }
Remove-Item -LiteralPath $sentinel, $receipt
Remove-Item -LiteralPath $target
# Simulate the old uninstaller having removed its marker but left a DLL in Programs.
$target = $defaultTarget
New-Item -ItemType Directory -Path $target | Out-Null
Copy-Item -LiteralPath $orphanFile -Destination (Join-Path $target 'WookText.Core.dll')
$unknown = Join-Path $target 'unrelated-user-file.txt'
[IO.File]::WriteAllText($unknown, 'Preserve unknown data.')
$process = Start-Process -FilePath $setup -ArgumentList "/S /D=$target" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Unknown orphan directory check timed out.' }
if ($process.ExitCode -eq 0 -or (Get-Content -LiteralPath $unknown -Raw) -ne 'Preserve unknown data.') { throw 'Orphan recovery accepted unrelated user data without an ownership marker.' }
Remove-Item -LiteralPath $unknown
Install-ShortcutOption '/DesktopShortcut=0' $false
$process = Start-Process -FilePath $uninstaller -ArgumentList "/S _?=$target" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(180000)) { $process.Kill(); throw 'Recovered installation removal timed out.' }
if ($process.ExitCode -ne 0 -or (Test-Path -LiteralPath $target)) { throw 'Recovered Programs installation was not fully removed.' }
Write-Host 'PASS: locked DLL/app preflight, retained user files, and recovery of known old Programs remnants.'

$target = Join-Path $sandbox 'no desktop shortcut'
Install-ShortcutOption '/DesktopShortcut=0' $false
$process = Start-Process -FilePath $uninstaller -ArgumentList "/S _?=$target" -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(180000)) { $process.Kill(); throw 'Opt-out uninstall timed out.' }
if ($process.ExitCode -ne 0 -or (Test-Path -LiteralPath $target)) { throw 'Fresh opt-out installation was not fully removed.' }
if ((Test-Path $testKey) -or (Test-Path $uninstallKey) -or (Test-Path -LiteralPath $shortcutDirectory) -or (Test-Path -LiteralPath $desktopShortcut)) { throw 'Opt-out installation left registration or shortcuts.' }
Write-Host 'PASS: fresh installation without a desktop shortcut and removal of both shortcut types on uninstall.'
Write-Host 'PASS: per-user installation, registration, shortcut, reinstall, uninstall, directory/marker protection, and preservation of user files.'
if ($PreviousSmokeInstaller) { Write-Host "PASS: upgrade from $PreviousSmokeInstaller to $version." }
