param([int[]]$Scales = @(10000, 100000, 1000000))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$env:MSBUILDDISABLENODEREUSE = '1'

dotnet build "$PSScriptRoot\RealEstate.Benchmarks" -c Release | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Помилка збірки' }

foreach ($n in $Scales) {
    Write-Host "######## $n оголошень"
    & "$root\deploy.ps1" -Server 'localhost,14333' -User sa -Password 'RealEstate_Bench1' -NoSeed | Out-Null
    dotnet run --project "$PSScriptRoot\RealEstate.Benchmarks" -c Release --no-build -- $n
    if ($LASTEXITCODE -ne 0) { throw "Помилка тесту для $n" }
}
