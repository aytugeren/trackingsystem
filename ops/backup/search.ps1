Param(
  [string]$Query = "",
  [string]$Date = "",
  [string]$From = "",
  [string]$To = ""
)

$ErrorActionPreference = 'Stop'

$root = Join-Path $PSScriptRoot "..\backups" | Resolve-Path | Select-Object -ExpandProperty Path
$index = Join-Path $root "index.json"
if (!(Test-Path $index)) { Write-Error "Index not found: $index" }

$items = Get-Content $index -Raw | ConvertFrom-Json

if ($Date) {
  $d = Get-Date $Date
  $fromDt = Get-Date ($d.ToString('yyyy-MM-dd 00:00:00'))
  $toDt = $fromDt.AddDays(1)
  $items = $items | Where-Object { (Get-Date $_.CreatedAt) -ge $fromDt -and (Get-Date $_.CreatedAt) -lt $toDt }
}
if ($From) {
  $fromDt = Get-Date $From
  $items = $items | Where-Object { (Get-Date $_.CreatedAt) -ge $fromDt }
}
if ($To) {
  $toDt = Get-Date $To
  $items = $items | Where-Object { (Get-Date $_.CreatedAt) -le $toDt }
}
if ($Query) {
  $items = $items | Where-Object { $_.FilePath -like "*${Query}*" -or $_.Status -like "*${Query}*" }
}

$items | Sort-Object CreatedAt | Select-Object CreatedAt,Type,Status,SizeBytes,FilePath,ArchivePath | Format-Table -AutoSize

