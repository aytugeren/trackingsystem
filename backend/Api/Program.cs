using KuyumculukTakipProgrami.Api.Services;
using KuyumculukTakipProgrami.Api;

using KuyumculukTakipProgrami.Application;
using KuyumculukTakipProgrami.Infrastructure;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using KuyumculukTakipProgrami.Infrastructure.Pricing;
using KuyumculukTakipProgrami.Infrastructure.Util;
using Microsoft.EntityFrameworkCore;
using KuyumculukTakipProgrami.Application.Invoices;
using KuyumculukTakipProgrami.Application.Expenses;
using KuyumculukTakipProgrami.Domain.Entities.Market;
using System.Globalization;
using System.Threading;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using KuyumculukTakipProgrami.Domain.Entities;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Mime;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
// Logging: limit console output to errors only
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Error);

// Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IPrintQueueService, PrintQueueService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // Allow UTF-8 characters (Turkish) in JSON without escaping
    options.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// JWT Auth
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key") ?? "insecure-dev-key-change-me-please-very-long";
var jwtIssuer = jwtSection.GetValue<string>("Issuer") ?? "KTP";
var jwtAudience = jwtSection.GetValue<string>("Audience") ?? "KTP-Clients";
var jwtExpiresHours = jwtSection.GetValue<int?>("ExpiresHours") ?? 8;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole(Role.Yonetici.ToString()));
});

// Seed configuration (admin user)
var seedAdminEmail = builder.Configuration["Seed:AdminEmail"] ?? "aytgeren@gmail.com";
var seedAdminPassword = builder.Configuration["Seed:AdminPassword"] ?? "72727361Aa";

var app = builder.Build();
var logger = app.Logger;

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ensure databases
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
// Force UTF-8 for JSON responses (only for /api)
app.Use(async (context, next) =>
{
    context.Response.OnStarting(state =>
    {
        var http = (HttpContext)state!;
        var ct = http.Response.ContentType;
        var isApi = http.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
        if (isApi && (string.IsNullOrEmpty(ct) || ct.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)))
        {
            http.Response.ContentType = "application/json; charset=utf-8";
        }
        return Task.CompletedTask;
    }, context);
    await next();
});
// Set Turkish culture and console encoding for proper I/O
try
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.InputEncoding = Encoding.UTF8;
}
catch { }
var tr = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = tr;
CultureInfo.DefaultThreadCurrentUICulture = tr;
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KtpDbContext>();
    var marketDb = scope.ServiceProvider.GetRequiredService<MarketDbContext>();
    var printQueueService = scope.ServiceProvider.GetRequiredService<IPrintQueueService>();
    try
    {
        db.Database.Migrate();
        await EnsureUsersSchemaAsync(db);
        // Ensure Leaves table exists (for leave requests)
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"Leaves\" (\"Id\" uuid PRIMARY KEY, \"From\" date NOT NULL,\"To\" date NOT NULL,\"FromTime\" time NULL,\"ToTime\" time NULL,\"Reason\" varchar(500) NULL,\"UserId\" uuid NULL,\"UserEmail\" varchar(200) NULL,\"CreatedAt\" timestamp with time zone NOT NULL DEFAULT now(), \"Status\" integer NOT NULL DEFAULT 0);");
        await db.Database.ExecuteSqlRawAsync("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Leaves_UserId_Users') THEN ALTER TABLE \"Leaves\" ADD CONSTRAINT \"FK_Leaves_UserId_Users\" FOREIGN KEY (\"UserId\") REFERENCES \"Users\"(\"Id\") ON DELETE SET NULL;END IF; END $$;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"LeaveAllowanceDays\" integer NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"CanCancelInvoice\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"CanAccessLeavesAdmin\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"WorkingDayHours\" double precision NULL;");
        // System settings
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"SystemSettings\" (\"Id\" uuid PRIMARY KEY, \"KeyName\" varchar(100) NOT NULL, \"Value\" varchar(100) NOT NULL, \"UpdatedAt\" timestamptz NOT NULL DEFAULT now());");
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_SystemSettings_KeyName ON \"SystemSettings\" (\"KeyName\");");
        // Enlarge Value column to store JSON configs (idempotent)
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"SystemSettings\" ALTER COLUMN \"Value\" TYPE text;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Leaves\" ADD COLUMN IF NOT EXISTS \"Status\" integer NOT NULL DEFAULT 0;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Leaves\" ADD COLUMN IF NOT EXISTS \"FromTime\" time NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Leaves\" ADD COLUMN IF NOT EXISTS \"ToTime\" time NULL;");
        // Roles and custom role assignment
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"Roles\" (\"Id\" uuid PRIMARY KEY, \"Name\" varchar(200) NOT NULL UNIQUE, \"CanCancelInvoice\" boolean NOT NULL DEFAULT false, \"CanToggleKesildi\" boolean NOT NULL DEFAULT false, \"CanAccessLeavesAdmin\" boolean NOT NULL DEFAULT false, \"CanManageSettings\" boolean NOT NULL DEFAULT false, \"CanManageCashier\" boolean NOT NULL DEFAULT false, \"CanManageKarat\" boolean NOT NULL DEFAULT false, \"CanUseInvoices\" boolean NOT NULL DEFAULT false, \"CanUseExpenses\" boolean NOT NULL DEFAULT false, \"CanViewReports\" boolean NOT NULL DEFAULT false, \"CanPrintLabels\" boolean NOT NULL DEFAULT false, \"LeaveAllowanceDays\" integer NULL, \"WorkingDayHours\" double precision NULL);");
        // add missing columns idempotently (in case table existed)
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Roles\" ADD COLUMN IF NOT EXISTS \"CanToggleKesildi\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Roles\" ADD COLUMN IF NOT EXISTS \"CanManageSettings\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Roles\" ADD COLUMN IF NOT EXISTS \"CanManageCashier\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Roles\" ADD COLUMN IF NOT EXISTS \"CanManageKarat\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Roles\" ADD COLUMN IF NOT EXISTS \"CanUseInvoices\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Roles\" ADD COLUMN IF NOT EXISTS \"CanUseExpenses\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Roles\" ADD COLUMN IF NOT EXISTS \"CanViewReports\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Roles\" ADD COLUMN IF NOT EXISTS \"CanPrintLabels\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"AssignedRoleId\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"CustomRoleName\" varchar(200) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"AltinSatisFiyati\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"AltinAyar\" integer NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"CreatedById\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"CreatedByEmail\" varchar(200) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"Kesildi\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"FinalizedAt\" timestamp with time zone NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"KasiyerId\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"SafAltinDegeri\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"UrunFiyati\" numeric(18,2) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"YeniUrunFiyati\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"GramDegeri\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"Iscilik\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN " +
            "IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Invoices_KasiyerId_Users') THEN " +
            "ALTER TABLE \"Invoices\" ADD CONSTRAINT \"FK_Invoices_KasiyerId_Users\" FOREIGN KEY (\"KasiyerId\") REFERENCES \"Users\"(\"Id\") ON DELETE SET NULL; " +
            "END IF; END $$;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"CreatedById\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"CreatedByEmail\" varchar(200) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"KasiyerId\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"AltinSatisFiyati\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"AltinAyar\" integer NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"SafAltinDegeri\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"UrunFiyati\" numeric(18,2) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"YeniUrunFiyati\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"GramDegeri\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"Iscilik\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"Kesildi\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"FinalizedAt\" timestamp with time zone NULL;");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"Customers\" (\"Id\" uuid PRIMARY KEY, \"AdSoyad\" varchar(150) NOT NULL, \"NormalizedAdSoyad\" varchar(160) NOT NULL, \"TCKN\" varchar(11) NOT NULL, \"Phone\" varchar(40) NULL, \"Email\" varchar(200) NULL, \"CreatedAt\" timestamptz NOT NULL DEFAULT now(), \"LastTransactionAt\" timestamptz NULL);");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Customers\" ADD COLUMN IF NOT EXISTS \"Phone\" varchar(40) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Customers\" ADD COLUMN IF NOT EXISTS \"Email\" varchar(200) NULL;");
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Customers_TCKN\" ON \"Customers\" (\"TCKN\");");
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Customers_NormalizedAdSoyad\" ON \"Customers\" (\"NormalizedAdSoyad\");");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"CustomerId\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"CustomerId\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Invoices_CustomerId\" ON \"Invoices\" (\"CustomerId\");");
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Expenses_CustomerId\" ON \"Expenses\" (\"CustomerId\");");
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN " +
            "IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Invoices_CustomerId_Customers') THEN " +
            "ALTER TABLE \"Invoices\" ADD CONSTRAINT \"FK_Invoices_CustomerId_Customers\" FOREIGN KEY (\"CustomerId\") REFERENCES \"Customers\"(\"Id\") ON DELETE SET NULL; " +
            "END IF; END $$;");
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN " +
            "IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Expenses_CustomerId_Customers') THEN " +
            "ALTER TABLE \"Expenses\" ADD CONSTRAINT \"FK_Expenses_CustomerId_Customers\" FOREIGN KEY (\"CustomerId\") REFERENCES \"Customers\"(\"Id\") ON DELETE SET NULL; " +
            "END IF; END $$;");
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN " +
            "IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Expenses_KasiyerId_Users') THEN " +
            "ALTER TABLE \"Expenses\" ADD CONSTRAINT \"FK_Expenses_KasiyerId_Users\" FOREIGN KEY (\"KasiyerId\") REFERENCES \"Users\"(\"Id\") ON DELETE SET NULL; " +
            "END IF; END $$;");
        await EnsureCustomersMigratedAsync(db, CancellationToken.None);
        await EnsureMarketSchemaAsync(marketDb);
        await SeedData.EnsureSeededAsync(db);
        // Temizlik: Eski test seed verilerini sil ("Ali Veli", "Ahmet Demir")
        var demoInvoices = db.Invoices.Where(x => x.MusteriAdSoyad == "Ali Veli");
        if (await demoInvoices.AnyAsync())
        {
            db.Invoices.RemoveRange(demoInvoices);
            await db.SaveChangesAsync();
        }
        var demoExpenses = db.Expenses.Where(x => x.MusteriAdSoyad == "Ahmet Demir");
        if (await demoExpenses.AnyAsync())
        {
            db.Expenses.RemoveRange(demoExpenses);
            await db.SaveChangesAsync();
        }
        await printQueueService.EnsureSchemaAsync(CancellationToken.None);
        // Ensure default admin user exists (idempotent)
        var adminEmail = seedAdminEmail;
        var existingAdmin = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == adminEmail.ToLower());
        if (existingAdmin is null)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                PasswordHash = HashPassword(seedAdminPassword),
                Role = Role.Yonetici
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        else if (app.Environment.IsDevelopment() && !VerifyPassword("72727361Aa", existingAdmin.PasswordHash))
        {
            // In development, keep default admin password in sync for convenience
            existingAdmin.PasswordHash = HashPassword("72727361Aa");
            await db.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed");
    }
}

