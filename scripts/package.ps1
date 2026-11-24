[CmdletBinding()]
param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $stage = Join-Path $root ("artifacts/staging/" + [guid]::NewGuid().ToString('N'))
    & $Dotnet publish src/PromptEnhancer.Windows -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -p:RestoreConfigFile=NuGet.Config -o $stage
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
    Copy-Item -LiteralPath 'LICENSE','README.md','settings.example.json' -Destination $stage
    $zip = Join-Path $root 'artifacts/PromptEnhancer-win-x64.zip'
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash.ToLowerInvariant()
    "$hash  PromptEnhancer-win-x64.zip" | Set-Content -LiteralPath "$zip.sha256" -Encoding ascii
    Write-Output "Package: $zip"
    Write-Output "SHA256: $hash"
} finally { Pop-Location }

