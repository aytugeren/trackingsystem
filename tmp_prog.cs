using KuyumculukTakipProgrami.Application;
using KuyumculukTakipProgrami.Infrastructure;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using KuyumculukTakipProgrami.Application.Invoices;
using KuyumculukTakipProgrami.Application.Expenses;
using KuyumculukTakipProgrami.Domain.Entities.Market;
using System.Globalization;
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

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
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
        // System settings
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"SystemSettings\" (\"Id\" uuid PRIMARY KEY, \"KeyName\" varchar(100) NOT NULL, \"Value\" varchar(100) NOT NULL, \"UpdatedAt\" timestamptz NOT NULL DEFAULT now());");
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_SystemSettings_KeyName ON \"SystemSettings\" (\"KeyName\");");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Leaves\" ADD COLUMN IF NOT EXISTS \"Status\" integer NOT NULL DEFAULT 0;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Leaves\" ADD COLUMN IF NOT EXISTS \"FromTime\" time NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Leaves\" ADD COLUMN IF NOT EXISTS \"ToTime\" time NULL;");
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
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN " +
            "IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Expenses_KasiyerId_Users') THEN " +
            "ALTER TABLE \"Expenses\" ADD CONSTRAINT \"FK_Expenses_KasiyerId_Users\" FOREIGN KEY (\"KasiyerId\") REFERENCES \"Users\"(\"Id\") ON DELETE SET NULL; " +
            "END IF; END $$;");
        await EnsureMarketSchemaAsync(marketDb);
        if (db.Database.CanConnect())
        {
            Console.WriteLine("Database Connected ?");
        }
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
            Console.WriteLine($"Seeded default admin user: {adminEmail}");
        }
        else if (app.Environment.IsDevelopment() && !VerifyPassword("72727361Aa", existingAdmin.PasswordHash))
        {
            // In development, keep default admin password in sync for convenience
            existingAdmin.PasswordHash = HashPassword("72727361Aa");
            await db.SaveChangesAsync();
            Console.WriteLine($"Reset default admin password for: {adminEmail}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database initialization failed: {ex.Message}");
    }
}