// Invoices
app.MapPost("/api/invoices", async (CreateInvoiceDto dto, ICreateInvoiceHandler handler, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    try
    {
        // Permission: admin or role.CanUseInvoices
        if (!http.User.IsInRole(Role.Yonetici.ToString()))
        {
            var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? http.User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
            var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
            if (u?.AssignedRoleId is Guid rid)
            {
                var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid, ct);
                if (r?.CanUseInvoices != true) return Results.Forbid();
            }
            else return Results.Forbid();
        }
        var sub2 = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        var email = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value;
        Guid? currentUserId = Guid.TryParse(sub2, out var uidVal) ? uidVal : null;
        if (currentUserId is null)
        {
            var hdrSub = http.Request.Headers["X-User-Id"].FirstOrDefault();
            if (Guid.TryParse(hdrSub, out var uid2)) currentUserId = uid2;
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            email = http.Request.Headers["X-User-Email"].FirstOrDefault();
        }
        var id = await handler.HandleAsync(new CreateInvoice(dto, currentUserId), ct);
        // Stamp creator info for legacy fields
        var inv = await db.Invoices.FindAsync(new object?[] { id }, ct);
        if (inv is not null)
        {
            inv.CreatedById = currentUserId;
            inv.CreatedByEmail = string.IsNullOrWhiteSpace(email) ? null : email;
            await db.SaveChangesAsync(ct);
        }
        return Results.Created($"/api/invoices/{id}", new { id });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errors = ex.Message.Split(" | ") });
    }
}).WithTags("Invoices").RequireAuthorization();

// Update invoice status (admin or users with CanCancelInvoice via role)
app.MapPut("/api/invoices/{id:guid}/status", async (Guid id, UpdateInvoiceStatusRequest body, KtpDbContext db, HttpContext http) =>
{
    var inv = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id);
    if (inv is null) return Results.NotFound();
    // Authorization: Admin or users with CanCancelInvoice
    var sub = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    var can = false;
    if (http.User.IsInRole(Role.Yonetici.ToString())) can = true;
    else if (Guid.TryParse(sub, out var uid))
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (u?.AssignedRoleId is Guid rid)
        {
            var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            can = r?.CanToggleKesildi == true;
        }
    }
    if (!can) return Results.Forbid();

    inv.Kesildi = body.Kesildi;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Invoices").RequireAuthorization();

// Finalize invoice: compute fields and set as Kesildi
app.MapPost("/api/invoices/{id:guid}/finalize", async (Guid id, KtpDbContext db, HttpContext http) =>
{
    // Permission: admin or role.CanToggleKesildi
    if (!http.User.IsInRole(Role.Yonetici.ToString()))
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (u?.AssignedRoleId is Guid rid)
        {
            var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (r?.CanToggleKesildi != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    var inv = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id);
    if (inv is null) return Results.NotFound();
    if (!inv.AltinSatisFiyati.HasValue)
        return Results.BadRequest(new { error = "Alt?n sat?? fiyat? bulunamad?." });
    

    decimal R2(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
    var hasAltin = inv.AltinSatisFiyati!.Value;
    var rawSafAltin = inv.AltinAyar == AltinAyar.Ayar22 ? hasAltin * 0.916m : hasAltin * 0.995m;
    var rawYeniUrun = inv.AltinAyar == AltinAyar.Ayar22 ? inv.Tutar * 0.99m : inv.Tutar * 0.998m;
    var safAltin = R2(rawSafAltin);
    var yeniUrun = R2(rawYeniUrun);
    var gram = (safAltin == 0 ? 0 : R2(yeniUrun / safAltin));
    var altinHizmet = R2(gram * safAltin);
    var iscilikKdvli = R2(R2(inv.Tutar) - altinHizmet);
    var iscilik = R2(iscilikKdvli / 1.20m);

    inv.UrunFiyati = R2(inv.Tutar);
    inv.SafAltinDegeri = safAltin;
    inv.YeniUrunFiyati = yeniUrun;
    inv.GramDegeri = gram;
    inv.Iscilik = iscilik;
    inv.FinalizedAt = DateTime.UtcNow;
    // Kesildi durumu kasiyerden de�il, sadece dashboarddan de�i�tirilecek
    await db.SaveChangesAsync();
    return Results.Ok(new { inv.Id, inv.SafAltinDegeri, inv.UrunFiyati, inv.YeniUrunFiyati, inv.GramDegeri, inv.Iscilik, inv.Kesildi });
}).WithTags("Invoices").RequireAuthorization();

app.MapGet("/api/invoices", async (int? page, int? pageSize, KtpDbContext db, IMemoryCache cache, HttpContext http, CancellationToken ct) =>
{
    // Permission: admin or role.CanUseInvoices
    if (!http.User.IsInRole(Role.Yonetici.ToString()))
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
        if (u?.AssignedRoleId is Guid rid)
        {
            var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid, ct);
            if (r?.CanUseInvoices != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 500);
    var cacheKey = $"invoices:{p}:{ps}";
    if (!cache.TryGetValue(cacheKey, out object? cached))
    {
        var baseQuery = from i in db.Invoices.AsNoTracking()
                        join u in db.Users.AsNoTracking() on i.KasiyerId equals u.Id into uu
                        from u in uu.DefaultIfEmpty()
                        select new
                        {
                            id = i.Id,
                            tarih = i.Tarih,
                            siraNo = i.SiraNo,
                            customerId = i.CustomerId,
                            musteriAdSoyad = i.MusteriAdSoyad,
                            tckn = i.TCKN,
                            tutar = i.Tutar,
                            odemeSekli = i.OdemeSekli,
                            altinAyar = i.AltinAyar,
                            altinSatisFiyati = i.AltinSatisFiyati,
                            safAltinDegeri = i.SafAltinDegeri,
                            urunFiyati = i.UrunFiyati,
                            yeniUrunFiyati = i.YeniUrunFiyati,
                            gramDegeri = i.GramDegeri,
                            iscilik = i.Iscilik,
                            finalizedAt = i.FinalizedAt,
                            kesildi = i.Kesildi,
                            kasiyerAdSoyad = (u != null ? u.Email : i.CreatedByEmail)
                        };

        var ordered = baseQuery
            .OrderBy(x => x.kesildi) // Bekliyor (false) ?nce
            .ThenByDescending(x => x.tarih)
            .ThenByDescending(x => x.siraNo);

        var totalCount = await db.Invoices.AsNoTracking().CountAsync(ct);
        var items = await ordered.Skip((p - 1) * ps).Take(ps).ToListAsync(ct);
        cached = new { items, totalCount };
        cache.Set(cacheKey, cached, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
    }
    return Results.Ok(cached);
}).WithTags("Invoices").RequireAuthorization();

// Next invoice sequence preview (does not consume)
app.MapGet("/api/invoices/next-sirano", async (KtpDbContext db, CancellationToken ct) =>
{
    var max = await db.Invoices.AsNoTracking().MaxAsync(x => (int?)x.SiraNo, ct) ?? 0;
    return Results.Ok(new { next = max + 1 });
}).WithTags("Invoices").RequireAuthorization();

// Expenses
app.MapPost("/api/expenses", async (CreateExpenseDto dto, ICreateExpenseHandler handler, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    try
    {
        // Permission: admin or role.CanUseExpenses
        if (!http.User.IsInRole(Role.Yonetici.ToString()))
        {
            var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? http.User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
            var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
            if (u?.AssignedRoleId is Guid rid)
            {
                var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid, ct);
                if (r?.CanUseExpenses != true) return Results.Forbid();
            }
            else return Results.Forbid();
        }
        var sub2 = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        var email = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value;
        Guid? currentUserId = Guid.TryParse(sub2, out var uidVal) ? uidVal : null;
        if (currentUserId is null)
        {
            var hdrSub = http.Request.Headers["X-User-Id"].FirstOrDefault();
            if (Guid.TryParse(hdrSub, out var uid2)) currentUserId = uid2;
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            email = http.Request.Headers["X-User-Email"].FirstOrDefault();
        }
        var id = await handler.HandleAsync(new CreateExpense(dto, currentUserId), ct);
        // Stamp creator info for legacy fields
        var exp = await db.Expenses.FindAsync(new object?[] { id }, ct);
        if (exp is not null)
        {
            exp.CreatedById = currentUserId;
            exp.CreatedByEmail = string.IsNullOrWhiteSpace(email) ? null : email;
            await db.SaveChangesAsync(ct);
        }
        return Results.Created($"/api/expenses/{id}", new { id });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errors = ex.Message.Split(" | ") });
    }
}).WithTags("Expenses").RequireAuthorization();



// Update expense status (admin or users with CanCancelInvoice via role)
app.MapPut("/api/expenses/{id:guid}/status", async (Guid id, UpdateInvoiceStatusRequest body, KtpDbContext db, HttpContext http) =>
{
    var exp = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id);
    if (exp is null) return Results.NotFound();
    // Authorization: Admin or users with CanCancelInvoice
    var sub = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    var can = false;
    if (http.User.IsInRole(Role.Yonetici.ToString())) can = true;
    else if (Guid.TryParse(sub, out var uid))
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (u?.AssignedRoleId is Guid rid)
        {
            var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            can = r?.CanToggleKesildi == true;
        }
    }
    if (!can) return Results.Forbid();

    exp.Kesildi = body.Kesildi;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Expenses").RequireAuthorization();

// Finalize expense: compute fields and set as Kesildi
app.MapPost("/api/expenses/{id:guid}/finalize", async (Guid id, KtpDbContext db) =>
{
    var exp = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id);
    if (exp is null) return Results.NotFound();
    if (!exp.AltinSatisFiyati.HasValue)
        return Results.BadRequest(new { error = "Alt?n sat?? fiyat? bulunamad?." });
    

    decimal R2e(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
    var hasAltin = exp.AltinSatisFiyati!.Value;
    var rawSafAltin = exp.AltinAyar == AltinAyar.Ayar22 ? hasAltin * 0.916m : hasAltin * 0.995m;
    var rawYeniUrun = exp.AltinAyar == AltinAyar.Ayar22 ? exp.Tutar * 0.99m : exp.Tutar * 0.998m;
    var safAltin = R2e(rawSafAltin);
    var yeniUrun = R2e(rawYeniUrun);
    var gram = (safAltin == 0 ? 0 : R2e(yeniUrun / safAltin));
    var altinHizmet = R2e(gram * safAltin);
    var iscilikKdvli = R2e(R2e(exp.Tutar) - altinHizmet);
    var iscilik = R2e(iscilikKdvli / 1.20m);

    exp.UrunFiyati = R2e(exp.Tutar);
    exp.SafAltinDegeri = safAltin;
    exp.YeniUrunFiyati = yeniUrun;
    exp.GramDegeri = gram;
    exp.Iscilik = iscilik;
    exp.FinalizedAt = DateTime.UtcNow;
    // Kesildi durumu kasiyerden de�il, sadece dashboarddan de�i�tirilecek
    await db.SaveChangesAsync();
    return Results.Ok(new { exp.Id, exp.SafAltinDegeri, exp.UrunFiyati, exp.YeniUrunFiyati, exp.GramDegeri, exp.Iscilik, exp.Kesildi });
}).WithTags("Expenses").RequireAuthorization();

