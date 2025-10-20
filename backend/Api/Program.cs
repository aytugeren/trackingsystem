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
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"AltinSatisFiyati\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"AltinAyar\" integer NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"CreatedById\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"CreatedByEmail\" varchar(200) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"Kesildi\" boolean NOT NULL DEFAULT false;");
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
    

    var hasAltin = inv.AltinSatisFiyati!.Value;
    var safAltin = inv.AltinAyar == AltinAyar.Ayar22 ? hasAltin * 0.916m : hasAltin * 0.995m;
    var yeniUrun = inv.AltinAyar == AltinAyar.Ayar22 ? inv.Tutar * 0.99m : inv.Tutar * 0.998m;
    var gram = (safAltin == 0 ? 0 : yeniUrun / safAltin);
    var iscilik = (inv.Tutar - (gram * safAltin)) / 1.20m;

    inv.UrunFiyati = Math.Round(inv.Tutar, 2);
    inv.SafAltinDegeri = Math.Round(safAltin, 3);
    inv.YeniUrunFiyati = Math.Round(yeniUrun, 3);
    inv.GramDegeri = Math.Round(gram, 3);
    inv.Iscilik = Math.Round(iscilik, 3);
    inv.Kesildi = true;
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
                            kesildi = i.Kesildi,
                            kasiyerAdSoyad = (u != null ? u.Email : null)
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
    

    var hasAltin = exp.AltinSatisFiyati!.Value;
    var safAltin = exp.AltinAyar == AltinAyar.Ayar22 ? hasAltin * 0.916m : hasAltin * 0.995m;
    var yeniUrun = exp.AltinAyar == AltinAyar.Ayar22 ? exp.Tutar * 0.99m : exp.Tutar * 0.998m;
    var gram = (safAltin == 0 ? 0 : yeniUrun / safAltin);
    var iscilik = (exp.Tutar - (gram * safAltin)) / 1.20m;

    exp.UrunFiyati = Math.Round(exp.Tutar, 2);
    exp.SafAltinDegeri = Math.Round(safAltin, 3);
    exp.YeniUrunFiyati = Math.Round(yeniUrun, 3);
    exp.GramDegeri = Math.Round(gram, 3);
    exp.Iscilik = Math.Round(iscilik, 3);
    exp.Kesildi = true;
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
                            kesildi = e.Kesildi,
                            kasiyerAdSoyad = (u != null ? u.Email : null)
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
    if (!resp.IsSuccessStatusCode) return Results.Problem("Feed ulaþýlmýyor", statusCode: 502);
    var json = await resp.Content.ReadAsStringAsync(ct);

    if (!TryParseAltin(json, out var alis, out var satis, out var sourceTime))
        return Results.Problem("ALTIN verisi bulunamadý", statusCode: 422);

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

// Users (admin only)
app.MapPost("/api/users", async (CreateUserRequest req, KtpDbContext db) =>
{
    var email = (req.Email ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Email ve þifre gereklidir" });
    var exists = await db.Users.AnyAsync(x => x.Email.ToLower() == email.ToLower());
    if (exists) return Results.Conflict(new { error = "Email zaten kayýtlý" });
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
    if (any) return Results.BadRequest(new { error = "Kullanýcýlar zaten mevcut" });
    if (req.Role != Role.Yonetici) return Results.BadRequest(new { error = "Ýlk kullanýcý Yönetici olmalý" });
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

public record LoginRequest(string Email, string Password);
public record CreateUserRequest(string Email, string Password, Role Role);
public record ResetPasswordRequest(string Password);
public record UpdateInvoiceStatusRequest(bool Kesildi);
public record FinalizeRequest(decimal UrunFiyati);













