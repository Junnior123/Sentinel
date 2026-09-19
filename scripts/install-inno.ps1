$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$toolRoot = Join-Path $projectRoot '.tools/inno'
$download = Join-Path $projectRoot '.tools/innosetup-6.7.3.exe'
New-Item -ItemType Directory -Path (Split-Path $download) -Force | Out-Null
if (!(Test-Path -LiteralPath $download)) {
    Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $download
}
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732') {
    throw 'Inno Setup download hash mismatch. Do not execute this file.'
}
$process = Start-Process -FilePath $download -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS','/TASKS=""',('/DIR="'+$toolRoot+'"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Inno Setup installation failed: $($process.ExitCode)" }
if (!(Test-Path -LiteralPath (Join-Path $toolRoot 'ISCC.exe'))) { throw 'Inno Setup compiler missing.' }
Write-Output (Join-Path $toolRoot 'ISCC.exe')
