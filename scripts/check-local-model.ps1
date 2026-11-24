[CmdletBinding()]
param([Parameter(Mandatory)][string]$Runner, [string]$Model = 'qwen2.5:0.5b', [string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
# Test acquisition is explicit. Production never starts a download.
$env:OLLAMA_MODELS = Join-Path $root '.tools/smoke-models'
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$env:OLLAMA_HOST = "127.0.0.1:$port"
$env:OLLAMA_NO_CLOUD = '1'
$server = Start-Process -FilePath $Runner -ArgumentList 'serve' -WindowStyle Hidden -PassThru
try {
    $ready = $false
    for ($attempt=0; $attempt -lt 45; $attempt++) {
        if ($server.HasExited) { throw 'Ollama test server stopped.' }
        try { $null = Invoke-RestMethod "http://127.0.0.1:$port/api/version" -TimeoutSec 2; $ready = $true; break }
        catch { Start-Sleep -Seconds 1 }
    }
    if (!$ready) { throw 'Ollama test runner unavailable.' }
    $response = Invoke-RestMethod "http://127.0.0.1:$port/api/pull" -Method Post -ContentType 'application/json' -Body (@{model=$Model;stream=$false}|ConvertTo-Json) -TimeoutSec 600
    if ($response.status -ne 'success') { throw 'Test model download failed.' }
} finally { if (!$server.HasExited) { Stop-Process -Id $server.Id; $server.WaitForExit() } }
Push-Location $root
try {
    & $Dotnet run --project tests/PromptEnhancer.Checks -c Release --no-build -- --ollama $Runner $Model
    if ($LASTEXITCODE -ne 0) { throw 'Model smoke test failed.' }
} finally {Pop-Location}

