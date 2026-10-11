$ErrorActionPreference = 'Stop'
$formatterRoot = Join-Path (Split-Path -Parent $PSScriptRoot) '.tools\formatter'
New-Item -ItemType Directory -Path $formatterRoot -Force | Out-Null
$nodePath = Join-Path $formatterRoot 'node.exe'
$nodeHash = 'cea6ac365f9bb9586dafd2084d996e092e7d3e7d07ab52d1f76bf53d1fca9bc4'
if (-not (Test-Path $nodePath) -or (Get-FileHash $nodePath -Algorithm SHA256).Hash -ne $nodeHash) {
    Invoke-WebRequest 'https://nodejs.org/dist/v26.10.0/win-x64/node.exe' -OutFile $nodePath -UseBasicParsing
    if ((Get-FileHash $nodePath -Algorithm SHA256).Hash -ne $nodeHash) { throw 'Node.js checksum mismatch.' }
}
$archive = Join-Path $formatterRoot 'prettier-3.9.6.tgz'
$expected = [Convert]::FromBase64String('OpN0zzVdiaiAhxpuuj5efpIS4sY9j7bY6uR5mnj5yPzGkdkjNKSJeUThPb60Jw29QuAZgA4o+/iB49kFiaBX6g==')
$expectedHex = [BitConverter]::ToString($expected).Replace('-', '')
if (-not (Test-Path $archive) -or (Get-FileHash $archive -Algorithm SHA512).Hash -ne $expectedHex) {
    Invoke-WebRequest 'https://registry.npmjs.org/prettier/-/prettier-3.9.6.tgz' -OutFile $archive -UseBasicParsing
    if ((Get-FileHash $archive -Algorithm SHA512).Hash -ne $expectedHex) { throw 'Prettier checksum mismatch.' }
}
& tar -xzf $archive -C $formatterRoot
if ($LASTEXITCODE -ne 0) { throw 'Could not extract Prettier.' }
Write-Host 'Verified formatter: Node.js 26.10.0 / Prettier 3.9.6.'
