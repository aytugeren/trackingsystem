Param(
  [string]$Env = "Development"
)

$ErrorActionPreference = 'Stop'

# Read connection string from appsettings.{Env}.json or appsettings.json
$apiDir = Join-Path $PSScriptRoot "..\..\backend\Api"
$appFile = Join-Path $apiDir "appsettings.$Env.json"
if (!(Test-Path $appFile)) { $appFile = Join-Path $apiDir "appsettings.json" }
$cfg = Get-Content $appFile -Raw | ConvertFrom-Json
$cs = $cfg.ConnectionStrings.DefaultConnection
if (-not $cs) { throw "Connection string not found in $appFile" }

$backupCfg = $cfg.Backup
if (-not $backupCfg) { $backupCfg = @{ BackupRoot = 'ops/backups'; PgDumpPath = 'pg_dump'; Compress = $true } }

$root = Resolve-Path (Join-Path $PSScriptRoot "..\backups") | Select-Object -ExpandProperty Path
$dayDir = Join-Path $root ("manual\" + (Get-Date).ToString('yyyy-MM-dd'))
New-Item -ItemType Directory -Force -Path $dayDir | Out-Null

# Parse connection string
function Get-ConnVal($name, $cs) {
  if ($cs -match "$name=([^;]+)") { return $Matches[1] } else { return '' }
}
$host = Get-ConnVal 'Host' $cs
$port = Get-ConnVal 'Port' $cs
$db   = Get-ConnVal 'Database' $cs
$user = Get-ConnVal 'Username' $cs
$pass = Get-ConnVal 'Password' $cs

$timestamp = (Get-Date).ToString('yyyyMMdd_HHmmss')
$compress = [bool]$backupCfg.Compress
if ($compress) {
  $outPath = Join-Path $dayDir ("db_backup_manual_" + $timestamp + ".sql.gz")
} else {
  $outPath = Join-Path $dayDir ("db_backup_manual_" + $timestamp + ".sql")
}

$env:PGPASSWORD = $pass
$pgDump = $backupCfg.PgDumpPath
$args = @('-h', $host, '-p', $port, '-U', $user, '-d', $db, '-F', 'p', '--encoding=UTF8')
$p = Start-Process -FilePath $pgDump -ArgumentList $args -NoNewWindow -PassThru -RedirectStandardOutput 'pipe' -RedirectStandardError 'pipe'

if ($compress) {
  $gz = [IO.Compression.GzipStream]::new([IO.File]::Create($outPath), [IO.Compression.CompressionLevel]::SmallestSize)
  $p.StandardOutput.BaseStream.CopyTo($gz)
  $gz.Dispose()
} else {
  $fs = [IO.File]::Create($outPath)
  $p.StandardOutput.BaseStream.CopyTo($fs)
  $fs.Dispose()
}
$err = $p.StandardError.ReadToEnd()
$p.WaitForExit()
if ($p.ExitCode -ne 0) {
  Remove-Item -Force $outPath -ErrorAction SilentlyContinue
  throw "pg_dump failed ($($p.ExitCode)): $err"
}

Write-Host "Backup created:" $outPath

# Update index
$index = Join-Path (Join-Path $PSScriptRoot "..\backups") "index.json"
if (!(Test-Path $index)) { Set-Content -Path $index -Value '[]' -Encoding UTF8 }
$arr = Get-Content $index -Raw | ConvertFrom-Json
if ($arr -eq $null) { $arr = @() }
$fi = Get-Item $outPath
$hash = (Get-FileHash -Algorithm SHA256 $outPath).Hash.ToLower()
$rec = [PSCustomObject]@{
  Id = [Guid]::NewGuid()
  CreatedAt = (Get-Date).ToString('o')
  Type = 'hourly'
  FilePath = $fi.FullName
  SizeBytes = [int64]$fi.Length
  ChecksumSha256 = $hash
  Status = 'success'
  Message = $null
  Archived = $false
  ArchivePath = $null
}
$arr = @($arr) + $rec
$json = $arr | ConvertTo-Json -Depth 5
Set-Content -Path $index -Value $json -Encoding UTF8
Write-Host "Index updated:" $index
