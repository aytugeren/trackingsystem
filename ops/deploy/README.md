Production Deploy Guide

1) Configure appsettings.Production.json
   - File: backend/Api/appsettings.Production.json
   - Set ConnectionStrings.DefaultConnection
   - Verify Backup settings and BusyStart/BusyEnd (09:00–19:30)
   - Keep Optimization.EnableIndexCreation=false for first rollout.

2) Build and start API with ASPNETCORE_ENVIRONMENT=Production
   - dotnet build KuyumculukTakipProgrami.sln -c Release
   - Set ASPNETCORE_ENVIRONMENT=Production when running.

3) Create performance indexes off-peak (optional but recommended)
   - Use psql: psql "$DB_URL" -f ops/db/create-indexes.sql
   - Or temporarily set Optimization.EnableIndexCreation=true and restart outside busy hours.

4) Verify health and endpoints
   - GET /health → { status: "ok" }
   - GET /api/invoices with small pageSize to confirm latency is low.

5) Backups
   - pg_dump is installed in the API image (postgresql-client). No extra step needed.
   - Backups are persisted via docker volume `ktp_backups` mounted at `/app/ops/backups`.
   - Manual run: powershell ops/backup/run-now.ps1 -Env Production

7) Persist DataProtection keys (auth cookies, tokens)
   - Keys are persisted via docker volume `ktp_dataprotection` at `/root/.aspnet/DataProtection-Keys`.

6) Rollback plan
   - Backups exist under ops/backups; archives nightly at 23:59.
   - Index creation is idempotent and concurrent; safe to rerun off-peak.