app.MapGet("/api/expenses", async (int? page, int? pageSize, KtpDbContext db, IMemoryCache cache, HttpContext http, CancellationToken ct) =>
{
    // Permission: admin or role.CanUseExpenses
    if (!http.User.IsInRole(Role.Yonetici.ToString()))
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
        if (u?.AssignedRoleId is Guid rid)
        {
            var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid, ct);
            if (r?.CanUseExpenses != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 500);
    var cacheKey = $"expenses:{p}:{ps}";
    if (!cache.TryGetValue(cacheKey, out object? cached))
    {
        var baseQuery = from e in db.Expenses.AsNoTracking()
                        join u in db.Users.AsNoTracking() on e.KasiyerId equals u.Id into uu
                        from u in uu.DefaultIfEmpty()
                        select new
                        {
                            id = e.Id,
                            tarih = e.Tarih,
                            siraNo = e.SiraNo,
                            customerId = e.CustomerId,
                            musteriAdSoyad = e.MusteriAdSoyad,
                            tckn = e.TCKN,
                            tutar = e.Tutar,
                            altinAyar = e.AltinAyar,
                            altinSatisFiyati = e.AltinSatisFiyati,
                            safAltinDegeri = e.SafAltinDegeri,
                            urunFiyati = e.UrunFiyati,
                            yeniUrunFiyati = e.YeniUrunFiyati,
                            gramDegeri = e.GramDegeri,
                            iscilik = e.Iscilik,
                            finalizedAt = e.FinalizedAt,
                            kesildi = e.Kesildi,
                            kasiyerAdSoyad = (u != null ? u.Email : e.CreatedByEmail)
                        };

        var ordered = baseQuery
            .OrderBy(x => x.kesildi) // Bekliyor (false) ?nce
            .ThenByDescending(x => x.tarih)
            .ThenByDescending(x => x.siraNo);

        var totalCount = await db.Expenses.AsNoTracking().CountAsync(ct);
        var items = await ordered.Skip((p - 1) * ps).Take(ps).ToListAsync(ct);
        cached = new { items, totalCount };
        cache.Set(cacheKey, cached, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
    }
    return Results.Ok(cached);
}).WithTags("Expenses").RequireAuthorization();

// Cashier: create draft invoice with definitive, gapless SiraNo
app.MapPost("/api/cashier/invoices/draft", async (CreateInvoiceDto dto, KtpDbContext db, MarketDbContext mdb, HttpContext http, CancellationToken ct) =>
{
    try
    {
        var sub = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value
            ?? http.User.FindFirst(ClaimTypes.Email)?.Value;
        Guid? currentUserId = Guid.TryParse(sub, out var uidVal) ? uidVal : null;
        if (currentUserId is null)
        {
            var hdrSub = http.Request.Headers["X-User-Id"].FirstOrDefault();
            if (Guid.TryParse(hdrSub, out var uid2)) currentUserId = uid2;
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            email = http.Request.Headers["X-User-Email"].FirstOrDefault();
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Invoices\" IN EXCLUSIVE MODE;", ct);
        var max = await db.Invoices.AsNoTracking().MaxAsync(x => (int?)x.SiraNo, ct) ?? 0;
        var next = max + 1;

        var normalizedName = CustomerUtil.NormalizeName(dto.MusteriAdSoyad);
        var normalizedTckn = CustomerUtil.NormalizeTckn(dto.TCKN);
        var customerPhone = dto.Telefon?.Trim();
        var customerEmail = dto.Email?.Trim();
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.TCKN == normalizedTckn, ct);
        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                AdSoyad = normalizedName,
                NormalizedAdSoyad = normalizedName,
                TCKN = normalizedTckn,
                Phone = string.IsNullOrWhiteSpace(customerPhone) ? null : customerPhone,
                Email = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
                CreatedAt = DateTime.UtcNow,
                LastTransactionAt = DateTime.UtcNow
            };
            db.Customers.Add(customer);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(normalizedName) && !string.Equals(customer.AdSoyad, normalizedName, StringComparison.Ordinal))
            {
                customer.AdSoyad = normalizedName;
                customer.NormalizedAdSoyad = normalizedName;
            }
            if (!string.IsNullOrWhiteSpace(customerPhone))
                customer.Phone = customerPhone;
            if (!string.IsNullOrWhiteSpace(customerEmail))
                customer.Email = customerEmail;
            customer.LastTransactionAt = DateTime.UtcNow;
        }

        var entity = new Invoice
        {
            Id = Guid.NewGuid(),
            Tarih = dto.Tarih,
            SiraNo = next,
            MusteriAdSoyad = customer.AdSoyad,
            TCKN = customer.TCKN,
            CustomerId = customer.Id,
            Tutar = dto.Tutar,
            OdemeSekli = dto.OdemeSekli,
            AltinAyar = dto.AltinAyar,
            KasiyerId = currentUserId,
            CreatedById = currentUserId,
            CreatedByEmail = string.IsNullOrWhiteSpace(email) ? null : email,
            Kesildi = false
        };

        // Stamp current ALTIN final sell price (per ayar margin)
        var priceData = await mdb.GetLatestPriceForAyarAsync(entity.AltinAyar, useBuyMargin: false, ct);
        if (priceData is null)
            return Results.BadRequest(new { error = "Kayitli altin fiyati bulunamadi" });
        entity.AltinSatisFiyati = priceData.Price;

        db.Invoices.Add(entity);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Created($"/api/invoices/{entity.Id}", new { id = entity.Id, siraNo = entity.SiraNo, altinSatisFiyati = entity.AltinSatisFiyati, updatedAt = priceData.SourceTime });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errors = ex.Message.Split(" | ") });
    }
}).WithTags("Invoices").RequireAuthorization();

// Cashier: create draft expense with definitive, gapless SiraNo
app.MapPost("/api/cashier/expenses/draft", async (CreateExpenseDto dto, KtpDbContext db, MarketDbContext mdb, HttpContext http, CancellationToken ct) =>
{
    try
    {
        var sub = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value
            ?? http.User.FindFirst(ClaimTypes.Email)?.Value;
        Guid? currentUserId = Guid.TryParse(sub, out var uidVal) ? uidVal : null;
        if (currentUserId is null)
        {
            var hdrSub = http.Request.Headers["X-User-Id"].FirstOrDefault();
            if (Guid.TryParse(hdrSub, out var uid2)) currentUserId = uid2;
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            email = http.Request.Headers["X-User-Email"].FirstOrDefault();
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Expenses\" IN EXCLUSIVE MODE;", ct);
        var max = await db.Expenses.AsNoTracking().MaxAsync(x => (int?)x.SiraNo, ct) ?? 0;
        var next = max + 1;

        var normalizedName2 = CustomerUtil.NormalizeName(dto.MusteriAdSoyad);
        var normalizedTckn2 = CustomerUtil.NormalizeTckn(dto.TCKN);
        var customerPhone2 = dto.Telefon?.Trim();
        var customerEmail2 = dto.Email?.Trim();
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.TCKN == normalizedTckn2, ct);
        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                AdSoyad = normalizedName2,
                NormalizedAdSoyad = normalizedName2,
                TCKN = normalizedTckn2,
                Phone = string.IsNullOrWhiteSpace(customerPhone2) ? null : customerPhone2,
                Email = string.IsNullOrWhiteSpace(customerEmail2) ? null : customerEmail2,
                CreatedAt = DateTime.UtcNow,
                LastTransactionAt = DateTime.UtcNow
            };
            db.Customers.Add(customer);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(normalizedName2) && !string.Equals(customer.AdSoyad, normalizedName2, StringComparison.Ordinal))
            {
                customer.AdSoyad = normalizedName2;
                customer.NormalizedAdSoyad = normalizedName2;
            }
            if (!string.IsNullOrWhiteSpace(customerPhone2))
                customer.Phone = customerPhone2;
            if (!string.IsNullOrWhiteSpace(customerEmail2))
                customer.Email = customerEmail2;
            customer.LastTransactionAt = DateTime.UtcNow;
        }

        var entity = new Expense
        {
            Id = Guid.NewGuid(), Tarih = dto.Tarih, SiraNo = next,
            MusteriAdSoyad = customer.AdSoyad, TCKN = customer.TCKN, CustomerId = customer.Id, Tutar = dto.Tutar,
            AltinAyar = dto.AltinAyar, KasiyerId = currentUserId,
            CreatedById = currentUserId, CreatedByEmail = string.IsNullOrWhiteSpace(email) ? null : email,
            Kesildi = false
        };

        var priceData = await mdb.GetLatestPriceForAyarAsync(entity.AltinAyar, useBuyMargin: true, ct);
        if (priceData is null)
            return Results.BadRequest(new { error = "Kayitli altin fiyati bulunamadi" });
        entity.AltinSatisFiyati = priceData.Price;

        db.Expenses.Add(entity);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Created($"/api/expenses/{entity.Id}", new { id = entity.Id, siraNo = entity.SiraNo, altinSatisFiyati = entity.AltinSatisFiyati, updatedAt = priceData.SourceTime });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errors = ex.Message.Split(" | ") });
    }
}).WithTags("Expenses").RequireAuthorization();