// Invoices
app.MapPost("/api/invoices", async (CreateInvoiceDto dto, ICreateInvoiceHandler handler, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    try
    {
        var sub = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        var email = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value;
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

// Update invoice status (admin only)
app.MapPut("/api/invoices/{id:guid}/status", async (Guid id, UpdateInvoiceStatusRequest body, KtpDbContext db) =>
{
    var inv = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id);
    if (inv is null) return Results.NotFound();
    inv.Kesildi = body.Kesildi;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Invoices").RequireAuthorization("AdminOnly");

// Finalize invoice: compute fields and set as Kesildi
app.MapPost("/api/invoices/{id:guid}/finalize", async (Guid id, KtpDbContext db) =>
{
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
    // Kesildi durumu kasiyerden değil, sadece dashboarddan değiştirilecek
    await db.SaveChangesAsync();
    return Results.Ok(new { inv.Id, inv.SafAltinDegeri, inv.UrunFiyati, inv.YeniUrunFiyati, inv.GramDegeri, inv.Iscilik, inv.Kesildi });
}).WithTags("Invoices").RequireAuthorization();

app.MapGet("/api/invoices", async (int? page, int? pageSize, KtpDbContext db, IMemoryCache cache, CancellationToken ct) =>
{
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
        var sub = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        var email = http.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value;
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



// Update expense status (admin only)
app.MapPut("/api/expenses/{id:guid}/status", async (Guid id, UpdateInvoiceStatusRequest body, KtpDbContext db) =>
{
    var exp = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id);
    if (exp is null) return Results.NotFound();
    exp.Kesildi = body.Kesildi;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Expenses").RequireAuthorization("AdminOnly");

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
    // Kesildi durumu kasiyerden değil, sadece dashboarddan değiştirilecek
    await db.SaveChangesAsync();
    return Results.Ok(new { exp.Id, exp.SafAltinDegeri, exp.UrunFiyati, exp.YeniUrunFiyati, exp.GramDegeri, exp.Iscilik, exp.Kesildi });
}).WithTags("Expenses").RequireAuthorization();

app.MapGet("/api/expenses", async (int? page, int? pageSize, KtpDbContext db, IMemoryCache cache, CancellationToken ct) =>
{
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
app.MapPost("/api/cashier/invoices/draft", async (CreateInvoiceDto dto, KtpDbContext db, MarketDbContext mdb, IHttpClientFactory httpFactory, IConfiguration cfg, HttpContext http, CancellationToken ct) =>
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

        var entity = new Invoice
        {
            Id = Guid.NewGuid(),
            Tarih = dto.Tarih,
            SiraNo = next,
            MusteriAdSoyad = dto.MusteriAdSoyad,
            TCKN = dto.TCKN,
            Tutar = dto.Tutar,
            OdemeSekli = dto.OdemeSekli,
            AltinAyar = dto.AltinAyar,
            KasiyerId = currentUserId,
            CreatedById = currentUserId,
            CreatedByEmail = string.IsNullOrWhiteSpace(email) ? null : email,
            Kesildi = false
        };

        // Stamp current ALTIN final sell price
        decimal? finalSatisFromLive = null; DateTime? sourceTimeFromLive = null;
        try
        {
            var url = cfg["Pricing:FeedUrl"] ?? "https://canlipiyasalar.haremaltin.com/tmp/altin.json";
            var lang = cfg["Pricing:LanguageParam"] ?? "tr";
            var client = httpFactory.CreateClient();
            using var resp = await client.GetAsync($"{url}?dil_kodu={lang}", ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                if (TryParseAltin(json, out var alis, out var satis, out var srcTime))
                {
                    var setting = await mdb.PriceSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Code == "ALTIN", ct)
                                  ?? new PriceSetting { Code = "ALTIN", MarginBuy = 0, MarginSell = 0 };
                    var finalSatis = satis + setting.MarginSell;
                    finalSatisFromLive = finalSatis; sourceTimeFromLive = srcTime;
                    var exists = await mdb.PriceRecords.AnyAsync(x => x.Code == "ALTIN" && x.SourceTime == srcTime, ct);
                    if (!exists)
                    {
                        mdb.PriceRecords.Add(new PriceRecord
                        {
                            Id = Guid.NewGuid(), Code = "ALTIN", Alis = alis, Satis = satis,
                            SourceTime = DateTime.SpecifyKind(srcTime, DateTimeKind.Utc),
                            FinalAlis = alis + setting.MarginBuy, FinalSatis = finalSatis, CreatedAt = DateTime.UtcNow
                        });
                        await mdb.SaveChangesAsync(ct);
                    }
                }
            }
        }
        catch { }
        if (finalSatisFromLive.HasValue)
            entity.AltinSatisFiyati = finalSatisFromLive.Value;
        else
        {
            var latestStored = await mdb.PriceRecords
                .Where(x => x.Code == "ALTIN")
                .OrderByDescending(x => x.SourceTime).ThenByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (latestStored is not null)
                entity.AltinSatisFiyati = latestStored.FinalSatis;
        }

        db.Invoices.Add(entity);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Created($"/api/invoices/{entity.Id}", new { id = entity.Id, siraNo = entity.SiraNo, altinSatisFiyati = entity.AltinSatisFiyati });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { errors = ex.Message.Split(" | ") });
    }
}).WithTags("Invoices").RequireAuthorization();

// Cashier: create draft expense with definitive, gapless SiraNo
app.MapPost("/api/cashier/expenses/draft", async (CreateExpenseDto dto, KtpDbContext db, MarketDbContext mdb, IHttpClientFactory httpFactory, IConfiguration cfg, HttpContext http, CancellationToken ct) =>
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

        var entity = new Expense
        {
            Id = Guid.NewGuid(), Tarih = dto.Tarih, SiraNo = next,
            MusteriAdSoyad = dto.MusteriAdSoyad, TCKN = dto.TCKN, Tutar = dto.Tutar,
            AltinAyar = dto.AltinAyar, KasiyerId = currentUserId,
            CreatedById = currentUserId, CreatedByEmail = string.IsNullOrWhiteSpace(email) ? null : email,
            Kesildi = false
        };

        decimal? finalSatisFromLive = null;
        try
        {
            var url = cfg["Pricing:FeedUrl"] ?? "https://canlipiyasalar.haremaltin.com/tmp/altin.json";
            var lang = cfg["Pricing:LanguageParam"] ?? "tr";
            var client = httpFactory.CreateClient();
            using var resp = await client.GetAsync($"{url}?dil_kodu={lang}", ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                if (TryParseAltin(json, out var alis, out var satis, out var srcTime))
                {
                    var setting = await mdb.PriceSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Code == "ALTIN", ct)
                                  ?? new PriceSetting { Code = "ALTIN", MarginBuy = 0, MarginSell = 0 };
                    // For expenses, apply buy margin as a discount from spot sell price
                    var effective = satis - setting.MarginBuy;
                    if (effective < 0) effective = 0;
                    finalSatisFromLive = effective;
                    var exists = await mdb.PriceRecords.AnyAsync(x => x.Code == "ALTIN" && x.SourceTime == srcTime, ct);
                    if (!exists)
                    {
                        mdb.PriceRecords.Add(new PriceRecord
                        {
                            Id = Guid.NewGuid(), Code = "ALTIN", Alis = alis, Satis = satis,
                            SourceTime = DateTime.SpecifyKind(srcTime, DateTimeKind.Utc),
                            FinalAlis = alis + setting.MarginBuy, FinalSatis = satis + setting.MarginSell, CreatedAt = DateTime.UtcNow
                        });
                        await mdb.SaveChangesAsync(ct);
                    }
                }
            }
        }
        catch { }
        if (finalSatisFromLive.HasValue)
            entity.AltinSatisFiyati = finalSatisFromLive.Value;
        else
        {
            var latestStored = await mdb.PriceRecords
                .Where(x => x.Code == "ALTIN")
                .OrderByDescending(x => x.SourceTime).ThenByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (latestStored is not null)
            {
                var setting = await mdb.PriceSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Code == "ALTIN", ct)
                              ?? new PriceSetting { Code = "ALTIN", MarginBuy = 0, MarginSell = 0 };
                var eff = latestStored.Satis - setting.MarginBuy;
                if (eff < 0) eff = 0;
                entity.AltinSatisFiyati = eff;
            }
        }

        db.Expenses.Add(entity);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Created($"/api/expenses/{entity.Id}", new { id = entity.Id, siraNo = entity.SiraNo, altinSatisFiyati = entity.AltinSatisFiyati });
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
    if (inv is null) return Results.NotFound();
    if (inv.Kesildi) return Results.BadRequest(new { error = "Kesilmiş kayıt silinemez" });
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
    if (exp is null) return Results.NotFound();
    if (exp.Kesildi) return Results.BadRequest(new { error = "Kesilmiş kayıt silinemez" });
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

// Current pricing (final sell) for ALTIN
app.MapGet("/api/pricing/current", async (MarketDbContext mdb, CancellationToken ct) =>
{
    var latest = await mdb.PriceRecords
        .AsNoTracking()
        .OrderByDescending(x => x.SourceTime)
        .ThenByDescending(x => x.CreatedAt)
        .FirstOrDefaultAsync(ct);
    if (latest is null) return Results.NotFound();
    return Results.Ok(new { code = latest.Code, finalSatis = latest.FinalSatis, sourceTime = latest.SourceTime });
}).WithTags("Pricing").RequireAuthorization();

// Pricing settings endpoints
app.MapGet("/api/pricing/settings/{code}", async (string code, MarketDbContext mdb) =>
{
    code = code.ToUpperInvariant();
    var s = await mdb.PriceSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code);
    s ??= new PriceSetting { Code = code, MarginBuy = 0, MarginSell = 0 };
    return Results.Ok(new { code = s.Code, marginBuy = s.MarginBuy, marginSell = s.MarginSell });
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

// Fetch and store ALTIN
app.MapPost("/api/pricing/refresh", async (IHttpClientFactory httpFactory, IConfiguration cfg, MarketDbContext mdb, CancellationToken ct) =>
{
    var url = cfg["Pricing:FeedUrl"] ?? "https://canlipiyasalar.haremaltin.com/tmp/altin.json";
    var lang = cfg["Pricing:LanguageParam"] ?? "tr";
    var client = httpFactory.CreateClient();
    var resp = await client.GetAsync($"{url}?dil_kodu={lang}", ct);
    if (!resp.IsSuccessStatusCode) return Results.Problem("Feed ulaşılmıyor", statusCode: 502);
    var json = await resp.Content.ReadAsStringAsync(ct);

    if (!TryParseAltin(json, out var alis, out var satis, out var sourceTime))
        return Results.Problem("ALTIN verisi bulunamadı", statusCode: 422);

    var setting = await mdb.PriceSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Code == "ALTIN", ct)
                  ?? new PriceSetting { Code = "ALTIN", MarginBuy = 0, MarginSell = 0 };

    var rec = new PriceRecord
    {
        Id = Guid.NewGuid(),
        Code = "ALTIN",
        Alis = alis,
        Satis = satis,
        SourceTime = DateTime.SpecifyKind(sourceTime, DateTimeKind.Utc),
        FinalAlis = alis + setting.MarginBuy,
        FinalSatis = satis + setting.MarginSell,
        CreatedAt = DateTime.UtcNow
    };
    var exists = await mdb.PriceRecords.AnyAsync(x => x.Code == rec.Code && x.SourceTime == rec.SourceTime, ct);
    if (!exists)
    {
        mdb.PriceRecords.Add(rec);
        await mdb.SaveChangesAsync(ct);
    }
    return Results.Ok(new
    {
        code = rec.Code,
        alis = rec.Alis,
        satis = rec.Satis,
        finalAlis = rec.FinalAlis,
        finalSatis = rec.FinalSatis,
        sourceTime = rec.SourceTime
    });
}).WithTags("Pricing");

app.MapGet("/api/pricing/{code}/latest", async (string code, MarketDbContext mdb, CancellationToken ct) =>
{
    code = code.ToUpperInvariant();
    var rec = await mdb.PriceRecords.Where(x => x.Code == code)
        .OrderByDescending(x => x.SourceTime)
        .ThenByDescending(x => x.CreatedAt)
        .FirstOrDefaultAsync(ct);
    if (rec is null) return Results.NotFound();
    return Results.Ok(new
    {
        code = rec.Code,
        alis = rec.Alis,
        satis = rec.Satis,
        finalAlis = rec.FinalAlis,
        finalSatis = rec.FinalSatis,
        sourceTime = rec.SourceTime
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
app.MapGet("/api/leaves", async (string? from, string? to, KtpDbContext db) =>
{
    DateOnly fromDo;
    DateOnly toDo;
    if (!string.IsNullOrWhiteSpace(from))
    {
        if (DateTime.TryParse(from, out var fdt)) fromDo = DateOnly.FromDateTime(fdt);
        else if (!DateOnly.TryParse(from, out fromDo)) return Results.BadRequest(new { error = "Geçersiz from" });
    }
    else fromDo = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));

    if (!string.IsNullOrWhiteSpace(to))
    {
        if (DateTime.TryParse(to, out var tdt)) toDo = DateOnly.FromDateTime(tdt);
        else if (!DateOnly.TryParse(to, out toDo)) return Results.BadRequest(new { error = "Geçersiz to" });
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
    if (DateTime.TryParse(req.from, out var fromDt)) from = DateOnly.FromDateTime(fromDt); else if (!DateOnly.TryParse(req.from, out from)) return Results.BadRequest(new { error = "Geçersiz from" });
    if (DateTime.TryParse(req.to, out var toDt)) to = DateOnly.FromDateTime(toDt); else if (!DateOnly.TryParse(req.to, out to)) return Results.BadRequest(new { error = "Geçersiz to" });
    if (to < from)
        return Results.BadRequest(new { error = "Bitiş tarihi başlangıçtan önce olamaz" });

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
        if (!TimeOnly.TryParse(req.fromTime, out var ft)) return Results.BadRequest(new { error = "Geçersiz fromTime" });
        fromTime = ft;
    }
    if (!string.IsNullOrWhiteSpace(req.toTime))
    {
        if (!TimeOnly.TryParse(req.toTime, out var tt)) return Results.BadRequest(new { error = "Geçersiz toTime" });
        toTime = tt;
    }
    if (fromTime.HasValue || toTime.HasValue)
    {
        // Saat aralığı sadece tek gün için desteklenir
        if (from != to) return Results.BadRequest(new { error = "Saatli izin sadece tek gün için geçerlidir" });
        if (!fromTime.HasValue || !toTime.HasValue || toTime.Value <= fromTime.Value)
            return Results.BadRequest(new { error = "Geçersiz saat aralığı" });
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
app.MapPut("/api/leaves/{id:guid}/status", async (Guid id, UpdateLeaveStatusRequest req, KtpDbContext db) =>
{
    if (!Enum.TryParse<LeaveStatus>(req.status, true, out var status))
        return Results.BadRequest(new { error = "Geçersiz status" });
    var entity = await db.Leaves.FirstOrDefaultAsync(l => l.Id == id);
    if (entity is null) return Results.NotFound();
    entity.Status = status;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("AdminOnly");

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
    const double workingDayHours = 8.0; // Saat bazlı kesinti için 1 gün = 8 saat varsayımı
    var used = approved
        .GroupBy(a => a.UserId)
        .ToDictionary(
            g => g.Key,
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
}).RequireAuthorization("AdminOnly");

// Admin: set user allowance
app.MapPut("/api/users/{id:guid}/leave-allowance", async (Guid id, UpdateLeaveAllowanceRequest req, KtpDbContext db) =>
{
    if (req.days < 0 || req.days > 365) return Results.BadRequest(new { error = "Geçersiz gün" });
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
    if (user is null) return Results.NotFound();
    user.LeaveAllowanceDays = req.days;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("AdminOnly");

// Users (admin only)
app.MapPost("/api/users", async (CreateUserRequest req, KtpDbContext db) =>
{
    var email = (req.Email ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Email ve şifre gereklidir" });
    var exists = await db.Users.AnyAsync(x => x.Email.ToLower() == email.ToLower());
    if (exists) return Results.Conflict(new { error = "Email zaten kayıtlı" });
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
}).RequireAuthorization("AdminOnly");

// List users (admin only)
app.MapGet("/api/users", async (string? role, KtpDbContext db) =>
{
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
}).RequireAuthorization("AdminOnly");

// Users + permissions (admin)
app.MapGet("/api/users/permissions", async (KtpDbContext db) =>
{
    var list = await db.Users.AsNoTracking()
        .OrderBy(u => u.Email)
        .Select(u => new {
            id = u.Id,
            email = u.Email,
            role = u.Role.ToString(),
            canCancelInvoice = u.CanCancelInvoice,
            canAccessLeavesAdmin = u.CanAccessLeavesAdmin,
            leaveAllowanceDays = u.LeaveAllowanceDays
        })
        .ToListAsync();
    return Results.Ok(list);
}).RequireAuthorization("AdminOnly");

app.MapPut("/api/users/{id:guid}/permissions", async (Guid id, UpdateUserPermissionsRequest req, KtpDbContext db) =>
{
    var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
    if (u is null) return Results.NotFound();
    if (req.canCancelInvoice.HasValue) u.CanCancelInvoice = req.canCancelInvoice.Value;
    if (req.canAccessLeavesAdmin.HasValue) u.CanAccessLeavesAdmin = req.canAccessLeavesAdmin.Value;
    if (req.leaveAllowanceDays.HasValue) u.LeaveAllowanceDays = req.leaveAllowanceDays.Value;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("AdminOnly");

// Settings: Milyem Oranı
app.MapGet("/api/settings/milyem", async (KtpDbContext db) =>
{
    // Kâr milyemi (‰). If not set, default 0 (no markup).
    var s = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.KeyName == "KarMilyemi");
    if (s is null)
        s = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.KeyName == "MilyemOrani"); // backward compat
    var val = 0.0; // default kar milyemi: 0‰
    if (s != null && double.TryParse(s.Value, out var parsed)) val = parsed;
    return Results.Ok(new { value = val });
}).RequireAuthorization();

app.MapPut("/api/settings/milyem", async (UpdateMilyemRequest req, KtpDbContext db) =>
{
    // Kâr milyemi (‰): 0..5000 aralığına izin ver (0..500%).
    if (req.value < 0 || req.value > 5000) return Results.BadRequest(new { error = "Geçersiz değer" });
    var s = await db.SystemSettings.FirstOrDefaultAsync(x => x.KeyName == "KarMilyemi");
    if (s is null)
    {
        s = new SystemSetting { Id = Guid.NewGuid(), KeyName = "KarMilyemi", Value = req.value.ToString(System.Globalization.CultureInfo.InvariantCulture), UpdatedAt = DateTime.UtcNow };
        db.SystemSettings.Add(s);
    }
    else
    {
        s.Value = req.value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        s.UpdatedAt = DateTime.UtcNow;
    }
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("AdminOnly");

// Advanced calculation settings
app.MapGet("/api/settings/calc", async (KtpDbContext db) =>
{
    var s = await db.SystemSettings.AsNoTracking().ToListAsync();
    T Get<T>(string key, T def, Func<string, T> parse)
    {
        var v = s.FirstOrDefault(x => x.KeyName == key)?.Value;
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

app.MapPut("/api/settings/calc", async (UpdateCalcSettingsRequest req, KtpDbContext db) =>
{
    // basic validation
    if (req.decimalPrecision < 0 || req.decimalPrecision > 6)
        return Results.BadRequest(new { error = "decimalPrecision 0..6 olmalıdır" });
    var okTypes = new[] { "basic", "withMargin", "custom" };
    if (!okTypes.Contains(req.karMilyemFormulaType))
        return Results.BadRequest(new { error = "Geçersiz karMilyemFormulaType" });
    if (req.taxRate < 0 || req.taxRate > 100)
        return Results.BadRequest(new { error = "taxRate 0..100 olmalıdır" });

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
}).RequireAuthorization("AdminOnly");

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
    if (any) return Results.BadRequest(new { error = "Kullanıcılar zaten mevcut" });
    if (req.Role != Role.Yonetici) return Results.BadRequest(new { error = "İlk kullanıcı Yönetici olmalı" });
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

app.Run();

static bool TryParseAltin(string json, out decimal alis, out decimal satis, out DateTime sourceTime)
{
    alis = 0; satis = 0; sourceTime = DateTime.UtcNow;
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var data = root.GetProperty("data");
        if (!data.TryGetProperty("ALTIN", out var altin)) return false;
        var alisStr = altin.GetProperty("alis").ToString();
        var satisStr = altin.GetProperty("satis").ToString();
        var tarihStr = altin.GetProperty("tarih").GetString();
        var ci = CultureInfo.InvariantCulture;
        alis = decimal.Parse(alisStr, ci);
        satis = decimal.Parse(satisStr, ci);
        // Kaynaktaki tarih yerel (TR) saat olarak geliyor; UTC'ye çevir.
        if (!DateTime.TryParseExact(
                tarihStr,
                "dd-MM-yyyy HH:mm:ss",
                CultureInfo.GetCultureInfo("tr-TR"),
                DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal,
                out sourceTime))
        {
            sourceTime = DateTime.UtcNow;
        }
        return true;
    }
    catch
    {
        return false;
    }
}
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
CREATE TABLE IF NOT EXISTS market.""InvoiceGoldSnapshots"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""InvoiceId"" uuid NOT NULL UNIQUE,
    ""Code"" varchar(32) NOT NULL,
    ""FinalSatis"" numeric(18,3) NOT NULL,
    ""SourceTime"" timestamptz NOT NULL,
    ""CreatedAt"" timestamptz NOT NULL
);";
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

// Request models
public record LeaveCreateRequest(string from, string to, string? reason, string? fromTime, string? toTime);
public record UpdateLeaveStatusRequest(string status);
public record UpdateLeaveAllowanceRequest(int days);
public record UpdateUserPermissionsRequest(bool? canCancelInvoice, bool? canAccessLeavesAdmin, int? leaveAllowanceDays);
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














