Param(
  [string]$DbUrl,
  [string]$Host,
  [int]$Port,
  [string]$Database,
  [string]$Username,
  [string]$Password,
  [string]$File,
  [switch]$UseDocker,
  [string]$ContainerName = "ktp-postgres"
)

$ErrorActionPreference = 'Stop'

function Read-DotEnv($path) {
  if (!(Test-Path $path)) { return @{} }
  $map = @{}
  Get-Content $path | ForEach-Object {
    $line = $_.Trim()
    if (-not $line -or $line.StartsWith('#')) { return }
    $idx = $line.IndexOf('=')
    if ($idx -lt 1) { return }
    $k = $line.Substring(0, $idx).Trim()
    $v = $line.Substring($idx+1).Trim()
    $map[$k] = $v
  }
  return $map
}

# Defaults
if (-not $File) { $File = Join-Path $PSScriptRoot "create-indexes.sql" }
if (!(Test-Path $File)) { throw "SQL file not found: $File" }

$envMap = Read-DotEnv (Join-Path $PSScriptRoot "..\..\ops\.env")
if (-not $envMap.Count) { $envMap = Read-DotEnv (Join-Path $PSScriptRoot "..\..\ops\.env.example") }

if (-not $Database -and $envMap.ContainsKey('POSTGRES_DB')) { $Database = $envMap['POSTGRES_DB'] }
if (-not $Username -and $envMap.ContainsKey('POSTGRES_USER')) { $Username = $envMap['POSTGRES_USER'] }
if (-not $Password -and $envMap.ContainsKey('POSTGRES_PASSWORD')) { $Password = $envMap['POSTGRES_PASSWORD'] }
if (-not $Port -and $envMap.ContainsKey('POSTGRES_PORT')) { [int]$Port = $envMap['POSTGRES_PORT'] }
if (-not $Port) { $Port = 5432 }

if ($UseDocker) {
  if (-not $Username -or -not $Database -or -not $Password) {
    throw "When using -UseDocker, please provide -Username, -Database and -Password or define POSTGRES_* in ops/.env"
  }
  Write-Host "Applying indexes via docker exec to container '$ContainerName'..."
  $sql = Get-Content $File -Raw
  $cmd = "PGPASSWORD='$Password' psql -U $Username -d $Database -f -"
  $bytes = [Text.Encoding]::UTF8.GetBytes($sql)
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = "docker"
  $psi.Arguments = "exec -i $ContainerName sh -lc \"$cmd\""
  $psi.UseShellExecute = $false
  $psi.RedirectStandardInput = $true
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $p = [System.Diagnostics.Process]::Start($psi)
  $p.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
  $p.StandardInput.Close()
  $out = $p.StandardOutput.ReadToEnd()
  $err = $p.StandardError.ReadToEnd()
  $p.WaitForExit()
  if ($p.ExitCode -ne 0) {
    Write-Error "docker exec failed ($($p.ExitCode))`n$err`n$out"
  } else {
    Write-Host $out
    if ($err) { Write-Warning $err }
    Write-Host "Indexes applied successfully via docker."
  }
  return
}

# Host psql path
$hasPsql = Get-Command psql -ErrorAction SilentlyContinue
if (-not $hasPsql) {
  throw "psql not found. Install PostgreSQL client or re-run with -UseDocker."
}

if ($DbUrl) {
  Write-Host "Applying indexes with psql using connection URL..."
  & psql $DbUrl -f $File
  if ($LASTEXITCODE -ne 0) { throw "psql failed with exit code $LASTEXITCODE" }
  Write-Host "Indexes applied successfully."
  return
}

if (-not $Host) { $Host = 'localhost' }
if (-not $Username -or -not $Database) {
  throw "Provide -Host/-Port/-Database/-Username/-Password or -DbUrl or use -UseDocker to exec into postgres container."
}

$env:PGPASSWORD = $Password
Write-Host "Applying indexes with psql to $Host:$Port/$Database ..."
& psql -h $Host -p $Port -U $Username -d $Database -f $File
if ($LASTEXITCODE -ne 0) { throw "psql failed with exit code $LASTEXITCODE" }
Write-Host "Indexes applied successfully."