// Allow deleting non-finalized records (to avoid gaps on cancel)
app.MapDelete("/api/invoices/{id:guid}", async (Guid id, KtpDbContext db, HttpContext http) =>
{
    var inv = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id);
    // Idempotent delete: if not found, treat as already deleted
    if (inv is null) return Results.NoContent();
    if (inv.Kesildi) return Results.BadRequest(new { error = "Kesilmi� kay�t silinemez" });
    // Ownership check (only creator can delete)
    var sub = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
    Guid? currentUserId = Guid.TryParse(sub, out var uidVal) ? uidVal : null;
    if (currentUserId is null)
    {
        var hdrSub = http.Request.Headers["X-User-Id"].FirstOrDefault();
        if (Guid.TryParse(hdrSub, out var uid2)) currentUserId = uid2;
    }
    if (inv.CreatedById.HasValue && currentUserId.HasValue && inv.CreatedById != currentUserId)
        return Results.Forbid();
    db.Invoices.Remove(inv);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Invoices").RequireAuthorization();

app.MapDelete("/api/expenses/{id:guid}", async (Guid id, KtpDbContext db, HttpContext http) =>
{
    var exp = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id);
    // Idempotent delete: if not found, treat as already deleted
    if (exp is null) return Results.NoContent();
    if (exp.Kesildi) return Results.BadRequest(new { error = "Kesilmi� kay�t silinemez" });
    var sub = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
    Guid? currentUserId = Guid.TryParse(sub, out var uidVal) ? uidVal : null;
    if (currentUserId is null)
    {
        var hdrSub = http.Request.Headers["X-User-Id"].FirstOrDefault();
        if (Guid.TryParse(hdrSub, out var uid2)) currentUserId = uid2;
    }
    if (exp.CreatedById.HasValue && currentUserId.HasValue && exp.CreatedById != currentUserId)
        return Results.Forbid();
    db.Expenses.Remove(exp);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Expenses").RequireAuthorization();

// Next expense sequence preview (does not consume)
app.MapGet("/api/expenses/next-sirano", async (KtpDbContext db, CancellationToken ct) =>
{
    var max = await db.Expenses.AsNoTracking().MaxAsync(x => (int?)x.SiraNo, ct) ?? 0;
    return Results.Ok(new { next = max + 1 });
}).WithTags("Expenses").RequireAuthorization();

app.MapGet("/api/customers/suggest", async (string q, int? limit, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!await HasCustomerLookupPermissionAsync(http, db, ct)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<object>());

    var take = Math.Clamp(limit ?? 8, 1, 50);
    var normalizedQuery = CustomerUtil.NormalizeName(q);
    var trimmed = q.Trim();

    var baseQuery = db.Customers.AsNoTracking()
        .Where(c =>
            (normalizedQuery != string.Empty && c.NormalizedAdSoyad.Contains(normalizedQuery)) ||
            (!string.IsNullOrEmpty(trimmed) && c.TCKN.Contains(trimmed)));

    var items = await baseQuery
        .OrderByDescending(c => c.LastTransactionAt ?? c.CreatedAt)
        .ThenBy(c => c.AdSoyad)
        .Take(take)
        .Select(c => new
        {
            id = c.Id,
            adSoyad = c.AdSoyad,
            tckn = c.TCKN,
            phone = c.Phone,
            email = c.Email,
            hasContact = !string.IsNullOrWhiteSpace(c.Phone) || !string.IsNullOrWhiteSpace(c.Email)
        })
        .ToListAsync(ct);

    return Results.Ok(items);
}).WithTags("Customers").RequireAuthorization();

app.MapGet("/api/customers", async (int? page, int? pageSize, string? q, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!await HasCustomerLookupPermissionAsync(http, db, ct)) return Results.Forbid();
    var p = Math.Max(page ?? 1, 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 200);
    var normalizedQuery = CustomerUtil.NormalizeName(q);
    var trimmed = q?.Trim() ?? string.Empty;

    var baseQuery = db.Customers.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(q))
    {
        baseQuery = baseQuery.Where(c =>
            (!string.IsNullOrEmpty(normalizedQuery) && c.NormalizedAdSoyad.Contains(normalizedQuery)) ||
            (!string.IsNullOrEmpty(trimmed) && c.TCKN.Contains(trimmed)));
    }

    var totalCount = await baseQuery.CountAsync(ct);
    var items = await baseQuery
        .OrderByDescending(c => c.LastTransactionAt ?? c.CreatedAt)
        .ThenBy(c => c.AdSoyad)
        .Skip((p - 1) * ps)
        .Take(ps)
        .Select(c => new
        {
            id = c.Id,
            adSoyad = c.AdSoyad,
            tckn = c.TCKN,
            phone = c.Phone,
            email = c.Email,
            lastTransactionAt = c.LastTransactionAt,
            createdAt = c.CreatedAt
        })
        .ToListAsync(ct);

    var ids = items.Select(i => i.id).ToList();
    var invoiceCounts = await db.Invoices.AsNoTracking()
        .Where(x => x.CustomerId != null && ids.Contains(x.CustomerId.Value))
        .GroupBy(x => x.CustomerId!.Value)
        .Select(g => new { CustomerId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.CustomerId, x => x.Count, ct);
    var expenseCounts = await db.Expenses.AsNoTracking()
        .Where(x => x.CustomerId != null && ids.Contains(x.CustomerId.Value))
        .GroupBy(x => x.CustomerId!.Value)
        .Select(g => new { CustomerId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.CustomerId, x => x.Count, ct);

    var shaped = items.Select(c =>
    {
        var inv = invoiceCounts.TryGetValue(c.id, out var ci) ? ci : 0;
        var exp = expenseCounts.TryGetValue(c.id, out var ce) ? ce : 0;
        return new
        {
            c.id,
            c.adSoyad,
            c.tckn,
            c.phone,
            c.email,
            c.lastTransactionAt,
            c.createdAt,
            purchaseCount = inv + exp
        };
    });

    return Results.Ok(new { items = shaped, totalCount });
}).WithTags("Customers").RequireAuthorization();

app.MapGet("/api/customers/{id:guid}/transactions", async (Guid id, int? limit, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!await HasCustomerLookupPermissionAsync(http, db, ct)) return Results.Forbid();
    var take = Math.Clamp(limit ?? 50, 1, 200);

    var invoices = await db.Invoices.AsNoTracking()
        .Where(x => x.CustomerId == id)
        .OrderByDescending(x => x.Tarih)
        .ThenByDescending(x => x.SiraNo)
        .Take(take)
        .Select(x => new { id = x.Id, type = "invoice", tarih = x.Tarih, siraNo = x.SiraNo, tutar = x.Tutar })
        .ToListAsync(ct);
    var expenses = await db.Expenses.AsNoTracking()
        .Where(x => x.CustomerId == id)
        .OrderByDescending(x => x.Tarih)
        .ThenByDescending(x => x.SiraNo)
        .Take(take)
        .Select(x => new { id = x.Id, type = "expense", tarih = x.Tarih, siraNo = x.SiraNo, tutar = x.Tutar })
        .ToListAsync(ct);

    var combined = invoices.Concat<object>(expenses)
        .Cast<dynamic>()
        .OrderByDescending(x => x.tarih)
        .ThenByDescending(x => x.siraNo)
        .Take(take)
        .ToList();

    return Results.Ok(new { items = combined, totalCount = combined.Count });
}).WithTags("Customers").RequireAuthorization();

// Has alt?n pricing endpoints (manual entry)
app.MapGet("/api/pricing/gold", async (MarketDbContext mdb, CancellationToken ct) =>
{
    var latest = await mdb.GlobalGoldPrices
        .AsNoTracking()
        .OrderByDescending(x => x.UpdatedAt)
        .FirstOrDefaultAsync(ct);
    if (latest is null) return Results.NotFound();
    return Results.Ok(new { price = latest.Price, updatedAt = latest.UpdatedAt, updatedBy = latest.UpdatedByEmail });
}).WithTags("Pricing").RequireAuthorization();

app.MapPut("/api/pricing/gold", async (GoldPriceUpdateRequest body, MarketDbContext mdb, HttpContext http, CancellationToken ct) =>
{
    if (body == null || body.Price <= 0) return Results.BadRequest(new { error = "Ge?erli bir has alt?n fiyat? girin" });
    var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    Guid? userId = Guid.TryParse(sub, out var parsed) ? parsed : null;
    var email = http.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
        ?? http.User.FindFirst(ClaimTypes.Email)?.Value
        ?? http.User.FindFirst("email")?.Value;

    var now = DateTime.UtcNow;
    var latest = await mdb.GlobalGoldPrices.OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(ct);
    if (latest is null)
    {
        latest = new GlobalGoldPrice { Id = Guid.NewGuid() };
        mdb.GlobalGoldPrices.Add(latest);
    }
    latest.Price = Math.Round(body.Price, 3);
    latest.UpdatedAt = now;
    latest.UpdatedById = userId;
    latest.UpdatedByEmail = string.IsNullOrWhiteSpace(email) ? null : email;

    var exists = await mdb.PriceRecords.AnyAsync(x => x.Code == "ALTIN" && x.SourceTime == now, ct);
    if (!exists)
    {
        mdb.PriceRecords.Add(new PriceRecord
        {
            Id = Guid.NewGuid(),
            Code = "ALTIN",
            Alis = latest.Price,
            Satis = latest.Price,
            SourceTime = now,
            FinalAlis = latest.Price,
            FinalSatis = latest.Price,
            CreatedAt = now
        });
    }

    await mdb.SaveChangesAsync(ct);
    return Results.Ok(new { price = latest.Price, updatedAt = latest.UpdatedAt, updatedBy = latest.UpdatedByEmail });
}).WithTags("Pricing").RequireAuthorization();

// Current pricing (final sell) for ALTIN
app.MapGet("/api/pricing/current", async (MarketDbContext mdb, CancellationToken ct) =>
{
    var latest = await mdb.GlobalGoldPrices
        .AsNoTracking()
        .OrderByDescending(x => x.UpdatedAt)
        .FirstOrDefaultAsync(ct);
    if (latest is null) return Results.NotFound();
    return Results.Ok(new { code = "ALTIN", finalSatis = latest.Price, sourceTime = latest.UpdatedAt });
}).WithTags("Pricing").RequireAuthorization();

// Pricing settings endpoints
app.MapGet("/api/pricing/settings/{code}", async (string code, MarketDbContext mdb) =>
{
    code = code.ToUpperInvariant();
    var ps = await mdb.PriceSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code);
    ps ??= new PriceSetting { Code = code, MarginBuy = 0, MarginSell = 0 };
    return Results.Ok(new { code = ps.Code, marginBuy = ps.MarginBuy, marginSell = ps.MarginSell });
}).WithTags("Pricing");

app.MapPut("/api/pricing/settings/{code}", async (string code, PriceSetting body, MarketDbContext mdb) =>
{
    code = code.ToUpperInvariant();
    var existing = await mdb.PriceSettings.FirstOrDefaultAsync(x => x.Code == code);
    if (existing is null)
    {
        existing = new PriceSetting { Id = Guid.NewGuid(), Code = code };
        mdb.PriceSettings.Add(existing);
    }
    existing.MarginBuy = body.MarginBuy;
    existing.MarginSell = body.MarginSell;
    existing.UpdatedAt = DateTime.UtcNow;
    await mdb.SaveChangesAsync();
    return Results.Ok(new { code = existing.Code, marginBuy = existing.MarginBuy, marginSell = existing.MarginSell });
}).WithTags("Pricing");

