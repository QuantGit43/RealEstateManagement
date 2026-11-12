$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = $utf8
Set-Location (Split-Path $PSScriptRoot -Parent)

docker compose cp redis/demo.redis redis:/tmp/demo.redis | Out-Null
$output = docker compose exec -T redis sh -c 'redis-cli -n 1 --raw < /tmp/demo.redis'
[IO.File]::WriteAllLines("$PSScriptRoot\demo-output.txt", $output, $utf8)
$output
