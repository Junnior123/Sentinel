$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = (Get-Content (Join-Path $projectRoot 'package.json') -Raw | ConvertFrom-Json).version
$setup = Join-Path $projectRoot "artifacts/Sentinel-$version-Setup-x64.exe"
$registration = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{AF20DE2F-1545-4B11-9C1D-C8FC25699041}_is1'
if (Test-Path -LiteralPath $registration) { throw 'Sentinel is already installed. Use a separate test account to avoid modifying that installation.' }
$testRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot ('artifacts/installer-test-'+[Guid]::NewGuid().ToString('N'))))
$expectedParent = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
if ([IO.Path]::GetDirectoryName($testRoot) -ne $expectedParent) { throw 'Test path outside artifact directory.' }
function Run-Setup([string]$file,[string[]]$arguments) {
    $process = Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Test installer timed out.' }
    return $process.ExitCode
}
$arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/LANG=korean',('/DIR="'+$testRoot+'"'))
$app = $null
try {
    if ((Run-Setup $setup $arguments) -ne 0) { throw 'Fresh installation failed.' }
    foreach ($file in @('Sentinel.exe','Sentinel.runtimeconfig.json','rules/catalog.json','INNO-SETUP-LICENSE.txt','unins000.exe')) {
        if (!(Test-Path -LiteralPath (Join-Path $testRoot $file))) { throw "Installed file missing: $file" }
    }
    if (!(Test-Path -LiteralPath $registration)) { throw 'Windows uninstall entry missing.' }
    $shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Sentinel/Sentinel.lnk'
    if (!(Test-Path -LiteralPath $shortcut)) { throw 'Start menu shortcut missing.' }
    $config = Join-Path $testRoot 'Sentinel.service.json'
    Set-Content -LiteralPath $config -Value '{"origin":"https://example.org"}' -Encoding utf8
    Set-Content -LiteralPath (Join-Path $testRoot 'my-report.json') -Value '{"test":"user owned"}' -Encoding utf8
    $before = (Get-FileHash -LiteralPath $config).Hash
    if ((Run-Setup $setup $arguments) -ne 0) { throw 'Reinstallation failed.' }
    if ((Get-FileHash -LiteralPath $config).Hash -ne $before) { throw 'Existing server configuration was overwritten.' }
    $app = Start-Process -FilePath (Join-Path $testRoot 'Sentinel.exe') -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    if ($app.HasExited) { throw 'Installed app exited unexpectedly.' }
    $guard = [Threading.Mutex]::OpenExisting('Sentinel.Desktop.Running')
    $guard.Dispose()
    if ((Run-Setup $setup $arguments) -eq 0) { throw 'Installer allowed replacement while the app was running.' }
    if (!$app.CloseMainWindow() -or !$app.WaitForExit(10000)) { throw 'Installed app did not close normally.' }
    $app = $null
    $uninstaller = [IO.Path]::GetFullPath((Join-Path $testRoot 'unins000.exe'))
    if ([IO.Path]::GetDirectoryName($uninstaller) -ne $testRoot) { throw 'Unsafe uninstaller path.' }
    if ((Run-Setup $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')) -ne 0) { throw 'Uninstallation failed.' }
    for ($attempt=0;$attempt -lt 30 -and (Test-Path -LiteralPath (Join-Path $testRoot 'Sentinel.exe'));$attempt++) { Start-Sleep -Milliseconds 200 }
    if ((Test-Path -LiteralPath (Join-Path $testRoot 'Sentinel.exe')) -or (Test-Path -LiteralPath $registration)) { throw 'Installed application or registration remains.' }
    if (!(Test-Path -LiteralPath $config) -or !(Test-Path -LiteralPath (Join-Path $testRoot 'my-report.json'))) { throw 'Uninstall removed user-owned data.' }
    if (Test-Path -LiteralPath $shortcut) { throw 'Start menu shortcut remains after uninstall.' }
    'PASS install, shortcut, registry, reinstall, configuration preservation, app launch, running-app guard, uninstall, user-data preservation'
} finally {
    if ($null -ne $app -and !$app.HasExited) { $null=$app.CloseMainWindow() }
}