app.MapGet("/api/pricing/status", async (MarketDbContext mdb, CancellationToken ct) =>
{
    var latest = await mdb.GlobalGoldPrices
        .AsNoTracking()
        .OrderByDescending(x => x.UpdatedAt)
        .FirstOrDefaultAsync(ct);
    if (latest is null)
    {
        return Results.Ok(new
        {
            hasAlert = true,
            message = "Has Altın Fiyatı Girilmemiş!",
            sourceTime = (DateTime?)null,
            fetchedAt = (DateTime?)null
        });
    }

    return Results.Ok(new
    {
        hasAlert = false,
        message = "Has Alt?n fiyat? manuel olarak girildi.",
        sourceTime = latest.UpdatedAt,
        fetchedAt = latest.UpdatedAt
    });
}).WithTags("Pricing");

app.MapGet("/api/pricing/{code}/latest", async (string code, MarketDbContext mdb, CancellationToken ct) =>
{
    code = code.ToUpperInvariant();
    if (code != "ALTIN") return Results.NotFound();
    var latest = await mdb.GlobalGoldPrices
        .AsNoTracking()
        .OrderByDescending(x => x.UpdatedAt)
        .FirstOrDefaultAsync(ct);
    if (latest is null) return Results.NotFound();
    return Results.Ok(new
    {
        code = "ALTIN",
        alis = latest.Price,
        satis = latest.Price,
        finalAlis = latest.Price,
        finalSatis = latest.Price,
        sourceTime = latest.UpdatedAt
    });
}).WithTags("Pricing");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Auth
app.MapPost("/api/auth/login", async (LoginRequest req, KtpDbContext db) =>
{
    var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
    if (user is null) return Results.Unauthorized();
    if (!VerifyPassword(req.Password ?? string.Empty, user.PasswordHash)) return Results.Unauthorized();

    var token = IssueJwt(user, jwtIssuer, jwtAudience, jwtKey, TimeSpan.FromHours(jwtExpiresHours));
    return Results.Ok(new { token, role = user.Role.ToString(), email = user.Email });
}).WithTags("Auth");

// Leaves (izinler)
app.MapGet("/api/leaves", async (string? from, string? to, KtpDbContext db, HttpContext http) =>
{
    DateOnly fromDo;
    DateOnly toDo;
    if (!string.IsNullOrWhiteSpace(from))
    {
        if (DateTime.TryParse(from, out var fdt)) fromDo = DateOnly.FromDateTime(fdt);
        else if (!DateOnly.TryParse(from, out fromDo)) return Results.BadRequest(new { error = "Ge�ersiz from" });
    }
    else fromDo = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));

    if (!string.IsNullOrWhiteSpace(to))
    {
        if (DateTime.TryParse(to, out var tdt)) toDo = DateOnly.FromDateTime(tdt);
        else if (!DateOnly.TryParse(to, out toDo)) return Results.BadRequest(new { error = "Ge�ersiz to" });
    }
    else toDo = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));

    var items = await db.Leaves.AsNoTracking()
        .Where(l => l.To >= fromDo && l.From <= toDo)
        .OrderByDescending(l => l.CreatedAt)
        .Select(l => new
        {
            id = l.Id,
            from = l.From.ToString("yyyy-MM-dd"),
            to = l.To.ToString("yyyy-MM-dd"),
            fromTime = l.FromTime.HasValue ? l.FromTime.Value.ToString("HH:mm") : null,
            toTime = l.ToTime.HasValue ? l.ToTime.Value.ToString("HH:mm") : null,
            user = l.UserEmail,
            reason = l.Reason,
            status = l.Status.ToString()
        })
        .ToListAsync();
    return Results.Ok(new { items });
}).RequireAuthorization();

app.MapPost("/api/leaves", async (LeaveCreateRequest req, KtpDbContext db, HttpContext http) =>
{
    if (string.IsNullOrWhiteSpace(req.from) || string.IsNullOrWhiteSpace(req.to))
        return Results.BadRequest(new { error = "from ve to zorunludur" });
    DateOnly from;
    DateOnly to;
    if (DateTime.TryParse(req.from, out var fromDt)) from = DateOnly.FromDateTime(fromDt); else if (!DateOnly.TryParse(req.from, out from)) return Results.BadRequest(new { error = "Ge�ersiz from" });
    if (DateTime.TryParse(req.to, out var toDt)) to = DateOnly.FromDateTime(toDt); else if (!DateOnly.TryParse(req.to, out to)) return Results.BadRequest(new { error = "Ge�ersiz to" });
    if (to < from)
        return Results.BadRequest(new { error = "Biti� tarihi ba�lang��tan �nce olamaz" });

    var userIdStr = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    Guid? userId = null;
    if (Guid.TryParse(userIdStr, out var parsed)) userId = parsed;
    var email = http.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
        ?? http.User.FindFirst(ClaimTypes.Email)?.Value
        ?? http.User.FindFirst("email")?.Value;

    TimeOnly? fromTime = null;
    TimeOnly? toTime = null;
    if (!string.IsNullOrWhiteSpace(req.fromTime))
    {
        if (!TimeOnly.TryParse(req.fromTime, out var ft)) return Results.BadRequest(new { error = "Ge�ersiz fromTime" });
        fromTime = ft;
    }
    if (!string.IsNullOrWhiteSpace(req.toTime))
    {
        if (!TimeOnly.TryParse(req.toTime, out var tt)) return Results.BadRequest(new { error = "Ge�ersiz toTime" });
        toTime = tt;
    }
    if (fromTime.HasValue || toTime.HasValue)
    {
        // Saat aral��� sadece tek g�n i�in desteklenir
        if (from != to) return Results.BadRequest(new { error = "Saatli izin sadece tek g�n i�in ge�erlidir" });
        if (!fromTime.HasValue || !toTime.HasValue || toTime.Value <= fromTime.Value)
            return Results.BadRequest(new { error = "Ge�ersiz saat aral���" });
    }

    var entity = new Leave
    {
        Id = Guid.NewGuid(),
        From = from,
        To = to,
        FromTime = fromTime,
        ToTime = toTime,
        Reason = string.IsNullOrWhiteSpace(req.reason) ? null : req.reason,
        UserId = userId,
        UserEmail = email,
        CreatedAt = DateTime.UtcNow,
        Status = LeaveStatus.Pending,
    };
    db.Leaves.Add(entity);
    await db.SaveChangesAsync();
    return Results.Created($"/api/leaves/{entity.Id}", new { id = entity.Id });
}).RequireAuthorization();

// Admin: update leave status
app.MapPut("/api/leaves/{id:guid}/status", async (Guid id, UpdateLeaveStatusRequest req, KtpDbContext db, HttpContext http) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString()))
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (u?.AssignedRoleId is Guid rid)
        {
            var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (r?.CanAccessLeavesAdmin != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    if (!Enum.TryParse<LeaveStatus>(req.status, true, out var status))
        return Results.BadRequest(new { error = "Ge�ersiz status" });
    var entity = await db.Leaves.FirstOrDefaultAsync(l => l.Id == id);
    if (entity is null) return Results.NotFound();
    entity.Status = status;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

// Admin: per-user summary for a year
app.MapGet("/api/leaves/summary", async (int? year, KtpDbContext db) =>
{
    var y = year ?? DateTime.UtcNow.Year;
    var from = new DateOnly(y, 1, 1);
    var to = new DateOnly(y, 12, 31);
    var users = await db.Users.AsNoTracking().Select(u => new { u.Id, u.Email, u.LeaveAllowanceDays }).ToListAsync();
    var approved = await db.Leaves.AsNoTracking()
        .Where(l => l.Status == LeaveStatus.Approved && l.To >= from && l.From <= to)
        .Select(l => new { l.UserId, l.From, l.To, l.FromTime, l.ToTime })
        .ToListAsync();
    const double workingDayHours = 8.0; // Saat bazl� kesinti i�in 1 g�n = 8 saat varsay�m�
    var used = approved
        .GroupBy(a => a.UserId)
        .Where(g => g.Key.HasValue)
        .ToDictionary(
            g => g.Key!.Value,
            g => g.Sum(x =>
                (x.FromTime.HasValue && x.ToTime.HasValue)
                    ? Math.Max(0.0, (x.ToTime.Value.ToTimeSpan() - x.FromTime.Value.ToTimeSpan()).TotalHours) / workingDayHours
                    : ((x.To.ToDateTime(TimeOnly.MinValue) - x.From.ToDateTime(TimeOnly.MinValue)).Days + 1)
            )
        );
    var list = users.Select(u => new
    {
        userId = u.Id,
        email = u.Email,
        allowanceDays = (int?)(u.LeaveAllowanceDays ?? 14),
        usedDays = used.TryGetValue(u.Id, out var d) ? Math.Round(d, 2) : 0.0,
    }).Select(x => new { x.userId, x.email, x.usedDays, allowanceDays = x.allowanceDays, remainingDays = Math.Round(((x.allowanceDays ?? 14) - x.usedDays), 2) });
    return Results.Ok(new { year = y, items = list });
}).RequireAuthorization();

// Admin: set user allowance
app.MapPut("/api/users/{id:guid}/leave-allowance", async (Guid id, UpdateLeaveAllowanceRequest req, KtpDbContext db, HttpContext http) =>
{
    if (req.days < 0 || req.days > 365) return Results.BadRequest(new { error = "Ge�ersiz g�n" });
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
    if (user is null) return Results.NotFound();
    user.LeaveAllowanceDays = req.days;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("AdminOnly");

// Users (admin or settings manager)
app.MapPost("/api/users", async (CreateUserRequest req, KtpDbContext db, HttpContext http) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString()))
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (u?.AssignedRoleId is Guid rid)
        {
            var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (r?.CanManageCashier != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    var email = (req.Email ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Email ve �ifre gereklidir" });
    var exists = await db.Users.AnyAsync(x => x.Email.ToLower() == email.ToLower());
    if (exists) return Results.Conflict(new { error = "Email zaten kay�tl�" });
    var user = new User
    {
        Id = Guid.NewGuid(),
        Email = email,
        PasswordHash = HashPassword(req.Password!),
        Role = req.Role
    };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Email, role = user.Role.ToString() });
}).RequireAuthorization();

// List users (admin or settings manager)
app.MapGet("/api/users", async (string? role, KtpDbContext db, HttpContext http) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString()))
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (u?.AssignedRoleId is Guid rid)
        {
            var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (r?.CanManageCashier != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    IQueryable<User> q = db.Users.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(role))
    {
        if (!Enum.TryParse<Role>(role, true, out var r))
            return Results.BadRequest(new { error = "Ge?ersiz rol" });
        q = q.Where(u => u.Role == r);
    }
    var list = await q
        .OrderBy(u => u.Email)
        .Select(u => new { id = u.Id, email = u.Email, role = u.Role.ToString() })
        .ToListAsync();
    return Results.Ok(list);
}).RequireAuthorization();

