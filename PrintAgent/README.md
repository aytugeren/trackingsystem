# PrintAgent

## Folder structure

- `PrintAgent.csproj` – .NET 8 console application with Dapper and RawPrint dependencies.  
- `Program.cs` – Bootstraps a generic host, binds configuration, and registers services.  
- `PrintQueueRepository.cs` – Polls the remote `PrintQueue` and updates jobs via Dapper.  
- `ZebraPrinter.cs` – Sends ZPL payloads to the configured Zebra printer using the RawPrint native helper.  
- `PrintAgentWorker.cs` – Background loop that checks the queue, prints jobs, and logs outcomes.  
- `Models/PrintJob.cs` – Typed representation of `PrintQueue` rows.  
- `appsettings.json` – Stores the database connection string, printer name, and polling cadence.

## Build & run instructions

1. Restore packages and build the project from the `PrintAgent` directory:

   ```bash
   dotnet restore
   dotnet build
   ```

2. Run the agent (Windows host with the Zebra printer installed):

   ```bash
   dotnet run --project PrintAgent/PrintAgent.csproj
   ```

   The console logs will show polling status, printing outcomes, and any errors.

## Database connection

The agent uses PostgreSQL via Npgsql/Dapper, so the connection string must use `Host`/`Port`/`Username`/`Password` instead of the SQL Server-style `Server=host:port`. Example:

```json
"ConnectionStrings": {
  "Default": "Host=161.97.97.216;Port=5433;Database=ktp_db;Username=erenkuyumculukprelive;Password=hA7J8imJz2X6L6rE3r3zQetFEK"
}
```

Make sure the host is reachable and that the PostgreSQL user can connect from the agent’s machine.

The agent automatically uses `Environment.MachineName` but you can override it by adding `Agent:MachineName` to `appsettings.json` so the queue filter matches a specific host:

```json
"Agent": {
  "PollIntervalMs": 2000,
  "MachineName": "ETIKET-PC"
}
```

If `MachineName` is left null, the runtime falls back to the operating system’s reported host name.

### Schema bootstrap

At startup the agent now auto-creates or updates the `PrintQueue` table if it does not exist, so you only need to ensure the database itself is reachable. The DDL executed is equivalent to:

```sql
CREATE TABLE IF NOT EXISTS PrintQueue (
    Id SERIAL PRIMARY KEY,
    Zpl TEXT NOT NULL,
    IsPrinted BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PrintedAt TIMESTAMPTZ NULL,
    MachineName VARCHAR(50) NULL
);

ALTER TABLE PrintQueue
ADD COLUMN IF NOT EXISTS MachineName VARCHAR(50) NULL;
```

If you later add columns, update the agent’s `EnsureSchemaAsync` accordingly.

## RawPrint NuGet installation

If you need to re-add the RawPrint dependency:

```bash
dotnet add PrintAgent/PrintAgent.csproj package RawPrint --version 0.5.0
```

This package exposes `Printer.PrintRawStream` which streams ZPL to USB printers via the Windows print spooler.

## Example SQL queries

```sql
SELECT Id, Zpl, IsPrinted, CreatedAt, PrintedAt, MachineName
FROM PrintQueue
WHERE IsPrinted = 0
  AND (MachineName IS NULL OR MachineName = @MachineName)
ORDER BY Id ASC
LIMIT 1;
```

```sql
UPDATE PrintQueue
SET IsPrinted = 1, PrintedAt = NOW()
WHERE Id = @Id;
```

## Sample ZPL test

Insert a row that contains this payload to test the printer:

```
^XA
^FO50,50^A0N,40,40^FDPrintAgent Test^FS
^FO50,100^BY2^BCN,60,Y,N,N
^FD>123456789012^FS
^PQ1
^XZ
```

Set `MachineName` to the host where you run PrintAgent, or leave it `NULL` to allow any machine to print it.

## Running as a Windows Service

The host now registers `UseWindowsService()`, so you can install the published agent directly as a Windows Service.

1. Publish a release build so the service has all dependencies beside the EXE:

   ```bash
   dotnet publish PrintAgent/PrintAgent.csproj -c Release -o ./PrintAgent/bin/Release/net8.0/publish
   ```

2. Install the service (adjust the `binPath` to the published folder on the target machine):

   ```powershell
   sc.exe create PrintAgent binPath= "C:\Path\To\PrintAgent\bin\Release\net8.0\publish\PrintAgent.exe" start= auto
   sc.exe description PrintAgent "PrintAgent background worker for the zebra printer queue."
   sc.exe start PrintAgent
   ```

   Alternatively use `New-Service` if you prefer PowerShell syntax.

3. The service loads `appsettings.json` from the executable directory (`AppContext.BaseDirectory`), so keep the published settings beside the EXE.

## Log dosyasi

`PrintAgent` streams every step to `logs/printagent.log` inside the publish folder. Startup logs include the resolved PostgreSQL host/port/database/user, the machine filter, and each operation (opening connections, ensuring schema, querying/marking jobs, etc.). If `sc.exe start` times out or fails, open or tail this file to see the exact exception and the ordered steps that preceded it.

