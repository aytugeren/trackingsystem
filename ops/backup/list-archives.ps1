$ErrorActionPreference = 'Stop'

$root = Join-Path $PSScriptRoot "..\backups" | Resolve-Path | Select-Object -ExpandProperty Path
$index = Join-Path $root "index.json"
if (!(Test-Path $index)) { Write-Error "Index not found: $index" }

$items = Get-Content $index -Raw | ConvertFrom-Json
$archives = $items | Where-Object { $_.Archived -and $_.ArchivePath } |
  Group-Object ArchivePath |
  ForEach-Object {
    [PSCustomObject]@{
      ArchivePath = $_.Name
      Count       = $_.Count
      Dates       = ($_.Group | ForEach-Object { (Get-Date $_.CreatedAt).ToString('yyyy-MM-dd') } | Sort-Object -Unique) -join ', '
    }
  } | Sort-Object ArchivePath

$archives | Format-Table -AutoSize

