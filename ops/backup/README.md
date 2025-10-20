Backup System

- Hourly PostgreSQL backups are created automatically by the API background service during the configured hours and archived daily.
- Configuration lives in `backend/Api/appsettings.json` under `Backup`.
- Backups and archives are stored under `ops/backups` by default.

Scripts

- `ops/backup/search.ps1` — Search backups and archives via the JSON index.
- `ops/backup/list-archives.ps1` — List daily archives and contained backup counts.
- `ops/backup/run-now.ps1` — Create an immediate backup using `pg_dump` with the API connection string.

Notes

- Ensure `pg_dump` is available on PATH or set `Backup:PgDumpPath`.
- The index is at `ops/backups/index.json` and is updated by the background service and scripts.
