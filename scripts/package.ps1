param([string]$DotnetRoot = '')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = (Get-Content -LiteralPath (Join-Path $projectRoot 'package.json') -Raw | ConvertFrom-Json).version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid package version.' }
$artifactRoot = Join-Path $projectRoot 'artifacts'
$appRoot = Join-Path $artifactRoot 'sentinel-app'
if (!(Test-Path (Join-Path $appRoot 'Sentinel.exe'))) { throw 'Publish the Windows app first.' }
if (!$DotnetRoot) { $DotnetRoot = Split-Path (Get-Command dotnet -ErrorAction Stop).Source }
foreach ($notice in @('LICENSE.txt','ThirdPartyNotices.txt')) {
    $source = Join-Path $DotnetRoot $notice
    if (!(Test-Path $source)) { throw "Missing .NET notice: $source" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $appRoot "DOTNET-$notice")
}
foreach ($name in @('LICENSE','README.md','VALIDATION.md','THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $appRoot
}
$desktopLicense = Get-ChildItem (Join-Path $projectRoot '.tools/nuget/microsoft.windowsdesktop.app.runtime.win-x64') -Filter LICENSE -Recurse | Select-Object -First 1
if (!$desktopLicense) { throw 'Missing WPF runtime license; restore with NuGet.Config first.' }
Copy-Item -LiteralPath $desktopLicense.FullName -Destination (Join-Path $appRoot 'WPF-LICENSE.txt')
New-Item -ItemType Directory -Path (Join-Path $appRoot 'docs') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') -File | Copy-Item -Destination (Join-Path $appRoot 'docs')

function Write-Package([string]$name, [string]$basePath, [System.IO.FileInfo[]]$files) {
    $target = [IO.Path]::GetFullPath((Join-Path $artifactRoot $name))
    if (![string]::Equals([IO.Path]::GetDirectoryName($target), $artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid package target' }
    $stream = [IO.File]::Open($target, [IO.FileMode]::Create, [IO.FileAccess]::Write)
    try {
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($file in $files) {
                $relative = [IO.Path]::GetRelativePath($basePath, $file.FullName).Replace('\','/')
                if ($relative.StartsWith('../')) { throw 'File outside package root' }
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
        } finally { $zip.Dispose() }
    } finally { $stream.Dispose() }
    Get-Item -LiteralPath $target | Select-Object Name,Length
}
Write-Package "Sentinel-$version-win-x64.zip" $appRoot @(Get-ChildItem -LiteralPath $appRoot -File -Recurse)
$sourceFiles = @(Get-ChildItem -LiteralPath $projectRoot -File | Where-Object { $_.Name -in @('package.json','pnpm-lock.yaml','index.html','tsconfig.json','vite.config.ts','wrangler.jsonc','NuGet.Config','.gitignore','.gitattributes','LICENSE','README.md','CONTRIBUTING.md','THIRD_PARTY_NOTICES.md','VALIDATION.md') })
foreach ($directory in @('public','desktop','web','worker','shared','rules','samples','tests','scripts','installer','migrations','docs','.github')) {
    $sourceFiles += Get-ChildItem -LiteralPath (Join-Path $projectRoot $directory) -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|private)[\\/]' }
}
Write-Package "Sentinel-$version-source.zip" $projectRoot $sourceFiles