// Users + permissions (admin)
app.MapGet("/api/users/permissions", async (KtpDbContext db, HttpContext http) =>
{
    // settings management required unless admin
    var isAdmin = http.User.IsInRole(Role.Yonetici.ToString());
    if (!isAdmin)
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var uu = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (uu?.AssignedRoleId is Guid rid)
        {
            var rr = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (rr?.CanManageCashier != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    var list = await db.Users.AsNoTracking()
        .GroupJoin(db.Roles.AsNoTracking(), u => u.AssignedRoleId, r => r.Id, (u, rr) => new { u, r = rr.FirstOrDefault() })
        .OrderBy(x => x.u.Email)
        .Select(x => new {
            id = x.u.Id,
            email = x.u.Email,
            role = x.u.Role.ToString(),
            canCancelInvoice = (x.r != null && x.r.CanCancelInvoice),
            canAccessLeavesAdmin = (x.r != null && x.r.CanAccessLeavesAdmin),
            leaveAllowanceDays = x.u.LeaveAllowanceDays,
            workingDayHours = x.u.WorkingDayHours,
            assignedRoleId = x.u.AssignedRoleId,
            customRoleName = x.u.CustomRoleName
        })
        .ToListAsync();
    return Results.Ok(list);
}).RequireAuthorization();

// Roles management (admin)
app.MapGet("/api/roles", async (KtpDbContext db, HttpContext http) =>
{
    var isAdmin = http.User.IsInRole(Role.Yonetici.ToString());
    if (!isAdmin)
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var uu = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (uu?.AssignedRoleId is Guid rid)
        {
            var rr = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (rr?.CanManageCashier != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
        var list = await db.Roles.AsNoTracking()
        .OrderBy(r => r.Name)
        .Select(r => new {
            id = r.Id, name = r.Name,
            canCancelInvoice = r.CanCancelInvoice,
            canToggleKesildi = r.CanToggleKesildi,
            canAccessLeavesAdmin = r.CanAccessLeavesAdmin,
            canManageSettings = r.CanManageSettings,
            canManageCashier = r.CanManageCashier,
            canManageKarat = r.CanManageKarat,
            canUseInvoices = r.CanUseInvoices,
            canUseExpenses = r.CanUseExpenses,
            canViewReports = r.CanViewReports,
            canPrintLabels = r.CanPrintLabels,
            leaveAllowanceDays = r.LeaveAllowanceDays,
            workingDayHours = r.WorkingDayHours
        })
        .ToListAsync();
    return Results.Ok(list);
}).RequireAuthorization();

app.MapPost("/api/roles", async (RoleCreateRequest req, KtpDbContext db, HttpContext http) =>
{
    var isAdmin = http.User.IsInRole(Role.Yonetici.ToString());
    if (!isAdmin)
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var uu = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (uu?.AssignedRoleId is Guid rid)
        {
            var rr = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (rr?.CanManageCashier != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    var name = (req.name ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { error = "Rol ad� zorunludur" });
    var exists = await db.Roles.AnyAsync(r => r.Name.ToLower() == name.ToLower());
    if (exists) return Results.Conflict(new { error = "Rol ad� zaten var" });
        var role = new RoleDef
        {
            Id = Guid.NewGuid(), Name = name,
            CanCancelInvoice = req.canCancelInvoice ?? false,
            CanToggleKesildi = req.canToggleKesildi ?? false,
            CanAccessLeavesAdmin = req.canAccessLeavesAdmin ?? false,
            CanManageSettings = req.canManageSettings ?? false,
            CanManageCashier = req.canManageCashier ?? false,
            CanManageKarat = req.canManageKarat ?? false,
            CanUseInvoices = req.canUseInvoices ?? false,
            CanUseExpenses = req.canUseExpenses ?? false,
            CanViewReports = req.canViewReports ?? false,
            CanPrintLabels = req.canPrintLabels ?? false,
            LeaveAllowanceDays = req.leaveAllowanceDays,
            WorkingDayHours = req.workingDayHours
        };
    db.Roles.Add(role);
    await db.SaveChangesAsync();
    return Results.Created($"/api/roles/{role.Id}", new { id = role.Id });
}).RequireAuthorization();

app.MapPut("/api/roles/{id:guid}", async (Guid id, RoleUpdateRequest req, KtpDbContext db, HttpContext http) =>
{
    var isAdmin = http.User.IsInRole(Role.Yonetici.ToString());
    if (!isAdmin)
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var uu = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (uu?.AssignedRoleId is Guid rid)
        {
            var rr = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (rr?.CanManageCashier != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id);
    if (role is null) return Results.NotFound();
    if (!string.IsNullOrWhiteSpace(req.name))
    {
        var name = req.name.Trim();
        if (!string.Equals(role.Name, name, StringComparison.Ordinal))
        {
            var exists = await db.Roles.AnyAsync(r => r.Id != id && r.Name.ToLower() == name.ToLower());
            if (exists) return Results.Conflict(new { error = "Rol ad� zaten var" });
            role.Name = name;
        }
    }
    if (req.canCancelInvoice.HasValue) role.CanCancelInvoice = req.canCancelInvoice.Value;
    if (req.canToggleKesildi.HasValue) role.CanToggleKesildi = req.canToggleKesildi.Value;
    if (req.canAccessLeavesAdmin.HasValue) role.CanAccessLeavesAdmin = req.canAccessLeavesAdmin.Value;
    if (req.canManageSettings.HasValue) role.CanManageSettings = req.canManageSettings.Value;
    if (req.canManageCashier.HasValue) role.CanManageCashier = req.canManageCashier.Value;
    if (req.canManageKarat.HasValue) role.CanManageKarat = req.canManageKarat.Value;
    if (req.canUseInvoices.HasValue) role.CanUseInvoices = req.canUseInvoices.Value;
    if (req.canUseExpenses.HasValue) role.CanUseExpenses = req.canUseExpenses.Value;
    if (req.canViewReports.HasValue) role.CanViewReports = req.canViewReports.Value;
    if (req.canPrintLabels.HasValue) role.CanPrintLabels = req.canPrintLabels.Value;
    if (req.leaveAllowanceDays.HasValue) role.LeaveAllowanceDays = req.leaveAllowanceDays.Value;
    if (req.workingDayHours.HasValue) role.WorkingDayHours = req.workingDayHours.Value;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.MapDelete("/api/roles/{id:guid}", async (Guid id, KtpDbContext db, HttpContext http) =>
{
    var isAdmin = http.User.IsInRole(Role.Yonetici.ToString());
    if (!isAdmin)
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var uu = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (uu?.AssignedRoleId is Guid rid)
        {
            var rr = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (rr?.CanManageCashier != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id);
    if (role is null) return Results.NoContent();
    db.Roles.Remove(role);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

// Assign role to user (apply presets to user flags)
app.MapPut("/api/users/{id:guid}/assign-role", async (Guid id, AssignRoleRequest req, KtpDbContext db, HttpContext http) =>
{
    var isAdmin = http.User.IsInRole(Role.Yonetici.ToString());
    if (!isAdmin)
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var uu = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (uu?.AssignedRoleId is Guid rid)
        {
            var rr = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (rr?.CanManageCashier != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
    if (user is null) return Results.NotFound();
    if (req.roleId.HasValue)
    {
        var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == req.roleId.Value);
        if (role is null) return Results.NotFound(new { error = "Rol bulunamad�" });
        user.AssignedRoleId = role.Id;
        user.CustomRoleName = role.Name;
        // Keep allowance sync for convenience
        user.LeaveAllowanceDays = role.LeaveAllowanceDays;
        user.WorkingDayHours = role.WorkingDayHours;
    }
    else
    {
        user.AssignedRoleId = null;
        user.CustomRoleName = null;
    }
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

// Current user's profile and leave summary
app.MapGet("/api/me", async (KtpDbContext db, HttpContext http) =>
{
    var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    var email = http.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
        ?? http.User.FindFirst(ClaimTypes.Email)?.Value
        ?? http.User.FindFirst("email")?.Value;

    Guid? userId = null;
    if (Guid.TryParse(sub, out var uidParsed)) userId = uidParsed;

    var user = await db.Users.AsNoTracking()
        .FirstOrDefaultAsync(u => (userId.HasValue && u.Id == userId.Value) || (!string.IsNullOrWhiteSpace(email) && u.Email == email));

    var y = DateTime.UtcNow.Year;
    var from = new DateOnly(y, 1, 1);
    var to = new DateOnly(y, 12, 31);

    var qMine = db.Leaves.AsNoTracking()
        .Where(l => l.To >= from && l.From <= to);
    if (userId.HasValue)
        qMine = qMine.Where(l => l.UserId == userId);
    else if (!string.IsNullOrWhiteSpace(email))
        qMine = qMine.Where(l => l.UserEmail == email);
    else
        qMine = qMine.Where(l => false);

    var myLeaves = await qMine
        .Select(l => new { l.From, l.To, l.FromTime, l.ToTime, l.Status })
        .ToListAsync();

    const double workingDayHours = 8.0;
    double usedDays = myLeaves
        .Where(l => l.Status == LeaveStatus.Approved)
        .Sum(l =>
            (l.FromTime.HasValue && l.ToTime.HasValue)
                ? Math.Max(0.0, (l.ToTime.Value.ToTimeSpan() - l.FromTime.Value.ToTimeSpan()).TotalHours) / workingDayHours
                : ((l.To.ToDateTime(TimeOnly.MinValue) - l.From.ToDateTime(TimeOnly.MinValue)).Days + 1)
        );

    int allowanceDays = (int)(user?.LeaveAllowanceDays ?? 14);
    var remainingDays = Math.Round((allowanceDays - usedDays), 2);

    return Results.Ok(new
    {
        id = user?.Id ?? userId,
        email = user?.Email ?? email,
        role = user?.CustomRoleName ?? user?.Role.ToString(),
        allowanceDays,
        usedDays = Math.Round(usedDays, 2),
        remainingDays
    });
}).RequireAuthorization();

app.MapPut("/api/users/{id:guid}/permissions", async (Guid id, UpdateUserPermissionsRequest req, KtpDbContext db, HttpContext http) =>
{
    var isAdmin = http.User.IsInRole(Role.Yonetici.ToString());
    if (!isAdmin)
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var uu = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (uu?.AssignedRoleId is Guid rid)
        {
            var rr = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (rr?.CanManageCashier != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
    if (u is null) return Results.NotFound();
    // Per-user permission flags deprecated: ignore toggles moving forward
    if (req.leaveAllowanceDays.HasValue) u.LeaveAllowanceDays = req.leaveAllowanceDays.Value;
    if (req.workingDayHours.HasValue) u.WorkingDayHours = req.workingDayHours.Value;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

// Settings: Milyem Oran�
app.MapGet("/api/settings/milyem", async (KtpDbContext db) =>
{
    // K�r milyemi (�). If not set, default 0 (no markup).
    var setting = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.KeyName == "KarMilyemi");
    if (setting is null)
        setting = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.KeyName == "MilyemOrani"); // backward compat
    var val = 0.0; // default kar milyemi: 0�
    if (setting != null && double.TryParse(setting.Value, out var parsed)) val = parsed;
    return Results.Ok(new { value = val });
}).RequireAuthorization();

app.MapPut("/api/settings/milyem", async (UpdateMilyemRequest req, KtpDbContext db, HttpContext http) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString()))
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (u?.AssignedRoleId is Guid rid)
        {
            var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (r?.CanManageSettings != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    // K�r milyemi (�): 0..5000 aral���na izin ver (0..500%).
    if (req.value < 0 || req.value > 5000) return Results.BadRequest(new { error = "Ge�ersiz de�er" });
    var setting = await db.SystemSettings.FirstOrDefaultAsync(x => x.KeyName == "KarMilyemi");
    if (setting is null)
    {
        setting = new SystemSetting { Id = Guid.NewGuid(), KeyName = "KarMilyemi", Value = req.value.ToString(System.Globalization.CultureInfo.InvariantCulture), UpdatedAt = DateTime.UtcNow };
        db.SystemSettings.Add(setting);
    }
    else
    {
        setting.Value = req.value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        setting.UpdatedAt = DateTime.UtcNow;
    }
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

// Advanced calculation settings
app.MapGet("/api/settings/calc", async (KtpDbContext db) =>
{
    var settingsList = await db.SystemSettings.AsNoTracking().ToListAsync();
    T Get<T>(string key, T def, Func<string, T> parse)
    {
        var v = settingsList.FirstOrDefault(x => x.KeyName == key)?.Value;
        if (string.IsNullOrWhiteSpace(v)) return def;
        try { return parse(v!); } catch { return def; }
    }

    var resp = new
    {
        defaultKariHesapla = Get("DefaultKariHesapla", true, v => bool.Parse(v)),
        karMargin = Get("KarMargin", 0.0, v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)),
        decimalPrecision = Get("DecimalPrecision", 2, v => int.Parse(v)),
        karMilyemFormulaType = Get("KarMilyemFormulaType", "basic", v => v),
        showPercentage = Get("ShowPercentage", true, v => bool.Parse(v)),
        includeTax = Get("IncludeTax", false, v => bool.Parse(v)),
        taxRate = Get("TaxRate", 0.0, v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)),
    };

    return Results.Ok(resp);
}).RequireAuthorization();

app.MapPut("/api/settings/calc", async (UpdateCalcSettingsRequest req, KtpDbContext db, HttpContext http) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString()))
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (u?.AssignedRoleId is Guid rid)
        {
            var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (r?.CanManageSettings != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    // basic validation
    if (req.decimalPrecision < 0 || req.decimalPrecision > 6)
        return Results.BadRequest(new { error = "decimalPrecision 0..6 olmal�d�r" });
    var okTypes = new[] { "basic", "withMargin", "custom" };
    if (!okTypes.Contains(req.karMilyemFormulaType))
        return Results.BadRequest(new { error = "Ge�ersiz karMilyemFormulaType" });
    if (req.taxRate < 0 || req.taxRate > 100)
        return Results.BadRequest(new { error = "taxRate 0..100 olmal�d�r" });

    void Upsert(string key, string value)
    {
        var s = db.SystemSettings.FirstOrDefault(x => x.KeyName == key);
        if (s is null)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Id = Guid.NewGuid(),
                KeyName = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            s.Value = value;
            s.UpdatedAt = DateTime.UtcNow;
        }
    }

    var inv = System.Globalization.CultureInfo.InvariantCulture;
    Upsert("DefaultKariHesapla", req.defaultKariHesapla.ToString());
    Upsert("KarMargin", req.karMargin.ToString(inv));
    Upsert("DecimalPrecision", req.decimalPrecision.ToString());
    Upsert("KarMilyemFormulaType", req.karMilyemFormulaType);
    Upsert("ShowPercentage", req.showPercentage.ToString());
    Upsert("IncludeTax", req.includeTax.ToString());
    Upsert("TaxRate", req.taxRate.ToString(inv));

    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

// Karat difference visualization settings (ranges + alert threshold)
app.MapGet("/api/settings/karat", async (KtpDbContext db) =>
{
    var setting = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.KeyName == "KaratDiffConfig");
    if (setting is null || string.IsNullOrWhiteSpace(setting.Value))
    {
        var def = new KaratDiffSettings
        {
            ranges = new[]
            {
                new KaratRange(100, 300, "#FFF9C4"), // soft yellow
                new KaratRange(300, 500, "#FFCC80"), // orange
                new KaratRange(500, 700, "#EF9A9A"), // light red
                new KaratRange(700, 1000, "#D32F2F"), // deep red
            },
            alertThreshold = 1000
        };
        return Results.Ok(def);
    }
    try
    {
        var cfg = System.Text.Json.JsonSerializer.Deserialize<KaratDiffSettings>(setting.Value) ?? new KaratDiffSettings();
        cfg.ranges = cfg.ranges?.Where(r => r.min < r.max && !string.IsNullOrWhiteSpace(r.colorHex)).ToArray() ?? Array.Empty<KaratRange>();
        return Results.Ok(cfg);
    }
    catch
    {
        var def = new KaratDiffSettings
        {
            ranges = new[]
            {
                new KaratRange(100, 300, "#FFF9C4"),
                new KaratRange(300, 500, "#FFCC80"),
                new KaratRange(500, 700, "#EF9A9A"),
                new KaratRange(700, 1000, "#D32F2F"),
            },
            alertThreshold = 1000
        };
        return Results.Ok(def);
    }
}).RequireAuthorization();

app.MapPut("/api/settings/karat", async (UpdateKaratSettingsRequest req, KtpDbContext db, HttpContext http) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString()))
    {
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return Results.Forbid();
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (u?.AssignedRoleId is Guid rid)
        {
            var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (r?.CanManageKarat != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }
    if (req.ranges is null || req.ranges.Length == 0)
        return Results.BadRequest(new { error = "ranges bo� olamaz" });
    foreach (var r in req.ranges)
    {
        if (r.min < 0 || r.max <= r.min) return Results.BadRequest(new { error = "Aral�klar ge�ersiz" });
        if (string.IsNullOrWhiteSpace(r.colorHex) || !r.colorHex.StartsWith('#') || (r.colorHex.Length != 7))
            return Results.BadRequest(new { error = "Renk hex #RRGGBB olmal�d�r" });
    }
    if (req.alertThreshold < 0) return Results.BadRequest(new { error = "alertThreshold >= 0 olmal�d�r" });

    var json = System.Text.Json.JsonSerializer.Serialize(new KaratDiffSettings { ranges = req.ranges, alertThreshold = req.alertThreshold });
    var set = await db.SystemSettings.FirstOrDefaultAsync(x => x.KeyName == "KaratDiffConfig");
    if (set is null)
    {
        set = new SystemSetting { Id = Guid.NewGuid(), KeyName = "KaratDiffConfig", Value = json, UpdatedAt = DateTime.UtcNow };
        db.SystemSettings.Add(set);
    }
    else
    {
        set.Value = json;
        set.UpdatedAt = DateTime.UtcNow;
    }
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

// Current user: minimal permissions for client nav
app.MapGet("/api/me/permissions", async (HttpContext http, KtpDbContext db) =>
{
    var sub = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    if (!Guid.TryParse(sub, out var uid)) return Results.Unauthorized();
    var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
    if (u is null) return Results.NotFound();
    var isAdmin = u.Role == Role.Yonetici;
    bool canCancel = false, canToggle = false, canLeaves = false, canManageSettings = false, canManageCashier = false, canManageKarat = false, canUseInv = false, canUseExp = false, canViewReports = false, canPrintLabels = false;
    string displayRole = u.CustomRoleName ?? u.Role.ToString();
    if (isAdmin)
    {
        canCancel = canToggle = canLeaves = canManageSettings = canManageCashier = canManageKarat = canUseInv = canUseExp = canViewReports = canPrintLabels = true;
    }
    else if (u.AssignedRoleId is Guid rid)
    {
        var r = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
        if (r != null)
        {
            canCancel = r.CanCancelInvoice;
            canToggle = r.CanToggleKesildi;
            canLeaves = r.CanAccessLeavesAdmin;
            canManageSettings = r.CanManageSettings;
            canManageCashier = r.CanManageCashier;
            canManageKarat = r.CanManageKarat;
            canUseInv = r.CanUseInvoices;
            canUseExp = r.CanUseExpenses;
            canViewReports = r.CanViewReports;
            canPrintLabels = r.CanPrintLabels;
        }
    }
    return Results.Ok(new { role = displayRole, canCancelInvoice = canCancel, canToggleKesildi = canToggle, canAccessLeavesAdmin = canLeaves, canManageSettings, canManageCashier, canManageKarat, canUseInvoices = canUseInv, canUseExpenses = canUseExp, canViewReports, canPrintLabels });
}).RequireAuthorization();

// Reset password (admin only)
app.MapPut("/api/users/{id:guid}/password", async (Guid id, ResetPasswordRequest req, KtpDbContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
    if (user is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(req.Password)) return Results.BadRequest(new { error = "?Yifre gereklidir" });
    user.PasswordHash = HashPassword(req.Password!);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("AdminOnly");

// Bootstrap first admin if none exists (one-time)
app.MapPost("/api/users/bootstrap", async (CreateUserRequest req, KtpDbContext db) =>
{
    var any = await db.Users.AnyAsync();
    if (any) return Results.BadRequest(new { error = "Kullan�c�lar zaten mevcut" });
    if (req.Role != Role.Yonetici) return Results.BadRequest(new { error = "�lk kullan�c� Y�netici olmal�" });
    var user = new User
    {
        Id = Guid.NewGuid(),
        Email = (req.Email ?? string.Empty).Trim(),
        PasswordHash = HashPassword(req.Password ?? string.Empty),
        Role = Role.Yonetici
    };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Email, role = user.Role.ToString() });
});

app.MapPost("/print/multi", async (PrintMultiRequest request, IPrintQueueService queueService, CancellationToken cancellationToken) =>
{
    var sourceValues = request.Values ?? new List<string>();

    var payload = sourceValues
        .Select(v => v.Trim())
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .ToList();

    if (payload.Count == 0)
    {
        return Results.BadRequest(new { message = "En az bir gramaj değeri giriniz." });
    }

    var zpls = payload.Select(ZplTemplate.Build);
    await queueService.EnqueueAsync(zpls, cancellationToken);
    return Results.Ok(new { count = payload.Count });
})
.WithName("PrintMultiLabels")
.Accepts<PrintMultiRequest>(MediaTypeNames.Application.Json)
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.Run();

static async Task EnsureMarketSchemaAsync(MarketDbContext db)
{
    var sql = @"CREATE SCHEMA IF NOT EXISTS market;
CREATE TABLE IF NOT EXISTS market.""PriceSettings"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""Code"" varchar(32) NOT NULL UNIQUE,
    ""MarginBuy"" numeric(18,2) NOT NULL,
    ""MarginSell"" numeric(18,2) NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL
);
CREATE TABLE IF NOT EXISTS market.""PriceRecords"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""Code"" varchar(32) NOT NULL,
    ""Alis"" numeric(18,3) NOT NULL,
    ""Satis"" numeric(18,3) NOT NULL,
    ""SourceTime"" timestamptz NOT NULL,
    ""FinalAlis"" numeric(18,3) NOT NULL,
    ""FinalSatis"" numeric(18,3) NOT NULL,
    ""CreatedAt"" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_PriceSettings_Code ON market.""PriceSettings"" (""Code"");
CREATE UNIQUE INDEX IF NOT EXISTS IX_PriceRecords_Code_SourceTime ON market.""PriceRecords"" (""Code"", ""SourceTime"");
CREATE TABLE IF NOT EXISTS market.""GoldFeedAlerts"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""Message"" text NOT NULL,
    ""Level"" varchar(32) NOT NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""ResolvedAt"" timestamptz NULL
);
CREATE TABLE IF NOT EXISTS market.""GoldFeedEntries"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""Payload"" text NOT NULL,
    ""MetaTarih"" varchar(100),
    ""Language"" varchar(16),
    ""FetchedAt"" timestamptz NOT NULL,
    ""SourceTime"" timestamptz
);
CREATE INDEX IF NOT EXISTS IX_GoldFeedEntries_FetchedAt ON market.""GoldFeedEntries"" (""FetchedAt"");
CREATE TABLE IF NOT EXISTS market.""InvoiceGoldSnapshots"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""InvoiceId"" uuid NOT NULL UNIQUE,
    ""Code"" varchar(32) NOT NULL,
    ""FinalSatis"" numeric(18,3) NOT NULL,
    ""SourceTime"" timestamptz NOT NULL,
    ""CreatedAt"" timestamptz NOT NULL
);
CREATE TABLE IF NOT EXISTS market.""GlobalGoldPrices"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""Price"" numeric(18,3) NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""UpdatedById"" uuid NULL,
    ""UpdatedByEmail"" varchar(200) NULL
);
CREATE INDEX IF NOT EXISTS IX_GlobalGoldPrices_UpdatedAt ON market.""GlobalGoldPrices"" (""UpdatedAt"");
";
    await db.Database.ExecuteSqlRawAsync(sql);
}
static async Task EnsureUsersSchemaAsync(KtpDbContext db)
{
    var sql = @"CREATE TABLE IF NOT EXISTS ""Users"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""Email"" varchar(200) NOT NULL UNIQUE,
    ""PasswordHash"" text NOT NULL,
    ""Role"" int NOT NULL
);";
    await db.Database.ExecuteSqlRawAsync(sql);
}

static async Task EnsureCustomersMigratedAsync(KtpDbContext db, CancellationToken ct)
{
    var customers = await db.Customers.ToListAsync(ct);
    var customerMap = customers.ToDictionary(c => CustomerUtil.NormalizeTckn(c.TCKN), c => c);

    var invoices = await db.Invoices.AsNoTracking()
        .Select(x => new { x.Id, x.TCKN, x.MusteriAdSoyad, x.Tarih, x.CustomerId })
        .ToListAsync(ct);
    var expenses = await db.Expenses.AsNoTracking()
        .Select(x => new { x.Id, x.TCKN, x.MusteriAdSoyad, x.Tarih, x.CustomerId })
        .ToListAsync(ct);

    void Upsert(string? tcknRaw, string? nameRaw, DateOnly tarih)
    {
        var tckn = CustomerUtil.NormalizeTckn(tcknRaw);
        if (string.IsNullOrWhiteSpace(tckn)) return;
        var normalizedName = CustomerUtil.NormalizeName(nameRaw);
        var txTime = DateTime.SpecifyKind(tarih.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        if (!customerMap.TryGetValue(tckn, out var cust))
        {
            cust = new Customer
            {
                Id = Guid.NewGuid(),
                AdSoyad = normalizedName,
                NormalizedAdSoyad = normalizedName,
                TCKN = tckn,
                CreatedAt = txTime,
                LastTransactionAt = txTime
            };
            db.Customers.Add(cust);
            customerMap[tckn] = cust;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(normalizedName) && !string.Equals(cust.AdSoyad, normalizedName, StringComparison.Ordinal))
            {
                cust.AdSoyad = normalizedName;
                cust.NormalizedAdSoyad = normalizedName;
            }
            if (!cust.LastTransactionAt.HasValue || cust.LastTransactionAt.Value < txTime)
                cust.LastTransactionAt = txTime;
        }
    }

    foreach (var inv in invoices)
    {
        Upsert(inv.TCKN, inv.MusteriAdSoyad, inv.Tarih);
    }
    foreach (var exp in expenses)
    {
        Upsert(exp.TCKN, exp.MusteriAdSoyad, exp.Tarih);
    }

    await db.SaveChangesAsync(ct);

    var invsToUpdate = await db.Invoices.Where(x => x.CustomerId == null && x.TCKN != null).ToListAsync(ct);
    foreach (var inv in invsToUpdate)
    {
        var key = CustomerUtil.NormalizeTckn(inv.TCKN);
        if (customerMap.TryGetValue(key, out var cust))
        {
            inv.CustomerId = cust.Id;
            inv.MusteriAdSoyad = cust.AdSoyad;
            inv.TCKN = cust.TCKN;
        }
    }

    var expsToUpdate = await db.Expenses.Where(x => x.CustomerId == null && x.TCKN != null).ToListAsync(ct);
    foreach (var exp in expsToUpdate)
    {
        var key = CustomerUtil.NormalizeTckn(exp.TCKN);
        if (customerMap.TryGetValue(key, out var cust))
        {
            exp.CustomerId = cust.Id;
            exp.MusteriAdSoyad = cust.AdSoyad;
            exp.TCKN = cust.TCKN;
        }
    }

    await db.SaveChangesAsync(ct);
}

static string IssueJwt(User user, string issuer, string audience, string key, TimeSpan lifetime)
{
    var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role.ToString())
    };
    var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
    var jwt = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        notBefore: DateTime.UtcNow,
        expires: DateTime.UtcNow.Add(lifetime),
        signingCredentials: creds
    );
    return new JwtSecurityTokenHandler().WriteToken(jwt);
}

static string HashPassword(string password)
{
    using var rng = RandomNumberGenerator.Create();
    var salt = new byte[16];
    rng.GetBytes(salt);
    var iterations = 10000;
    using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
    var hash = pbkdf2.GetBytes(32);
    return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
}

static bool VerifyPassword(string password, string stored)
{
    try
    {
        var parts = stored.Split('.');
        if (parts.Length != 3) return false;
        var iterations = int.Parse(parts[0]);
        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var actual = pbkdf2.GetBytes(32);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
    catch { return false; }
}

static async Task<bool> HasCustomerLookupPermissionAsync(HttpContext http, KtpDbContext db, CancellationToken ct)
{
    if (http.User.IsInRole(Role.Yonetici.ToString())) return true;
    var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    if (!Guid.TryParse(sub, out var uid)) return false;
    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
    if (user?.AssignedRoleId is Guid rid)
    {
        var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid, ct);
        return role?.CanUseInvoices == true || role?.CanUseExpenses == true;
    }
    return false;
}

// Request models
public record LeaveCreateRequest(string from, string to, string? reason, string? fromTime, string? toTime);
public record UpdateLeaveStatusRequest(string status);
public record UpdateLeaveAllowanceRequest(int days);
public record UpdateUserPermissionsRequest(bool? canCancelInvoice, bool? canAccessLeavesAdmin, int? leaveAllowanceDays, double? workingDayHours);
public record RoleCreateRequest(
    string name,
    bool? canCancelInvoice,
    bool? canToggleKesildi,
    bool? canAccessLeavesAdmin,
    bool? canManageSettings,
    bool? canManageCashier,
    bool? canManageKarat,
    bool? canUseInvoices,
    bool? canUseExpenses,
    bool? canViewReports,
    bool? canPrintLabels,
    int? leaveAllowanceDays,
    double? workingDayHours);
public record RoleUpdateRequest(
    string? name,
    bool? canCancelInvoice,
    bool? canToggleKesildi,
    bool? canAccessLeavesAdmin,
    bool? canManageSettings,
    bool? canManageCashier,
    bool? canManageKarat,
    bool? canUseInvoices,
    bool? canUseExpenses,
    bool? canViewReports,
    bool? canPrintLabels,
    int? leaveAllowanceDays,
    double? workingDayHours);
public record AssignRoleRequest(Guid? roleId);
public record UpdateMilyemRequest(double value);
public record UpdateCalcSettingsRequest(
    bool defaultKariHesapla,
    double karMargin,
    int decimalPrecision,
    string karMilyemFormulaType,
    bool showPercentage,
    bool includeTax,
    double taxRate
);

public record LoginRequest(string Email, string Password);
public record CreateUserRequest(string Email, string Password, Role Role);
public record ResetPasswordRequest(string Password);
public record UpdateInvoiceStatusRequest(bool Kesildi);
public record FinalizeRequest(decimal UrunFiyati);
public record GoldPriceUpdateRequest(decimal Price);

public record KaratRange(double min, double max, string colorHex);
public record KaratDiffSettings
{
    public KaratRange[] ranges { get; set; } = Array.Empty<KaratRange>();
    public double alertThreshold { get; set; } = 1000;
}
public record UpdateKaratSettingsRequest(KaratRange[] ranges, double alertThreshold);

public record PrintMultiRequest([property: Required] List<string> Values);
