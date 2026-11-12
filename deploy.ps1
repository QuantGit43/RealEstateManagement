param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$User,
    [string]$Password,
    [switch]$Test,
    [switch]$NoSeed
)
$ErrorActionPreference = 'Stop'

if ($Server -like '(localdb)*') { sqllocaldb start MSSQLLocalDB | Out-Null }
$auth = @(if ($User) { '-U', $User, '-P', $Password } else { '-E' })

$scripts = Get-ChildItem "$PSScriptRoot\sql\0*.sql" | Sort-Object Name
if ($NoSeed) { $scripts = $scripts | Where-Object Name -ne '07_seed.sql' }
if ($Test) { $scripts += Get-Item "$PSScriptRoot\sql\99_tests.sql" }

foreach ($s in $scripts) {
    Write-Host "== $($s.Name)"
    sqlcmd -S $Server @auth -C -I -b -f 65001 -i $s.FullName
    if ($LASTEXITCODE -ne 0) { throw "Помилка у $($s.Name)" }
}
Write-Host 'Готово.'
