param([string]$Compiler = '')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$Compiler) { $Compiler = Join-Path $projectRoot '.tools/inno/ISCC.exe' }
if (!(Test-Path -LiteralPath $Compiler)) { throw 'Run scripts/install-inno.ps1 or supply -Compiler with the Inno Setup 6.7.3 ISCC.exe path.' }
$appRoot = Join-Path $projectRoot 'artifacts/sentinel-app'
foreach ($required in @('Sentinel.exe','Sentinel.runtimeconfig.json','rules/catalog.json','DOTNET-LICENSE.txt','WPF-LICENSE.txt','docs/GETTING_STARTED.md')) {
    if (!(Test-Path -LiteralPath (Join-Path $appRoot $required))) { throw "Package input missing: $required. Publish the app and run package.ps1 first." }
}
$version = (Get-Content -LiteralPath (Join-Path $projectRoot 'package.json') -Raw | ConvertFrom-Json).version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Unsupported installer version.' }
$license = Join-Path (Split-Path $Compiler) 'license.txt'
if (!(Test-Path -LiteralPath $license)) { throw 'Inno Setup license missing.' }
Copy-Item -LiteralPath $license -Destination (Join-Path $appRoot 'INNO-SETUP-LICENSE.txt')
& $Compiler '/Qp' "/DAppVersion=$version" (Join-Path $projectRoot 'installer/Sentinel.iss')
if ($LASTEXITCODE -ne 0) { throw "Installer compiler failed: $LASTEXITCODE" }
$installer = Join-Path $projectRoot "artifacts/Sentinel-$version-Setup-x64.exe"
if (!(Test-Path -LiteralPath $installer)) { throw 'Installer output missing.' }
Get-Item -LiteralPath $installer | Select-Object Name,Length
((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($installer)) | Set-Content -LiteralPath ($installer+'.sha256') -Encoding ascii
