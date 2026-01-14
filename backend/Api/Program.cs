using KuyumculukTakipProgrami.Api.Services;
using KuyumculukTakipProgrami.Api;

using KuyumculukTakipProgrami.Application;
using KuyumculukTakipProgrami.Infrastructure;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using KuyumculukTakipProgrami.Infrastructure.Pricing;
using KuyumculukTakipProgrami.Infrastructure.Util;
using KuyumculukTakipProgrami.Infrastructure.Integration.Turmob;
using Microsoft.EntityFrameworkCore;
using KuyumculukTakipProgrami.Application.Invoices;
using KuyumculukTakipProgrami.Application.Expenses;
using KuyumculukTakipProgrami.Application.Interfaces;
using KuyumculukTakipProgrami.Application.Gold;
using KuyumculukTakipProgrami.Application.Gold.Formula;
using KuyumculukTakipProgrami.Domain.Entities.Market;
using System.Globalization;
using System.Threading;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using KuyumculukTakipProgrami.Domain.Entities;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Net.Mime;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// TURMOB endpoint rejects older TLS versions.
ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

var defaultPreviewTheme = new FormulaPreviewTheme
{
    Title = "Hesap Özeti",
    Fields = new List<FormulaPreviewField>
    {
        new() { Key = "unitHasPriceUsed", Label = "Has Altın Fiyatı", Format = "currency" },
        new() { Key = "amount", Label = "Tutar", Format = "currency" },
        new() { Key = "gram", Label = "Gram", Format = "number" },
        new() { Key = "laborNet", Label = "İşçilik (KDV'siz)", Format = "currency" }
    }
};

var previewJsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

var builder = WebApplication.CreateBuilder(args);
// Logging: allow env-configured minimum level (fallback to Error)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var logLevelRaw =
    builder.Configuration["Logging:LogLevel:Default"] ??
    builder.Configuration["Serilog:MinimumLevel"] ??
    builder.Configuration["SERILOG:MINIMUMLEVEL"];
var minimumLevel = LogLevel.Error;
if (!string.IsNullOrWhiteSpace(logLevelRaw) && Enum.TryParse(logLevelRaw, true, out LogLevel parsedLevel))
{
    minimumLevel = parsedLevel;
}
builder.Logging.SetMinimumLevel(minimumLevel);
builder.Logging.AddFilter("KuyumculukTakipProgrami.Infrastructure.Pricing.GoldPriceFeedService", LogLevel.Information);

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
var jwtValidateLifetime = jwtSection.GetValue<bool?>("ValidateLifetime") ?? true;

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
        ValidateLifetime = jwtValidateLifetime,
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
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"CompanyInfos\" (\"Id\" uuid PRIMARY KEY, \"CompanyName\" varchar(200) NULL, \"TaxNo\" varchar(20) NULL, \"Address\" varchar(500) NULL, \"TradeRegistryNo\" varchar(100) NULL, \"Phone\" varchar(40) NULL, \"Email\" varchar(200) NULL, \"CityName\" varchar(100) NULL, \"TownName\" varchar(100) NULL, \"PostalCode\" varchar(20) NULL, \"TaxOfficeName\" varchar(100) NULL, \"UpdatedAt\" timestamptz NOT NULL DEFAULT now());");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"CompanyInfos\" ADD COLUMN IF NOT EXISTS \"Email\" varchar(200) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"CompanyInfos\" ADD COLUMN IF NOT EXISTS \"CityName\" varchar(100) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"CompanyInfos\" ADD COLUMN IF NOT EXISTS \"TownName\" varchar(100) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"CompanyInfos\" ADD COLUMN IF NOT EXISTS \"PostalCode\" varchar(20) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"CompanyInfos\" ADD COLUMN IF NOT EXISTS \"TaxOfficeName\" varchar(100) NULL;");
        // Opening inventory table (accounting baseline)
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"GoldOpeningInventories\" (\"Id\" uuid PRIMARY KEY, \"Date\" timestamptz NOT NULL, \"Karat\" integer NOT NULL, \"Gram\" numeric(18,3) NOT NULL, \"Description\" varchar(250) NULL, \"CreatedAt\" timestamptz NOT NULL DEFAULT now());");
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_GoldOpeningInventories_Karat\" ON \"GoldOpeningInventories\" (\"Karat\");");
        // Product catalog + opening inventories
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"Products\" (\"Id\" uuid PRIMARY KEY, \"Code\" varchar(100) NOT NULL, \"Name\" varchar(200) NOT NULL, \"IsActive\" boolean NOT NULL DEFAULT true, \"ShowInSales\" boolean NOT NULL DEFAULT true, \"AccountingType\" integer NOT NULL DEFAULT 0, \"Gram\" numeric(18,3) NULL, \"CreatedAt\" timestamptz NOT NULL DEFAULT now(), \"UpdatedAt\" timestamptz NOT NULL DEFAULT now(), \"CreatedUserId\" uuid NULL, \"UpdatedUserId\" uuid NULL);");
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Products_Code\" ON \"Products\" (\"Code\");");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Products\" ADD COLUMN IF NOT EXISTS \"ShowInSales\" boolean NOT NULL DEFAULT true;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Products\" ADD COLUMN IF NOT EXISTS \"AccountingType\" integer NOT NULL DEFAULT 0;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Products\" ADD COLUMN IF NOT EXISTS \"Gram\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"ProductOpeningInventories\" (\"Id\" uuid PRIMARY KEY, \"ProductId\" uuid NOT NULL, \"Date\" timestamptz NOT NULL, \"Quantity\" numeric(18,3) NOT NULL, \"CreatedAt\" timestamptz NOT NULL DEFAULT now(), \"UpdatedAt\" timestamptz NOT NULL DEFAULT now(), \"CreatedUserId\" uuid NULL, \"UpdatedUserId\" uuid NULL);");
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ProductOpeningInventories_ProductId\" ON \"ProductOpeningInventories\" (\"ProductId\");");
        await db.Database.ExecuteSqlRawAsync("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_ProductOpeningInventories_ProductId_Products') THEN ALTER TABLE \"ProductOpeningInventories\" ADD CONSTRAINT \"FK_ProductOpeningInventories_ProductId_Products\" FOREIGN KEY (\"ProductId\") REFERENCES \"Products\"(\"Id\") ON DELETE CASCADE; END IF; END $$;");
        // Categories and category-product mapping
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"Categories\" (\"Id\" uuid PRIMARY KEY, \"Name\" varchar(200) NOT NULL, \"ParentId\" uuid NULL, \"CreatedAt\" timestamptz NOT NULL DEFAULT now(), \"UpdatedAt\" timestamptz NOT NULL DEFAULT now(), \"CreatedUserId\" uuid NULL, \"UpdatedUserId\" uuid NULL);");
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Categories_Name\" ON \"Categories\" (\"Name\");");
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Categories_ParentId\" ON \"Categories\" (\"ParentId\");");
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN " +
            "IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Categories_ParentId_Categories') THEN " +
            "ALTER TABLE \"Categories\" ADD CONSTRAINT \"FK_Categories_ParentId_Categories\" FOREIGN KEY (\"ParentId\") REFERENCES \"Categories\"(\"Id\") ON DELETE SET NULL; " +
            "END IF; END $$;");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"CategoryProducts\" (\"Id\" uuid PRIMARY KEY, \"CategoryId\" uuid NOT NULL, \"ProductId\" uuid NOT NULL, \"CreatedAt\" timestamptz NOT NULL DEFAULT now(), \"UpdatedAt\" timestamptz NOT NULL DEFAULT now(), \"CreatedUserId\" uuid NULL, \"UpdatedUserId\" uuid NULL);");
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_CategoryProducts_CategoryId_ProductId\" ON \"CategoryProducts\" (\"CategoryId\", \"ProductId\");");
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN " +
            "IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_CategoryProducts_CategoryId_Categories') THEN " +
            "ALTER TABLE \"CategoryProducts\" ADD CONSTRAINT \"FK_CategoryProducts_CategoryId_Categories\" FOREIGN KEY (\"CategoryId\") REFERENCES \"Categories\"(\"Id\") ON DELETE CASCADE; " +
            "END IF; END $$;");
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN " +
            "IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_CategoryProducts_ProductId_Products') THEN " +
            "ALTER TABLE \"CategoryProducts\" ADD CONSTRAINT \"FK_CategoryProducts_ProductId_Products\" FOREIGN KEY (\"ProductId\") REFERENCES \"Products\"(\"Id\") ON DELETE CASCADE; " +
            "END IF; END $$;");
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
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"ProductId\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"CreatedById\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"CreatedByEmail\" varchar(200) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Invoices\" ADD COLUMN IF NOT EXISTS \"IsForCompany\" boolean NOT NULL DEFAULT false;");
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
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN " +
            "IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Invoices_ProductId_Products') THEN " +
            "ALTER TABLE \"Invoices\" ADD CONSTRAINT \"FK_Invoices_ProductId_Products\" FOREIGN KEY (\"ProductId\") REFERENCES \"Products\"(\"Id\") ON DELETE SET NULL; " +
            "END IF; END $$;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"CreatedById\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"CreatedByEmail\" varchar(200) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"IsForCompany\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"KasiyerId\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"AltinSatisFiyati\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"AltinAyar\" integer NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"ProductId\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"SafAltinDegeri\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"UrunFiyati\" numeric(18,2) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"YeniUrunFiyati\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"GramDegeri\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"Iscilik\" numeric(18,3) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"Kesildi\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Expenses\" ADD COLUMN IF NOT EXISTS \"FinalizedAt\" timestamp with time zone NULL;");
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN " +
            "IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Expenses_ProductId_Products') THEN " +
            "ALTER TABLE \"Expenses\" ADD CONSTRAINT \"FK_Expenses_ProductId_Products\" FOREIGN KEY (\"ProductId\") REFERENCES \"Products\"(\"Id\") ON DELETE SET NULL; " +
            "END IF; END $$;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Products\" ADD COLUMN IF NOT EXISTS \"RequiresFormula\" boolean NOT NULL DEFAULT true;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Products\" ADD COLUMN IF NOT EXISTS \"DefaultFormulaId\" uuid NULL;");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"GoldFormulaTemplates\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"Code\" varchar(100) NOT NULL UNIQUE, " +
            "\"Name\" varchar(200) NOT NULL, " +
            "\"Scope\" integer NOT NULL, " +
            "\"FormulaType\" integer NOT NULL, " +
            "\"DslVersion\" integer NOT NULL, " +
            "\"DefinitionJson\" text NOT NULL, " +
            "\"IsActive\" boolean NOT NULL DEFAULT true, " +
            "\"CreatedAt\" timestamptz NOT NULL DEFAULT now()" +
            ");");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"GoldProductFormulaBindings\" (" +
            "\"Id\" uuid PRIMARY KEY, " +
            "\"GoldProductId\" uuid NOT NULL, " +
            "\"FormulaTemplateId\" uuid NOT NULL, " +
            "\"Direction\" integer NOT NULL, " +
            "\"IsActive\" boolean NOT NULL DEFAULT true" +
            ");");
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_GoldProductFormulaBindings_Product_Direction_Active\" ON \"GoldProductFormulaBindings\" (\"GoldProductId\", \"Direction\", \"IsActive\");");
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_GoldProductFormulaBindings_Template\" ON \"GoldProductFormulaBindings\" (\"FormulaTemplateId\");");
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN " +
            "IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_GoldProductFormulaBindings_Product') THEN " +
            "ALTER TABLE \"GoldProductFormulaBindings\" ADD CONSTRAINT \"FK_GoldProductFormulaBindings_Product\" FOREIGN KEY (\"GoldProductId\") REFERENCES \"Products\"(\"Id\") ON DELETE CASCADE; " +
            "END IF; END $$;");
        await db.Database.ExecuteSqlRawAsync(
            "DO $$ BEGIN " +
            "IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_GoldProductFormulaBindings_Template') THEN " +
            "ALTER TABLE \"GoldProductFormulaBindings\" ADD CONSTRAINT \"FK_GoldProductFormulaBindings_Template\" FOREIGN KEY (\"FormulaTemplateId\") REFERENCES \"GoldFormulaTemplates\"(\"Id\") ON DELETE CASCADE; " +
            "END IF; END $$;");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"Customers\" (\"Id\" uuid PRIMARY KEY, \"AdSoyad\" varchar(150) NOT NULL, \"NormalizedAdSoyad\" varchar(160) NOT NULL, \"TCKN\" varchar(11) NOT NULL, \"IsCompany\" boolean NOT NULL DEFAULT false, \"VknNo\" varchar(10) NULL, \"CompanyName\" varchar(200) NULL, \"Phone\" varchar(40) NULL, \"Email\" varchar(200) NULL, \"CreatedAt\" timestamptz NOT NULL DEFAULT now(), \"LastTransactionAt\" timestamptz NULL);");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Customers\" ADD COLUMN IF NOT EXISTS \"IsCompany\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Customers\" ADD COLUMN IF NOT EXISTS \"VknNo\" varchar(10) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Customers\" ADD COLUMN IF NOT EXISTS \"CompanyName\" varchar(200) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Customers\" ADD COLUMN IF NOT EXISTS \"Phone\" varchar(40) NULL;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Customers\" ADD COLUMN IF NOT EXISTS \"Email\" varchar(200) NULL;");
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Customers_TCKN\" ON \"Customers\" (\"TCKN\");");
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Customers_NormalizedAdSoyad\" ON \"Customers\" (\"NormalizedAdSoyad\");");
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Customers_VknNo\" ON \"Customers\" (\"VknNo\");");
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
app.MapPost("/api/invoices/{id:guid}/finalize", async (Guid id, KtpDbContext db, IGoldFormulaEngine engine, HttpContext http) =>
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
    if (!inv.AltinAyar.HasValue)
    {
        decimal R2NoAyar(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
        inv.UrunFiyati = R2NoAyar(inv.Tutar);
        inv.SafAltinDegeri = null;
        inv.YeniUrunFiyati = null;
        inv.GramDegeri = null;
        inv.Iscilik = null;
        inv.FinalizedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(new { inv.Id, inv.SafAltinDegeri, inv.UrunFiyati, inv.YeniUrunFiyati, inv.GramDegeri, inv.Iscilik, inv.Kesildi });
    }
    if (!inv.AltinSatisFiyati.HasValue)
        return Results.BadRequest(new { error = "Alt?n sat?? fiyat? bulunamad?." });
    

    decimal R2(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
    var hasAltin = inv.AltinSatisFiyati!.Value;
    var templateCode = inv.AltinAyar == AltinAyar.Ayar22 ? "DEFAULT_22_SALE" : "DEFAULT_24_SALE";
    var template = await db.GoldFormulaTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Code == templateCode && x.IsActive);
    if (template is not null)
    {
        var product = inv.ProductId.HasValue
            ? await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == inv.ProductId.Value)
            : null;
        var context = new GoldFormulaContext(
            inv.Tutar,
            hasAltin,
            0.20m,
            product?.AccountingType ?? ProductAccountingType.Gram,
            product?.Gram,
            GoldFormulaDirection.Sale,
            GoldFormulaOperationType.Invoice,
            hasAltin);

        try
        {
            var eval = engine.Evaluate(template.DefinitionJson, context, GoldFormulaMode.Finalize);
            inv.UrunFiyati = eval.Result.Amount;
            inv.SafAltinDegeri = eval.Result.UnitHasPriceUsed;
            inv.YeniUrunFiyati = TryGetVariable(eval.UsedVariables, "yeniUrun");
            inv.GramDegeri = eval.Result.Gram;
            inv.Iscilik = eval.Result.LaborNet;
            inv.FinalizedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { inv.Id, inv.SafAltinDegeri, inv.UrunFiyati, inv.YeniUrunFiyati, inv.GramDegeri, inv.Iscilik, inv.Kesildi });
        }
        catch (ArgumentException)
        {
            // fallback to legacy calculation
        }
    }
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

// Update invoice preview fields
app.MapPut("/api/invoices/{id:guid}/preview", async (Guid id, UpdateInvoicePreviewRequest body, KtpDbContext db, HttpContext http) =>
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

    if (body.Tutar <= 0) return Results.BadRequest(new { error = "Tutar 0'dan büyük olmalı." });

    var inv = await db.Invoices.FirstOrDefaultAsync(x => x.Id == id);
    if (inv is null) return Results.NotFound();
    decimal R2(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
    decimal R3(decimal x) => Math.Round(x, 3, MidpointRounding.AwayFromZero);

    if (body.AltinAyar.HasValue)
    {
        var ayarValue = body.AltinAyar.Value;
        if (ayarValue != 22 && ayarValue != 24)
        {
            return Results.BadRequest(new { error = "Altın ayarı geçersiz." });
        }
        inv.AltinAyar = ayarValue == 22 ? AltinAyar.Ayar22 : AltinAyar.Ayar24;
    }

    if (!inv.AltinAyar.HasValue || !inv.AltinSatisFiyati.HasValue)
    {
        var tutarNoAyar = R2(body.Tutar);
        inv.Tutar = tutarNoAyar;
        inv.UrunFiyati = tutarNoAyar;
        inv.SafAltinDegeri = null;
        inv.YeniUrunFiyati = null;
        inv.GramDegeri = null;
        inv.Iscilik = null;
        await db.SaveChangesAsync();
        return Results.Ok(new
        {
            inv.Id,
            tutar = inv.Tutar,
            safAltinDegeri = inv.SafAltinDegeri,
            urunFiyati = inv.UrunFiyati,
            yeniUrunFiyati = inv.YeniUrunFiyati,
            gramDegeri = inv.GramDegeri,
            iscilik = inv.Iscilik,
            altinAyar = inv.AltinAyar.HasValue ? (int?)inv.AltinAyar.Value : null
        });
    }

    var hasAltin = inv.AltinSatisFiyati!.Value;
    var safOran = inv.AltinAyar == AltinAyar.Ayar22 ? 0.916m : 0.995m;
    var yeniOran = inv.AltinAyar == AltinAyar.Ayar22 ? 0.99m : 0.998m;
    var safAltin = R2(hasAltin * safOran);
    var tutar = R2(body.Tutar);
    var gramMode = string.Equals(body.Mode, "gram", StringComparison.OrdinalIgnoreCase);

    decimal gram;
    decimal yeniUrun;
    if (gramMode)
    {
        gram = R3(body.GramDegeri);
        yeniUrun = R3(gram * safAltin);
    }
    else
    {
        yeniUrun = R3(tutar * yeniOran);
        gram = safAltin == 0 ? 0 : R3(yeniUrun / safAltin);
    }

    var altinHizmet = R2(gram * safAltin);
    var iscilikKdvli = R2(R2(tutar) - altinHizmet);
    var iscilik = R3(iscilikKdvli / 1.20m);

    inv.Tutar = tutar;
    inv.UrunFiyati = tutar;
    inv.SafAltinDegeri = safAltin;
    inv.YeniUrunFiyati = yeniUrun;
    inv.GramDegeri = gram;
    inv.Iscilik = iscilik;
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        inv.Id,
        tutar = inv.Tutar,
        safAltinDegeri = inv.SafAltinDegeri,
        urunFiyati = inv.UrunFiyati,
        yeniUrunFiyati = inv.YeniUrunFiyati,
        gramDegeri = inv.GramDegeri,
        iscilik = inv.Iscilik,
        altinAyar = inv.AltinAyar.HasValue ? (int?)inv.AltinAyar.Value : null
    });
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
                        join c in db.Customers.AsNoTracking() on i.CustomerId equals c.Id into cc
                        from c in cc.DefaultIfEmpty()
                        join prod in db.Products.AsNoTracking() on i.ProductId equals prod.Id into pp
                        from prod in pp.DefaultIfEmpty()
                        select new
                        {
                            id = i.Id,
                            tarih = i.Tarih,
                            siraNo = i.SiraNo,
                            customerId = i.CustomerId,
                            musteriAdSoyad = i.MusteriAdSoyad,
                            tckn = i.TCKN,
                            isForCompany = i.IsForCompany,
                            isCompany = c != null && c.IsCompany,
                            vknNo = c != null ? c.VknNo : null,
                            companyName = c != null ? c.CompanyName : null,
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
                            kasiyerAdSoyad = (u != null ? u.Email : i.CreatedByEmail),
                            productId = i.ProductId,
                            productName = prod != null ? prod.Name : null
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

// TURMOB preview and send
app.MapPost("/api/turmob/invoices/{id:guid}/preview", async (
    Guid id,
    TurmobInvoiceBuilder builder,
    TurmobInvoiceMapper mapper,
    IOptionsMonitor<TurmobOptions> options,
    CancellationToken ct) =>
{
    var dto = await builder.BuildAsync(id, ct);
    if (dto is null) return Results.NotFound();

    var environment = options.CurrentValue.GetSelectedEnvironment() ?? new TurmobEnvironmentOptions();

    try
    {
        var xml = dto.IsArchive
            ? mapper.MapToArchiveInvoiceXml(dto, environment)
            : mapper.MapToInvoiceXml(dto, environment);
        var action = dto.IsArchive ? "SendArchiveInvoice" : "SendInvoice";

        return Results.Ok(new { action, xml });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "XML preview failed.", detail = ex.Message });
    }
}).WithTags("Turmob").RequireAuthorization();

app.MapPost("/api/turmob/invoices/{id:guid}/send", async (
    Guid id,
    TurmobInvoiceBuilder builder,
    ITurmobInvoiceGateway gateway,
    CancellationToken ct) =>
{
    var dto = await builder.BuildAsync(id, ct);
    if (dto is null) return Results.NotFound();

    var result = await gateway.SendAsync(dto);
    return Results.Ok(result);
}).WithTags("Turmob").RequireAuthorization();

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
app.MapPost("/api/expenses/{id:guid}/finalize", async (Guid id, KtpDbContext db, IGoldFormulaEngine engine) =>
{
    var exp = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id);
    if (exp is null) return Results.NotFound();
    if (!exp.AltinAyar.HasValue)
    {
        decimal R2eNoAyar(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
        exp.UrunFiyati = R2eNoAyar(exp.Tutar);
        exp.SafAltinDegeri = null;
        exp.YeniUrunFiyati = null;
        exp.GramDegeri = null;
        exp.Iscilik = null;
        exp.FinalizedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(new { exp.Id, exp.SafAltinDegeri, exp.UrunFiyati, exp.YeniUrunFiyati, exp.GramDegeri, exp.Iscilik, exp.Kesildi });
    }
    if (!exp.AltinSatisFiyati.HasValue)
        return Results.BadRequest(new { error = "Alt?n sat?? fiyat? bulunamad?." });
    

    decimal R2e(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
    var hasAltin = exp.AltinSatisFiyati!.Value;
    var templateCode = exp.AltinAyar == AltinAyar.Ayar22 ? "DEFAULT_22_PURCHASE" : "DEFAULT_24_PURCHASE";
    var template = await db.GoldFormulaTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Code == templateCode && x.IsActive);
    if (template is not null)
    {
        var product = exp.ProductId.HasValue
            ? await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == exp.ProductId.Value)
            : null;
        var context = new GoldFormulaContext(
            exp.Tutar,
            hasAltin,
            0.20m,
            product?.AccountingType ?? ProductAccountingType.Gram,
            product?.Gram,
            GoldFormulaDirection.Purchase,
            GoldFormulaOperationType.Expense,
            hasAltin);

        try
        {
            var eval = engine.Evaluate(template.DefinitionJson, context, GoldFormulaMode.Finalize);
            exp.UrunFiyati = eval.Result.Amount;
            exp.SafAltinDegeri = eval.Result.UnitHasPriceUsed;
            exp.YeniUrunFiyati = TryGetVariable(eval.UsedVariables, "yeniUrun");
            exp.GramDegeri = eval.Result.Gram;
            exp.Iscilik = eval.Result.LaborNet;
            exp.FinalizedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { exp.Id, exp.SafAltinDegeri, exp.UrunFiyati, exp.YeniUrunFiyati, exp.GramDegeri, exp.Iscilik, exp.Kesildi });
        }
        catch (ArgumentException)
        {
            // fallback to legacy calculation
        }
    }
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

// Update expense preview fields
app.MapPut("/api/expenses/{id:guid}/preview", async (Guid id, UpdateExpensePreviewRequest body, KtpDbContext db, HttpContext http) =>
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

    if (body.Tutar <= 0) return Results.BadRequest(new { error = "Tutar 0'dan büyük olmalı." });

    var exp = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id);
    if (exp is null) return Results.NotFound();
    decimal R2e(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
    decimal R3e(decimal x) => Math.Round(x, 3, MidpointRounding.AwayFromZero);

    if (body.AltinAyar.HasValue)
    {
        var ayarValue = body.AltinAyar.Value;
        if (ayarValue != 22 && ayarValue != 24)
        {
            return Results.BadRequest(new { error = "Altın ayarı geçersiz." });
        }
        exp.AltinAyar = ayarValue == 22 ? AltinAyar.Ayar22 : AltinAyar.Ayar24;
    }

    if (!exp.AltinAyar.HasValue || !exp.AltinSatisFiyati.HasValue)
    {
        var tutarNoAyar = R2e(body.Tutar);
        exp.Tutar = tutarNoAyar;
        exp.UrunFiyati = tutarNoAyar;
        exp.SafAltinDegeri = null;
        exp.YeniUrunFiyati = null;
        exp.GramDegeri = null;
        exp.Iscilik = null;
        await db.SaveChangesAsync();
        return Results.Ok(new
        {
            exp.Id,
            tutar = exp.Tutar,
            safAltinDegeri = exp.SafAltinDegeri,
            urunFiyati = exp.UrunFiyati,
            yeniUrunFiyati = exp.YeniUrunFiyati,
            gramDegeri = exp.GramDegeri,
            iscilik = exp.Iscilik,
            altinAyar = exp.AltinAyar.HasValue ? (int?)exp.AltinAyar.Value : null
        });
    }

    var hasAltin = exp.AltinSatisFiyati!.Value;
    var safOran = exp.AltinAyar == AltinAyar.Ayar22 ? 0.916m : 0.995m;
    var yeniOran = exp.AltinAyar == AltinAyar.Ayar22 ? 0.99m : 0.998m;
    var safAltin = R2e(hasAltin * safOran);
    var tutar = R2e(body.Tutar);
    var gramMode = string.Equals(body.Mode, "gram", StringComparison.OrdinalIgnoreCase);

    decimal gram;
    decimal yeniUrun;
    if (gramMode)
    {
        gram = R3e(body.GramDegeri);
        yeniUrun = R3e(gram * safAltin);
    }
    else
    {
        yeniUrun = R3e(tutar * yeniOran);
        gram = safAltin == 0 ? 0 : R3e(yeniUrun / safAltin);
    }

    var altinHizmet = R2e(gram * safAltin);
    var iscilikKdvli = R2e(R2e(tutar) - altinHizmet);
    var iscilik = R3e(iscilikKdvli / 1.20m);

    exp.Tutar = tutar;
    exp.UrunFiyati = tutar;
    exp.SafAltinDegeri = safAltin;
    exp.YeniUrunFiyati = yeniUrun;
    exp.GramDegeri = gram;
    exp.Iscilik = iscilik;
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        exp.Id,
        tutar = exp.Tutar,
        safAltinDegeri = exp.SafAltinDegeri,
        urunFiyati = exp.UrunFiyati,
        yeniUrunFiyati = exp.YeniUrunFiyati,
        gramDegeri = exp.GramDegeri,
        iscilik = exp.Iscilik,
        altinAyar = exp.AltinAyar.HasValue ? (int?)exp.AltinAyar.Value : null
    });
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
                        join c in db.Customers.AsNoTracking() on e.CustomerId equals c.Id into cc
                        from c in cc.DefaultIfEmpty()
                        join prod in db.Products.AsNoTracking() on e.ProductId equals prod.Id into pp
                        from prod in pp.DefaultIfEmpty()
                        select new
                        {
                            id = e.Id,
                            tarih = e.Tarih,
                            siraNo = e.SiraNo,
                            customerId = e.CustomerId,
                            musteriAdSoyad = e.MusteriAdSoyad,
                            tckn = e.TCKN,
                            isForCompany = e.IsForCompany,
                            isCompany = c != null && c.IsCompany,
                            vknNo = c != null ? c.VknNo : null,
                            companyName = c != null ? c.CompanyName : null,
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
                            kasiyerAdSoyad = (u != null ? u.Email : e.CreatedByEmail),
                            productId = e.ProductId,
                            productName = prod != null ? prod.Name : null
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

app.MapGet("/api/dashboard/summary", async (
    string? mode,
    string? years,
    string? months,
    string? day,
    KtpDbContext db,
    IMemoryCache cache,
    HttpContext http,
    CancellationToken ct) =>
{
    // Permission: admin or role.CanUseInvoices + role.CanUseExpenses
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
            if (r?.CanUseInvoices != true || r?.CanUseExpenses != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }

    var normalizedMode = string.IsNullOrWhiteSpace(mode) ? "all" : mode.Trim().ToLowerInvariant();
    if (normalizedMode != "all" && normalizedMode != "yearly" && normalizedMode != "monthly" && normalizedMode != "daily")
    {
        normalizedMode = "all";
    }

    var yearList = (years ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => int.TryParse(x, out var y) ? (int?)y : null)
        .Where(x => x.HasValue && x.Value > 0)
        .Select(x => x!.Value)
        .Distinct()
        .OrderBy(x => x)
        .ToList();
    var yearSet = new HashSet<int>(yearList);

    var monthKeys = new HashSet<int>();
    var monthList = (months ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var m in monthList)
    {
        var parts = m.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) continue;
        if (!int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var mo)) continue;
        if (y <= 0 || mo < 1 || mo > 12) continue;
        monthKeys.Add((y * 100) + mo);
    }

    DateOnly? dayFilter = null;
    if (!string.IsNullOrWhiteSpace(day) && DateOnly.TryParse(day, out var parsedDay))
    {
        dayFilter = parsedDay;
    }

    var yearsKey = yearSet.Count > 0 ? string.Join("-", yearSet.OrderBy(x => x)) : "all";
    var monthsKey = monthKeys.Count > 0 ? string.Join("-", monthKeys.OrderBy(x => x)) : "all";
    var dayKey = dayFilter.HasValue ? dayFilter.Value.ToString("yyyy-MM-dd") : "all";
    var cacheKey = $"dashboard:summary:{normalizedMode}:{yearsKey}:{monthsKey}:{dayKey}";

    if (!cache.TryGetValue(cacheKey, out object? cached))
    {
        IQueryable<Invoice> invQuery = db.Invoices.AsNoTracking();
        IQueryable<Expense> expQuery = db.Expenses.AsNoTracking();

        switch (normalizedMode)
        {
            case "daily":
                if (dayFilter.HasValue)
                {
                    invQuery = invQuery.Where(x => x.Tarih == dayFilter.Value);
                    expQuery = expQuery.Where(x => x.Tarih == dayFilter.Value);
                }
                break;
            case "monthly":
                if (monthKeys.Count > 0)
                {
                    invQuery = invQuery.Where(x => monthKeys.Contains((x.Tarih.Year * 100) + x.Tarih.Month));
                    expQuery = expQuery.Where(x => monthKeys.Contains((x.Tarih.Year * 100) + x.Tarih.Month));
                }
                else if (yearSet.Count > 0)
                {
                    invQuery = invQuery.Where(x => yearSet.Contains(x.Tarih.Year));
                    expQuery = expQuery.Where(x => yearSet.Contains(x.Tarih.Year));
                }
                break;
            case "yearly":
                if (yearSet.Count > 0)
                {
                    invQuery = invQuery.Where(x => yearSet.Contains(x.Tarih.Year));
                    expQuery = expQuery.Where(x => yearSet.Contains(x.Tarih.Year));
                }
                break;
        }

        var income = await invQuery.Select(x => (decimal?)x.Tutar).SumAsync(ct) ?? 0m;
        var outgo = await expQuery.Select(x => (decimal?)x.Tutar).SumAsync(ct) ?? 0m;
        var invGrams = await invQuery.Select(x => (decimal?)x.GramDegeri).SumAsync(ct) ?? 0m;
        var expGrams = await expQuery.Select(x => (decimal?)x.GramDegeri).SumAsync(ct) ?? 0m;

        var invKarat = await invQuery
            .Where(x => x.Kesildi && x.AltinAyar.HasValue)
            .GroupBy(x => x.AltinAyar!.Value)
            .Select(g => new { ayar = (int)g.Key, gram = g.Sum(x => (decimal?)x.GramDegeri) ?? 0m })
            .ToListAsync(ct);
        var expKarat = await expQuery
            .Where(x => x.Kesildi && x.AltinAyar.HasValue)
            .GroupBy(x => x.AltinAyar!.Value)
            .Select(g => new { ayar = (int)g.Key, gram = g.Sum(x => (decimal?)x.GramDegeri) ?? 0m })
            .ToListAsync(ct);

        var karatMap = new Dictionary<int, (decimal inv, decimal exp)>();
        foreach (var row in invKarat)
        {
            if (!karatMap.TryGetValue(row.ayar, out var cur)) cur = (0m, 0m);
            karatMap[row.ayar] = (row.gram, cur.exp);
        }
        foreach (var row in expKarat)
        {
            if (!karatMap.TryGetValue(row.ayar, out var cur)) cur = (0m, 0m);
            karatMap[row.ayar] = (cur.inv, row.gram);
        }
        var karatRows = karatMap
            .Select(x => new { ayar = x.Key, inv = x.Value.inv, exp = x.Value.exp })
            .OrderByDescending(x => x.ayar)
            .ToList();

        var invYears = await db.Invoices.AsNoTracking().Select(x => x.Tarih.Year).Distinct().ToListAsync(ct);
        var expYears = await db.Expenses.AsNoTracking().Select(x => x.Tarih.Year).Distinct().ToListAsync(ct);
        var availableYears = invYears.Concat(expYears)
            .Distinct()
            .OrderByDescending(x => x)
            .Select(x => x.ToString())
            .ToList();

        IQueryable<Invoice> invMonthQuery = db.Invoices.AsNoTracking();
        IQueryable<Expense> expMonthQuery = db.Expenses.AsNoTracking();
        if (yearSet.Count > 0)
        {
            invMonthQuery = invMonthQuery.Where(x => yearSet.Contains(x.Tarih.Year));
            expMonthQuery = expMonthQuery.Where(x => yearSet.Contains(x.Tarih.Year));
        }
        var invMonths = await invMonthQuery
            .Select(x => new { x.Tarih.Year, x.Tarih.Month })
            .Distinct()
            .ToListAsync(ct);
        var expMonths = await expMonthQuery
            .Select(x => new { x.Tarih.Year, x.Tarih.Month })
            .Distinct()
            .ToListAsync(ct);
        var monthSet = new HashSet<int>();
        foreach (var m in invMonths) monthSet.Add((m.Year * 100) + m.Month);
        foreach (var m in expMonths) monthSet.Add((m.Year * 100) + m.Month);
        var availableMonths = monthSet
            .OrderByDescending(x => x)
            .Select(x => $"{x / 100}-{(x % 100).ToString().PadLeft(2, '0')}")
            .ToList();

        var pendingInvoices = await db.Invoices.AsNoTracking().CountAsync(x => !x.Kesildi, ct);
        var pendingExpenses = await db.Expenses.AsNoTracking().CountAsync(x => !x.Kesildi, ct);

        cached = new
        {
            income,
            outgo,
            net = income - outgo,
            invGrams,
            expGrams,
            karatRows,
            availableYears,
            availableMonths,
            pendingInvoices,
            pendingExpenses
        };
        cache.Set(cacheKey, cached, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
    }

    return Results.Ok(cached);
}).WithTags("Dashboard").RequireAuthorization();

app.MapGet("/api/gold/stock", async (
    IGoldStockService stockService,
    KtpDbContext db,
    HttpContext http,
    CancellationToken ct) =>
{
    // Permission: admin or role.CanUseInvoices + role.CanUseExpenses
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
            if (r?.CanUseInvoices != true || r?.CanUseExpenses != true) return Results.Forbid();
        }
        else return Results.Forbid();
    }

    var rows = await stockService.GetStockAsync(ct);
    return Results.Ok(rows);
}).WithTags("Gold").RequireAuthorization();

app.MapPost("/api/gold/opening-inventory", async (
    GoldOpeningInventoryRequest req,
    IGoldStockService stockService,
    KtpDbContext db,
    HttpContext http,
    CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();

    if (req.Karat <= 0) return Results.BadRequest(new { error = "Karat gecersiz" });
    if (req.Gram < 0) return Results.BadRequest(new { error = "Gram negatif olamaz" });
    if (req.Date == default) return Results.BadRequest(new { error = "Acilis tarihi gecersiz" });

    var desc = string.IsNullOrWhiteSpace(req.Description) ? "Muhasebe acilis bakiyesi" : req.Description.Trim();
    var row = await stockService.UpsertOpeningAsync(new GoldOpeningInventoryInput(req.Karat, req.Date, req.Gram, desc), ct);
    return Results.Ok(row);
}).WithTags("Gold").RequireAuthorization();

app.MapGet("/api/products", async (KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var items = await db.Products.AsNoTracking()
        .OrderBy(x => x.Name)
        .ThenBy(x => x.Code)
        .ToListAsync(ct);
    return Results.Ok(items);
}).WithTags("Products").RequireAuthorization();

app.MapPost("/api/products", async (ProductCreateRequest req, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var code = req.Code?.Trim() ?? string.Empty;
    var name = req.Name?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(code)) return Results.BadRequest(new { error = "Code gerekli" });
    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { error = "Name gerekli" });
    if (req.Gram.HasValue && req.Gram.Value < 0) return Results.BadRequest(new { error = "Gram negatif olamaz" });

    var exists = await db.Products.AsNoTracking().AnyAsync(x => x.Code.ToUpper() == code.ToUpper(), ct);
    if (exists) return Results.BadRequest(new { error = "Code zaten var" });

    var requiresFormula = req.RequiresFormula ?? true;
    Guid? defaultFormulaId = req.DefaultFormulaId;
    (Guid? saleTemplateId, Guid? purchaseTemplateId) autoTemplates = (null, null);
    if (requiresFormula && !defaultFormulaId.HasValue)
    {
        var inferredAyar = ProductAyarResolver.TryInferFromText($"{name} {code}");
        if (inferredAyar.HasValue)
        {
            autoTemplates = await TryGetDefaultFormulaTemplatesForAyarAsync(db, inferredAyar.Value, ct);
            if (autoTemplates.saleTemplateId.HasValue)
                defaultFormulaId = autoTemplates.saleTemplateId;
        }
    }
    if (defaultFormulaId.HasValue)
    {
        var formulaExists = await db.GoldFormulaTemplates.AsNoTracking().AnyAsync(x => x.Id == defaultFormulaId.Value, ct);
        if (!formulaExists) return Results.BadRequest(new { error = "Default formula bulunamadı" });
    }
    if ((req.IsActive ?? true) && requiresFormula && !defaultFormulaId.HasValue)
        return Results.BadRequest(new { error = "Formül olmadan ürün aktif edilemez" });

    var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    Guid? uid = Guid.TryParse(sub, out var uidVal) ? uidVal : null;

    var now = DateTime.UtcNow;
    var entity = new Product
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = name,
        IsActive = req.IsActive ?? true,
        ShowInSales = req.ShowInSales ?? true,
        AccountingType = (ProductAccountingType)(req.AccountingType ?? (int)ProductAccountingType.Gram),
        Gram = req.Gram,
        RequiresFormula = requiresFormula,
        DefaultFormulaId = defaultFormulaId,
        CreatedAt = now,
        UpdatedAt = now,
        CreatedUserId = uid,
        UpdatedUserId = uid
    };
    db.Products.Add(entity);
    if (autoTemplates.saleTemplateId.HasValue && autoTemplates.purchaseTemplateId.HasValue)
    {
        await EnsureFormulaBindingRowAsync(db, entity.Id, autoTemplates.saleTemplateId.Value, GoldFormulaDirection.Sale, ct);
        await EnsureFormulaBindingRowAsync(db, entity.Id, autoTemplates.purchaseTemplateId.Value, GoldFormulaDirection.Purchase, ct);
    }
    await db.SaveChangesAsync(ct);
    return Results.Ok(entity);
}).WithTags("Products").RequireAuthorization();

app.MapPut("/api/products/{id:guid}", async (Guid id, ProductUpdateRequest req, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var code = req.Code?.Trim() ?? string.Empty;
    var name = req.Name?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(code)) return Results.BadRequest(new { error = "Code gerekli" });
    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { error = "Name gerekli" });
    if (req.Gram.HasValue && req.Gram.Value < 0) return Results.BadRequest(new { error = "Gram negatif olamaz" });

    var entity = await db.Products.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) return Results.NotFound();

    var exists = await db.Products.AsNoTracking()
        .AnyAsync(x => x.Id != id && x.Code.ToUpper() == code.ToUpper(), ct);
    if (exists) return Results.BadRequest(new { error = "Code zaten var" });

    var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    Guid? uid = Guid.TryParse(sub, out var uidVal) ? uidVal : null;

    Guid? defaultFormulaId = req.DefaultFormulaId ?? entity.DefaultFormulaId;
    var requiresFormula = req.RequiresFormula ?? entity.RequiresFormula;
    var willBeActive = req.IsActive ?? entity.IsActive;
    (Guid? saleTemplateId, Guid? purchaseTemplateId) autoTemplates = (null, null);
    var hasBinding = await db.GoldProductFormulaBindings.AsNoTracking()
        .AnyAsync(x => x.GoldProductId == entity.Id && x.IsActive, ct);
    if (willBeActive && requiresFormula && !defaultFormulaId.HasValue && !hasBinding)
    {
        var inferredAyar = ProductAyarResolver.TryInferFromText($"{name} {code}");
        if (inferredAyar.HasValue)
        {
            autoTemplates = await TryGetDefaultFormulaTemplatesForAyarAsync(db, inferredAyar.Value, ct);
            if (autoTemplates.saleTemplateId.HasValue)
                defaultFormulaId = autoTemplates.saleTemplateId;
        }
    }
    if (defaultFormulaId.HasValue)
    {
        var formulaExists = await db.GoldFormulaTemplates.AsNoTracking().AnyAsync(x => x.Id == defaultFormulaId.Value, ct);
        if (!formulaExists) return Results.BadRequest(new { error = "Default formula bulunamadı" });
    }
    if (willBeActive && requiresFormula && !hasBinding && !defaultFormulaId.HasValue)
        return Results.BadRequest(new { error = "Formül olmadan ürün aktif edilemez" });

    entity.Code = code;
    entity.Name = name;
    entity.IsActive = req.IsActive ?? entity.IsActive;
    entity.ShowInSales = req.ShowInSales ?? entity.ShowInSales;
    entity.AccountingType = (ProductAccountingType)(req.AccountingType ?? (int)entity.AccountingType);
    entity.Gram = req.Gram;
    entity.RequiresFormula = requiresFormula;
    entity.DefaultFormulaId = defaultFormulaId;
    entity.UpdatedAt = DateTime.UtcNow;
    entity.UpdatedUserId = uid;

    if (autoTemplates.saleTemplateId.HasValue && autoTemplates.purchaseTemplateId.HasValue)
    {
        await EnsureFormulaBindingRowAsync(db, entity.Id, autoTemplates.saleTemplateId.Value, GoldFormulaDirection.Sale, ct);
        await EnsureFormulaBindingRowAsync(db, entity.Id, autoTemplates.purchaseTemplateId.Value, GoldFormulaDirection.Purchase, ct);
    }

    await db.SaveChangesAsync(ct);
    return Results.Ok(entity);
}).WithTags("Products").RequireAuthorization();

app.MapDelete("/api/products/{id:guid}", async (Guid id, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var entity = await db.Products.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) return Results.NotFound();
    db.Products.Remove(entity);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
}).WithTags("Products").RequireAuthorization();

app.MapGet("/api/products/opening-inventory", async (KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var items = await (from p in db.Products.AsNoTracking()
                       join o in db.ProductOpeningInventories.AsNoTracking() on p.Id equals o.ProductId into og
                       from o in og.DefaultIfEmpty()
                       orderby p.Name, p.Code
                       select new
                       {
                           productId = p.Id,
                           p.Code,
                           p.Name,
                           p.IsActive,
                           p.ShowInSales,
                           p.AccountingType,
                           p.Gram,
                           openingQuantity = (decimal?)o.Quantity,
                           openingDate = (DateTime?)o.Date
                       }).ToListAsync(ct);
    return Results.Ok(items);
}).WithTags("Products").RequireAuthorization();

app.MapPost("/api/products/opening-inventory", async (ProductOpeningInventoryRequest req, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    if (req.ProductId == Guid.Empty) return Results.BadRequest(new { error = "ProductId gerekli" });
    if (req.Date == default) return Results.BadRequest(new { error = "Tarih gerekli" });
    if (req.Quantity < 0) return Results.BadRequest(new { error = "Miktar negatif olamaz" });

    var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.ProductId, ct);
    if (product is null) return Results.NotFound();

    var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    Guid? uid = Guid.TryParse(sub, out var uidVal) ? uidVal : null;

    var now = DateTime.UtcNow;
    var normalizedDate = req.Date.Kind == DateTimeKind.Utc
        ? req.Date
        : (req.Date.Kind == DateTimeKind.Local ? req.Date.ToUniversalTime() : DateTime.SpecifyKind(req.Date, DateTimeKind.Utc));

    var opening = await db.ProductOpeningInventories.FirstOrDefaultAsync(x => x.ProductId == req.ProductId, ct);
    if (opening is null)
    {
        opening = new ProductOpeningInventory
        {
            Id = Guid.NewGuid(),
            ProductId = req.ProductId,
            Date = normalizedDate,
            Quantity = req.Quantity,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedUserId = uid,
            UpdatedUserId = uid
        };
        db.ProductOpeningInventories.Add(opening);
    }
    else
    {
        opening.Date = normalizedDate;
        opening.Quantity = req.Quantity;
        opening.UpdatedAt = now;
        opening.UpdatedUserId = uid;
    }

    await db.SaveChangesAsync(ct);

    return Results.Ok(new
    {
        productId = product.Id,
        product.Code,
        product.Name,
        product.IsActive,
        product.ShowInSales,
        product.AccountingType,
        product.Gram,
        openingQuantity = opening.Quantity,
        openingDate = opening.Date
    });
}).WithTags("Products").RequireAuthorization();

// Formula templates
app.MapGet("/api/formulas", async (KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var rows = await db.GoldFormulaTemplates.AsNoTracking()
        .OrderBy(x => x.Code)
        .ToListAsync(ct);
    return Results.Ok(rows);
}).WithTags("Formulas").RequireAuthorization();

app.MapGet("/api/formulas/{id:guid}", async (Guid id, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var entity = await db.GoldFormulaTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) return Results.NotFound();
    return Results.Ok(entity);
}).WithTags("Formulas").RequireAuthorization();

app.MapPost("/api/formulas", async (FormulaUpsertRequest req, KtpDbContext db, IGoldFormulaEngine engine, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var code = req.Code?.Trim() ?? string.Empty;
    var name = req.Name?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(code)) return Results.BadRequest(new { error = "Code gerekli" });
    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { error = "Name gerekli" });
    if (string.IsNullOrWhiteSpace(req.DefinitionJson)) return Results.BadRequest(new { error = "DefinitionJson gerekli" });

    var exists = await db.GoldFormulaTemplates.AsNoTracking().AnyAsync(x => x.Code.ToUpper() == code.ToUpper(), ct);
    if (exists) return Results.BadRequest(new { error = "Code zaten var" });

    try
    {
        engine.ValidateDefinition(req.DefinitionJson);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    var entity = new GoldFormulaTemplate
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = name,
        Scope = req.Scope ?? GoldFormulaScope.ProductSpecific,
        FormulaType = req.FormulaType ?? GoldFormulaType.Both,
        DslVersion = req.DslVersion ?? 1,
        DefinitionJson = req.DefinitionJson,
        IsActive = req.IsActive ?? true,
        CreatedAt = DateTime.UtcNow
    };
    db.GoldFormulaTemplates.Add(entity);
    await db.SaveChangesAsync(ct);
    return Results.Ok(entity);
}).WithTags("Formulas").RequireAuthorization();

app.MapPut("/api/formulas/{id:guid}", async (Guid id, FormulaUpsertRequest req, KtpDbContext db, IGoldFormulaEngine engine, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var entity = await db.GoldFormulaTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) return Results.NotFound();

    var code = req.Code?.Trim() ?? string.Empty;
    var name = req.Name?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(code)) return Results.BadRequest(new { error = "Code gerekli" });
    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { error = "Name gerekli" });
    if (string.IsNullOrWhiteSpace(req.DefinitionJson)) return Results.BadRequest(new { error = "DefinitionJson gerekli" });

    var exists = await db.GoldFormulaTemplates.AsNoTracking()
        .AnyAsync(x => x.Id != id && x.Code.ToUpper() == code.ToUpper(), ct);
    if (exists) return Results.BadRequest(new { error = "Code zaten var" });

    try
    {
        engine.ValidateDefinition(req.DefinitionJson);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    entity.Code = code;
    entity.Name = name;
    entity.Scope = req.Scope ?? entity.Scope;
    entity.FormulaType = req.FormulaType ?? entity.FormulaType;
    entity.DslVersion = req.DslVersion ?? entity.DslVersion;
    entity.DefinitionJson = req.DefinitionJson;
    entity.IsActive = req.IsActive ?? entity.IsActive;

    await db.SaveChangesAsync(ct);
    return Results.Ok(entity);
}).WithTags("Formulas").RequireAuthorization();

app.MapDelete("/api/formulas/{id:guid}", async (Guid id, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    await db.GoldProductFormulaBindings
        .Where(x => x.FormulaTemplateId == id)
        .ExecuteDeleteAsync(ct);

    var deleted = await db.GoldFormulaTemplates
        .Where(x => x.Id == id)
        .ExecuteDeleteAsync(ct);

    if (deleted == 0) return Results.NotFound();
    return Results.Ok();
}).WithTags("Formulas").RequireAuthorization();

app.MapPost("/api/formulas/{id:guid}/validate", async (
    Guid id,
    FormulaValidateRequest req,
    KtpDbContext db,
    IGoldFormulaEngine engine,
    HttpContext http,
    CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var entity = await db.GoldFormulaTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) return Results.NotFound();

    var context = new GoldFormulaContext(
        req.Amount ?? 0m,
        req.HasGoldPrice ?? 0m,
        req.VatRate ?? 0.20m,
        req.AccountingType.HasValue ? (ProductAccountingType)req.AccountingType.Value : ProductAccountingType.Gram,
        req.ProductGram,
        req.Direction ?? GoldFormulaDirection.Sale,
        req.OperationType ?? GoldFormulaOperationType.Invoice,
        req.AltinSatisFiyati);

    try
    {
        var result = engine.Evaluate(entity.DefinitionJson, context, req.Mode ?? GoldFormulaMode.Preview);
        return Results.Ok(new { ok = true, result = result.Result });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { ok = false, error = ex.Message });
    }
}).WithTags("Formulas").RequireAuthorization();

// Formula bindings
app.MapGet("/api/products/{id:guid}/formula-bindings", async (Guid id, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var rows = await (from b in db.GoldProductFormulaBindings.AsNoTracking()
                      join t in db.GoldFormulaTemplates.AsNoTracking() on b.FormulaTemplateId equals t.Id
                      where b.GoldProductId == id
                      orderby b.Direction, t.Code
                      select new
                      {
                          id = b.Id,
                          productId = b.GoldProductId,
                          templateId = t.Id,
                          templateCode = t.Code,
                          templateName = t.Name,
                          direction = b.Direction,
                          isActive = b.IsActive
                      }).ToListAsync(ct);
    return Results.Ok(rows);
}).WithTags("Formulas").RequireAuthorization();

app.MapPost("/api/formula-bindings", async (FormulaBindingCreateRequest req, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    if (req.ProductId == Guid.Empty) return Results.BadRequest(new { error = "ProductId gerekli" });
    if (req.TemplateId == Guid.Empty) return Results.BadRequest(new { error = "TemplateId gerekli" });

    var productExists = await db.Products.AsNoTracking().AnyAsync(x => x.Id == req.ProductId, ct);
    if (!productExists) return Results.BadRequest(new { error = "Ürün bulunamadı" });
    var templateExists = await db.GoldFormulaTemplates.AsNoTracking().AnyAsync(x => x.Id == req.TemplateId, ct);
    if (!templateExists) return Results.BadRequest(new { error = "Formül bulunamadı" });

    var entity = new GoldProductFormulaBinding
    {
        Id = Guid.NewGuid(),
        GoldProductId = req.ProductId,
        FormulaTemplateId = req.TemplateId,
        Direction = req.Direction,
        IsActive = req.IsActive ?? true
    };
    db.GoldProductFormulaBindings.Add(entity);
    await db.SaveChangesAsync(ct);
    return Results.Ok(entity);
}).WithTags("Formulas").RequireAuthorization();

app.MapPut("/api/formula-bindings/{id:guid}", async (Guid id, FormulaBindingUpdateRequest req, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var entity = await db.GoldProductFormulaBindings.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) return Results.NotFound();

    if (req.TemplateId.HasValue)
    {
        var templateExists = await db.GoldFormulaTemplates.AsNoTracking().AnyAsync(x => x.Id == req.TemplateId.Value, ct);
        if (!templateExists) return Results.BadRequest(new { error = "Formül bulunamadı" });
        entity.FormulaTemplateId = req.TemplateId.Value;
    }
    if (req.Direction.HasValue)
        entity.Direction = req.Direction.Value;
    if (req.IsActive.HasValue)
        entity.IsActive = req.IsActive.Value;

    await db.SaveChangesAsync(ct);
    return Results.Ok(entity);
}).WithTags("Formulas").RequireAuthorization();

// Categories
app.MapGet("/api/cashier/categories", async (KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!await HasCustomerLookupPermissionAsync(http, db, ct)) return Results.Forbid();
    var rows = await (from cp in db.CategoryProducts.AsNoTracking()
                      join c in db.Categories.AsNoTracking() on cp.CategoryId equals c.Id
                      join p in db.Products.AsNoTracking() on cp.ProductId equals p.Id
                      where p.IsActive && p.ShowInSales
                            && (!p.RequiresFormula || db.GoldProductFormulaBindings.Any(b =>
                                b.GoldProductId == p.Id && b.Direction == GoldFormulaDirection.Sale && b.IsActive))
                      orderby c.Name, p.Name
                      select new
                      {
                          categoryId = c.Id,
                          categoryName = c.Name,
                          categoryParentId = c.ParentId,
                          productId = p.Id,
                          productCode = p.Code,
                          productName = p.Name
                      }).ToListAsync(ct);

    var grouped = rows
        .GroupBy(x => new { x.categoryId, x.categoryName, x.categoryParentId })
        .Select(g =>
        {
            var products = g
                .GroupBy(x => x.productId)
                .Select(pg =>
                {
                    var first = pg.First();
                    return new { id = first.productId, code = first.productCode, name = first.productName };
                })
                .OrderBy(p => p.name)
                .ToList();
            return new { id = g.Key.categoryId, name = g.Key.categoryName, parentId = g.Key.categoryParentId, products };
        })
        .OrderBy(x => x.name)
        .ToList();

    return Results.Ok(grouped);
}).WithTags("Cashier").RequireAuthorization();

// POS formula preview
app.MapPost("/api/pos/preview", async (
    PosPreviewRequest req,
    KtpDbContext db,
    MarketDbContext mdb,
    IGoldFormulaEngine engine,
    HttpContext http,
    CancellationToken ct) =>
{
    if (!await HasCustomerLookupPermissionAsync(http, db, ct)) return Results.Forbid();
    if (req.ProductId == Guid.Empty) return Results.BadRequest(new { error = "ProductId gerekli" });
    if (req.Amount <= 0) return Results.BadRequest(new { error = "Tutar 0'dan büyük olmalı" });

    var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.ProductId, ct);
    if (product is null) return Results.NotFound();
    if (!product.IsActive) return Results.BadRequest(new { error = "Ürün aktif değil" });
    if (req.Direction == GoldFormulaDirection.Sale && !product.ShowInSales)
        return Results.BadRequest(new { error = "Ürün satışta kullanılamaz" });

    var (template, templateError) = await ResolveFormulaTemplateAsync(db, product, req.Direction, ct);
    if (template is null) return Results.BadRequest(new { error = templateError ?? "Formül bulunamadı" });

    var (hasGoldPrice, priceError) = await ResolveHasGoldPriceAsync(mdb, db, product.Id, req.Direction, ct);
    if (!hasGoldPrice.HasValue) return Results.BadRequest(new { error = priceError ?? "Has altın fiyatı bulunamadı" });

    var context = new GoldFormulaContext(
        req.Amount,
        hasGoldPrice.Value,
        req.VatRate ?? 0.20m,
        product.AccountingType,
        product.Gram,
        req.Direction,
        req.OperationType,
        hasGoldPrice.Value);

    try
    {
        var eval = engine.Evaluate(template.DefinitionJson, context, GoldFormulaMode.Preview);
        var previewTheme = NormalizePreviewTheme(TryParsePreviewTheme(template.DefinitionJson));
        var previewFields = BuildPreviewFields(previewTheme, eval.Result, eval.UsedVariables);
        var isAdmin = http.User.IsInRole(Role.Yonetici.ToString());
        return Results.Ok(new
        {
            formulaTemplateId = template.Id,
            hasGoldPrice = hasGoldPrice.Value,
            result = eval.Result,
            previewTitle = previewTheme?.Title ?? defaultPreviewTheme.Title,
            previewFields,
            usedVariables = isAdmin ? eval.UsedVariables : null,
            debugSteps = isAdmin ? eval.DebugSteps : null
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithTags("POS").RequireAuthorization();

// POS formula finalize
app.MapPost("/api/pos/finalize", async (
    PosFinalizeRequest req,
    KtpDbContext db,
    MarketDbContext mdb,
    IGoldFormulaEngine engine,
    HttpContext http,
    CancellationToken ct) =>
{
    if (!await HasCustomerLookupPermissionAsync(http, db, ct)) return Results.Forbid();
    if (req.ProductId == Guid.Empty) return Results.BadRequest(new { error = "ProductId gerekli" });
    if (req.Amount <= 0) return Results.BadRequest(new { error = "Tutar 0'dan büyük olmalı" });

    var product = await db.Products.FirstOrDefaultAsync(x => x.Id == req.ProductId, ct);
    if (product is null) return Results.NotFound();
    if (!product.IsActive) return Results.BadRequest(new { error = "Ürün aktif değil" });
    if (req.Direction == GoldFormulaDirection.Sale && !product.ShowInSales)
        return Results.BadRequest(new { error = "Ürün satışta kullanılamaz" });

    var (template, templateError) = await ResolveFormulaTemplateAsync(db, product, req.Direction, ct);
    if (template is null) return Results.BadRequest(new { error = templateError ?? "Formül bulunamadı" });

    var (hasGoldPrice, priceError) = await ResolveHasGoldPriceAsync(mdb, db, product.Id, req.Direction, ct);
    if (!hasGoldPrice.HasValue) return Results.BadRequest(new { error = priceError ?? "Has altın fiyatı bulunamadı" });

    var context = new GoldFormulaContext(
        req.Amount,
        hasGoldPrice.Value,
        req.VatRate ?? 0.20m,
        product.AccountingType,
        product.Gram,
        req.Direction,
        req.OperationType,
        hasGoldPrice.Value);

    GoldFormulaEvaluationResult eval;
    try
    {
        eval = engine.Evaluate(template.DefinitionJson, context, GoldFormulaMode.Finalize);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    var now = DateTime.UtcNow;
    var userInfo = GetCurrentUserInfo(http);
    var altinAyar = await ProductAyarResolver.TryResolveAsync(db, product.Id, ct);

    if (req.OperationType == GoldFormulaOperationType.Invoice)
    {
        if (!req.OdemeSekli.HasValue)
            return Results.BadRequest(new { error = "OdemeSekli gerekli" });

        var siraNo = await SequenceUtil.NextIntAsync(db.Database, "Invoices_SiraNo_seq", initTable: "Invoices", initColumn: "SiraNo", ct: ct);
        var inv = new Invoice
        {
            Id = Guid.NewGuid(),
            Tarih = DateOnly.FromDateTime(now),
            SiraNo = siraNo,
            IsForCompany = false,
            ProductId = product.Id,
            Tutar = eval.Result.Amount,
            OdemeSekli = req.OdemeSekli.Value,
            AltinAyar = altinAyar,
            AltinSatisFiyati = hasGoldPrice.Value,
            SafAltinDegeri = eval.Result.UnitHasPriceUsed,
            UrunFiyati = eval.Result.Amount,
            YeniUrunFiyati = TryGetVariable(eval.UsedVariables, "yeniUrun"),
            GramDegeri = eval.Result.Gram,
            Iscilik = eval.Result.LaborNet,
            FinalizedAt = now,
            CreatedById = userInfo.UserId,
            CreatedByEmail = userInfo.Email,
            KasiyerId = userInfo.UserId,
            Kesildi = false
        };

        db.Invoices.Add(inv);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new
        {
            id = inv.Id,
            siraNo = inv.SiraNo,
            formulaTemplateId = template.Id,
            hasGoldPrice = hasGoldPrice.Value,
            result = eval.Result
        });
    }

    var expenseSiraNo = await SequenceUtil.NextIntAsync(db.Database, "Expenses_SiraNo_seq", initTable: "Expenses", initColumn: "SiraNo", ct: ct);
    var exp = new Expense
    {
        Id = Guid.NewGuid(),
        Tarih = DateOnly.FromDateTime(now),
        SiraNo = expenseSiraNo,
        IsForCompany = false,
        ProductId = product.Id,
        Tutar = eval.Result.Amount,
        AltinAyar = altinAyar,
        AltinSatisFiyati = hasGoldPrice.Value,
        SafAltinDegeri = eval.Result.UnitHasPriceUsed,
        UrunFiyati = eval.Result.Amount,
        YeniUrunFiyati = TryGetVariable(eval.UsedVariables, "yeniUrun"),
        GramDegeri = eval.Result.Gram,
        Iscilik = eval.Result.LaborNet,
        FinalizedAt = now,
        CreatedById = userInfo.UserId,
        CreatedByEmail = userInfo.Email,
        KasiyerId = userInfo.UserId,
        Kesildi = false
    };

    db.Expenses.Add(exp);
    await db.SaveChangesAsync(ct);
    return Results.Ok(new
    {
        id = exp.Id,
        siraNo = exp.SiraNo,
        formulaTemplateId = template.Id,
        hasGoldPrice = hasGoldPrice.Value,
        result = eval.Result
    });
}).WithTags("POS").RequireAuthorization();

app.MapGet("/api/categories", async (KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var items = await (from c in db.Categories.AsNoTracking()
                       join u in db.Users.AsNoTracking() on c.UpdatedUserId equals u.Id into uu
                       from u in uu.DefaultIfEmpty()
                       orderby c.Name
                       select new
                       {
                           id = c.Id,
                           name = c.Name,
                           parentId = c.ParentId,
                           createdAt = c.CreatedAt,
                           updatedAt = c.UpdatedAt,
                           updatedUserId = c.UpdatedUserId,
                           updatedUserEmail = u != null ? u.Email : null
                       }).ToListAsync(ct);
    return Results.Ok(items);
}).WithTags("Categories").RequireAuthorization();

app.MapPost("/api/categories", async (CategoryCreateRequest req, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var name = req.Name?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { error = "Name gerekli" });

    Guid? parentId = req.ParentId.HasValue && req.ParentId.Value != Guid.Empty ? req.ParentId.Value : null;
    if (parentId.HasValue)
    {
        var parentExists = await db.Categories.AsNoTracking().AnyAsync(x => x.Id == parentId.Value, ct);
        if (!parentExists) return Results.BadRequest(new { error = "Parent bulunamadı" });
    }

    var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    Guid? uid = Guid.TryParse(sub, out var uidVal) ? uidVal : null;

    var now = DateTime.UtcNow;
    var entity = new Category
    {
        Id = Guid.NewGuid(),
        Name = name,
        ParentId = parentId,
        CreatedAt = now,
        UpdatedAt = now,
        CreatedUserId = uid,
        UpdatedUserId = uid
    };
    db.Categories.Add(entity);
    await db.SaveChangesAsync(ct);
    return Results.Ok(entity);
}).WithTags("Categories").RequireAuthorization();

app.MapPut("/api/categories/{id:guid}", async (Guid id, CategoryUpdateRequest req, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var name = req.Name?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { error = "Name gerekli" });

    var entity = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) return Results.NotFound();

    Guid? parentId = req.ParentId.HasValue && req.ParentId.Value != Guid.Empty ? req.ParentId.Value : null;
    if (parentId.HasValue)
    {
        if (parentId.Value == id) return Results.BadRequest(new { error = "ParentId kendisi olamaz" });
        var parentExists = await db.Categories.AsNoTracking().AnyAsync(x => x.Id == parentId.Value, ct);
        if (!parentExists) return Results.BadRequest(new { error = "Parent bulunamadı" });
        if (await HasCategoryCycleAsync(db, id, parentId.Value, ct))
            return Results.BadRequest(new { error = "Kategori hiyerarşisi döngü oluşturamaz" });
    }

    var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    Guid? uid = Guid.TryParse(sub, out var uidVal) ? uidVal : null;

    entity.Name = name;
    entity.ParentId = parentId;
    entity.UpdatedAt = DateTime.UtcNow;
    entity.UpdatedUserId = uid;

    await db.SaveChangesAsync(ct);
    return Results.Ok(entity);
}).WithTags("Categories").RequireAuthorization();

app.MapDelete("/api/categories/{id:guid}", async (Guid id, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var entity = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) return Results.NotFound();
    db.Categories.Remove(entity);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
}).WithTags("Categories").RequireAuthorization();

// Category-Product mapping
app.MapGet("/api/category-products", async (KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var items = await (from cp in db.CategoryProducts.AsNoTracking()
                       join c in db.Categories.AsNoTracking() on cp.CategoryId equals c.Id
                       join p in db.Products.AsNoTracking() on cp.ProductId equals p.Id
                       orderby c.Name, p.Name
                       select new
                       {
                           id = cp.Id,
                           categoryId = cp.CategoryId,
                           categoryName = c.Name,
                           productId = cp.ProductId,
                           productCode = p.Code,
                           productName = p.Name
                       }).ToListAsync(ct);
    return Results.Ok(items);
}).WithTags("Categories").RequireAuthorization();

app.MapPost("/api/category-products", async (CategoryProductCreateRequest req, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    if (req.CategoryId == Guid.Empty) return Results.BadRequest(new { error = "CategoryId gerekli" });
    if (req.ProductId == Guid.Empty) return Results.BadRequest(new { error = "ProductId gerekli" });

    var categoryExists = await db.Categories.AsNoTracking().AnyAsync(x => x.Id == req.CategoryId, ct);
    if (!categoryExists) return Results.BadRequest(new { error = "Kategori bulunamadı" });
    var productExists = await db.Products.AsNoTracking().AnyAsync(x => x.Id == req.ProductId, ct);
    if (!productExists) return Results.BadRequest(new { error = "Ürün bulunamadı" });

    var exists = await db.CategoryProducts.AsNoTracking()
        .AnyAsync(x => x.CategoryId == req.CategoryId && x.ProductId == req.ProductId, ct);
    if (exists) return Results.BadRequest(new { error = "Eşleşme zaten var" });

    var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    Guid? uid = Guid.TryParse(sub, out var uidVal) ? uidVal : null;

    var now = DateTime.UtcNow;
    var entity = new CategoryProduct
    {
        Id = Guid.NewGuid(),
        CategoryId = req.CategoryId,
        ProductId = req.ProductId,
        CreatedAt = now,
        UpdatedAt = now,
        CreatedUserId = uid,
        UpdatedUserId = uid
    };
    db.CategoryProducts.Add(entity);
    await db.SaveChangesAsync(ct);
    return Results.Ok(entity);
}).WithTags("Categories").RequireAuthorization();

app.MapPut("/api/category-products/{id:guid}", async (Guid id, CategoryProductUpdateRequest req, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    if (req.CategoryId == Guid.Empty) return Results.BadRequest(new { error = "CategoryId gerekli" });
    if (req.ProductId == Guid.Empty) return Results.BadRequest(new { error = "ProductId gerekli" });

    var entity = await db.CategoryProducts.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) return Results.NotFound();

    var categoryExists = await db.Categories.AsNoTracking().AnyAsync(x => x.Id == req.CategoryId, ct);
    if (!categoryExists) return Results.BadRequest(new { error = "Kategori bulunamadı" });
    var productExists = await db.Products.AsNoTracking().AnyAsync(x => x.Id == req.ProductId, ct);
    if (!productExists) return Results.BadRequest(new { error = "Ürün bulunamadı" });

    var exists = await db.CategoryProducts.AsNoTracking()
        .AnyAsync(x => x.Id != id && x.CategoryId == req.CategoryId && x.ProductId == req.ProductId, ct);
    if (exists) return Results.BadRequest(new { error = "Eşleşme zaten var" });

    var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    Guid? uid = Guid.TryParse(sub, out var uidVal) ? uidVal : null;

    entity.CategoryId = req.CategoryId;
    entity.ProductId = req.ProductId;
    entity.UpdatedAt = DateTime.UtcNow;
    entity.UpdatedUserId = uid;

    await db.SaveChangesAsync(ct);
    return Results.Ok(entity);
}).WithTags("Categories").RequireAuthorization();

app.MapDelete("/api/category-products/{id:guid}", async (Guid id, KtpDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!http.User.IsInRole(Role.Yonetici.ToString())) return Results.Forbid();
    var entity = await db.CategoryProducts.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) return Results.NotFound();
    db.CategoryProducts.Remove(entity);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
}).WithTags("Categories").RequireAuthorization();

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

        var rawTckn = dto.TCKN?.Trim() ?? string.Empty;
        if (rawTckn.Length != 11 || !rawTckn.All(char.IsDigit))
            return Results.BadRequest(new { error = "TCKN hatali" });

        var rawCompanyName = dto.CompanyName?.Trim() ?? string.Empty;
        var rawVkn = dto.VknNo?.Trim() ?? string.Empty;
        if (dto.IsCompany)
        {
            if (string.IsNullOrWhiteSpace(rawCompanyName))
                return Results.BadRequest(new { error = "CompanyName gerekli" });
            if (!IsValidVkn(rawVkn))
                return Results.BadRequest(new { error = "VKN gecersiz" });
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(rawCompanyName) || !string.IsNullOrWhiteSpace(rawVkn))
                return Results.BadRequest(new { error = "VKN icin IsCompany secilmeli" });
        }

        if (dto.ProductId == Guid.Empty)
            return Results.BadRequest(new { error = "ProductId gerekli" });

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.ProductId, ct);
        if (product is null) return Results.BadRequest(new { error = "Ürün bulunamadı" });
        var (template, templateError) = await ResolveFormulaTemplateAsync(db, product, GoldFormulaDirection.Sale, ct);
        if (template is null && product.RequiresFormula)
            return Results.BadRequest(new { error = templateError ?? "Ürün için satış formülü tanımlı değil" });

        var resolvedAyar = await ProductAyarResolver.TryResolveAsync(db, dto.ProductId, ct);

        var normalizedName = CustomerUtil.NormalizeName(dto.MusteriAdSoyad);
        var normalizedTckn = CustomerUtil.NormalizeTckn(dto.TCKN);
        var normalizedCompanyName = CustomerUtil.NormalizeName(dto.CompanyName);
        var normalizedVkn = CustomerUtil.NormalizeVkn(dto.VknNo);
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
                IsCompany = dto.IsCompany,
                VknNo = dto.IsCompany && !string.IsNullOrWhiteSpace(normalizedVkn) ? normalizedVkn : null,
                CompanyName = dto.IsCompany && !string.IsNullOrWhiteSpace(normalizedCompanyName) ? normalizedCompanyName : null,
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
            if (dto.IsCompany)
            {
                customer.IsCompany = true;
                if (!string.IsNullOrWhiteSpace(normalizedVkn))
                    customer.VknNo = normalizedVkn;
                if (!string.IsNullOrWhiteSpace(normalizedCompanyName))
                    customer.CompanyName = normalizedCompanyName;
            }
            customer.LastTransactionAt = DateTime.UtcNow;
        }

        var entity = new Invoice
        {
            Id = Guid.NewGuid(),
            Tarih = dto.Tarih,
            SiraNo = next,
            MusteriAdSoyad = customer.AdSoyad,
            TCKN = customer.TCKN,
            IsForCompany = dto.IsForCompany,
            CustomerId = customer.Id,
            Tutar = dto.Tutar,
            OdemeSekli = dto.OdemeSekli,
            AltinAyar = resolvedAyar,
            ProductId = dto.ProductId,
            KasiyerId = currentUserId,
            CreatedById = currentUserId,
            CreatedByEmail = string.IsNullOrWhiteSpace(email) ? null : email,
            Kesildi = false
        };

        // Stamp current ALTIN final sell price (per ayar margin)
        DateTime? sourceTime = null;
        if (entity.AltinAyar.HasValue)
        {
            var priceData = await mdb.GetLatestPriceForAyarAsync(entity.AltinAyar.Value, useBuyMargin: false, ct);
            if (priceData is null)
                return Results.BadRequest(new { error = "Kayitli altin fiyati bulunamadi" });
            entity.AltinSatisFiyati = priceData.Price;
            sourceTime = priceData.SourceTime;
        }

        db.Invoices.Add(entity);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Created($"/api/invoices/{entity.Id}", new { id = entity.Id, siraNo = entity.SiraNo, altinSatisFiyati = entity.AltinSatisFiyati, updatedAt = sourceTime });
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

        var rawTckn2 = dto.TCKN?.Trim() ?? string.Empty;
        if (rawTckn2.Length != 11 || !rawTckn2.All(char.IsDigit))
            return Results.BadRequest(new { error = "TCKN hatali" });

        var rawCompanyName2 = dto.CompanyName?.Trim() ?? string.Empty;
        var rawVkn2 = dto.VknNo?.Trim() ?? string.Empty;
        if (dto.IsCompany)
        {
            if (string.IsNullOrWhiteSpace(rawCompanyName2))
                return Results.BadRequest(new { error = "CompanyName gerekli" });
            if (!IsValidVkn(rawVkn2))
                return Results.BadRequest(new { error = "VKN gecersiz" });
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(rawCompanyName2) || !string.IsNullOrWhiteSpace(rawVkn2))
                return Results.BadRequest(new { error = "VKN icin IsCompany secilmeli" });
        }

        if (dto.ProductId == Guid.Empty)
            return Results.BadRequest(new { error = "ProductId gerekli" });

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.ProductId, ct);
        if (product is null) return Results.BadRequest(new { error = "Ürün bulunamadı" });
        var (template, templateError) = await ResolveFormulaTemplateAsync(db, product, GoldFormulaDirection.Purchase, ct);
        if (template is null && product.RequiresFormula)
            return Results.BadRequest(new { error = templateError ?? "Ürün için alış formülü tanımlı değil" });

        var resolvedAyar = await ProductAyarResolver.TryResolveAsync(db, dto.ProductId, ct);

        var normalizedName2 = CustomerUtil.NormalizeName(dto.MusteriAdSoyad);
        var normalizedTckn2 = CustomerUtil.NormalizeTckn(dto.TCKN);
        var normalizedCompanyName2 = CustomerUtil.NormalizeName(dto.CompanyName);
        var normalizedVkn2 = CustomerUtil.NormalizeVkn(dto.VknNo);
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
                IsCompany = dto.IsCompany,
                VknNo = dto.IsCompany && !string.IsNullOrWhiteSpace(normalizedVkn2) ? normalizedVkn2 : null,
                CompanyName = dto.IsCompany && !string.IsNullOrWhiteSpace(normalizedCompanyName2) ? normalizedCompanyName2 : null,
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
            if (dto.IsCompany)
            {
                customer.IsCompany = true;
                if (!string.IsNullOrWhiteSpace(normalizedVkn2))
                    customer.VknNo = normalizedVkn2;
                if (!string.IsNullOrWhiteSpace(normalizedCompanyName2))
                    customer.CompanyName = normalizedCompanyName2;
            }
            customer.LastTransactionAt = DateTime.UtcNow;
        }

        var entity = new Expense
        {
            Id = Guid.NewGuid(), Tarih = dto.Tarih, SiraNo = next,
            MusteriAdSoyad = customer.AdSoyad, TCKN = customer.TCKN, IsForCompany = dto.IsForCompany, CustomerId = customer.Id, Tutar = dto.Tutar,
            AltinAyar = resolvedAyar,
            ProductId = dto.ProductId,
            KasiyerId = currentUserId,
            CreatedById = currentUserId, CreatedByEmail = string.IsNullOrWhiteSpace(email) ? null : email,
            Kesildi = false
        };

        DateTime? sourceTime = null;
        if (entity.AltinAyar.HasValue)
        {
            var priceData = await mdb.GetLatestPriceForAyarAsync(entity.AltinAyar.Value, useBuyMargin: true, ct);
            if (priceData is null)
                return Results.BadRequest(new { error = "Kayitli altin fiyati bulunamadi" });
            entity.AltinSatisFiyati = priceData.Price;
            sourceTime = priceData.SourceTime;
        }

        db.Expenses.Add(entity);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Created($"/api/expenses/{entity.Id}", new { id = entity.Id, siraNo = entity.SiraNo, altinSatisFiyati = entity.AltinSatisFiyati, updatedAt = sourceTime });
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
            (!string.IsNullOrEmpty(trimmed) && (c.TCKN.Contains(trimmed) || (c.VknNo != null && c.VknNo.Contains(trimmed)))));

    var items = await baseQuery
        .OrderByDescending(c => c.LastTransactionAt ?? c.CreatedAt)
        .ThenBy(c => c.AdSoyad)
        .Take(take)
        .Select(c => new
        {
            id = c.Id,
            adSoyad = c.AdSoyad,
            tckn = c.TCKN,
            isCompany = c.IsCompany,
            vknNo = c.VknNo,
            companyName = c.CompanyName,
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
            (!string.IsNullOrEmpty(trimmed) && (c.TCKN.Contains(trimmed) || (c.VknNo != null && c.VknNo.Contains(trimmed)))));
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
            isCompany = c.IsCompany,
            vknNo = c.VknNo,
            companyName = c.CompanyName,
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
            c.isCompany,
            c.vknNo,
            c.companyName,
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

app.MapPut("/api/pricing/gold", async (GoldPriceUpdateRequest body, IGoldPriceWriter writer, HttpContext http, CancellationToken ct) =>
{
    if (body == null || body.Price <= 0) return Results.BadRequest(new { error = "Ge?erli bir has alt?n fiyat? girin" });
    var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    Guid? userId = Guid.TryParse(sub, out var parsed) ? parsed : null;
    var email = http.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
        ?? http.User.FindFirst(ClaimTypes.Email)?.Value
        ?? http.User.FindFirst("email")?.Value;

    var latest = await writer.UpsertAsync(body.Price, userId, email, ct);
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

app.MapGet("/api/pricing/feed/latest", async (MarketDbContext mdb, CancellationToken ct) =>
{
    var candidates = await mdb.GoldFeedNewVersions
        .AsNoTracking()
        .OrderByDescending(x => x.IsParsed)
        .ThenByDescending(x => x.FetchTime)
        .Take(50)
        .ToListAsync(ct);
    if (candidates.Count == 0) return Results.NotFound();

    GoldFeedParsedResult? parsed = null;
    GoldFeedNewVersion? picked = null;
    string? lastError = null;
    foreach (var entry in candidates)
    {
        if (GoldFeedNewVersionParser.TryParse(entry.RawResponse, out parsed, out var error) && parsed is not null)
        {
            picked = entry;
            break;
        }
        lastError = error;
    }

    if (picked is null || parsed is null)
    {
        return Results.Problem($"Gold feed parse failed: {lastError}");
    }

    var items = GoldFeedNewVersionMapping.Indexes
        .Select(def => new
        {
            index = def.Index,
            label = def.Label,
            isUsed = def.IsUsed,
            value = parsed.IndexedValues[def.Index - 1]
        })
        .ToList();

    return Results.Ok(new
    {
        fetchedAt = picked.FetchTime,
        header = new
        {
            usdAlis = parsed.Header.UsdAlis,
            usdSatis = parsed.Header.UsdSatis,
            eurAlis = parsed.Header.EurAlis,
            eurSatis = parsed.Header.EurSatis,
            eurUsd = parsed.Header.EurUsd,
            ons = parsed.Header.Ons,
            has = parsed.Header.Has,
            gumusHas = parsed.Header.GumusHas
        },
        items
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

// Company info (single row)
app.MapGet("/api/company-info", async (KtpDbContext db) =>
{
    var info = await db.CompanyInfos.AsNoTracking().OrderBy(x => x.UpdatedAt).FirstOrDefaultAsync();
    return Results.Ok(new
    {
        companyName = info?.CompanyName ?? string.Empty,
        taxNo = info?.TaxNo ?? string.Empty,
        address = info?.Address ?? string.Empty,
        tradeRegistryNo = info?.TradeRegistryNo ?? string.Empty,
        phone = info?.Phone ?? string.Empty,
        email = info?.Email ?? string.Empty,
        cityName = info?.CityName ?? string.Empty,
        townName = info?.TownName ?? string.Empty,
        postalCode = info?.PostalCode ?? string.Empty,
        taxOfficeName = info?.TaxOfficeName ?? string.Empty
    });
}).RequireAuthorization();

app.MapPut("/api/company-info", async (UpdateCompanyInfoRequest req, KtpDbContext db, HttpContext http) =>
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

    var info = await db.CompanyInfos.FirstOrDefaultAsync();
    if (info is null)
    {
        info = new CompanyInfo { Id = Guid.NewGuid() };
        db.CompanyInfos.Add(info);
    }

    info.CompanyName = req.companyName?.Trim();
    info.TaxNo = req.taxNo?.Trim();
    info.Address = req.address?.Trim();
    info.TradeRegistryNo = req.tradeRegistryNo?.Trim();
    info.Phone = req.phone?.Trim();
    info.Email = req.email?.Trim();
    info.CityName = req.cityName?.Trim();
    info.TownName = req.townName?.Trim();
    info.PostalCode = req.postalCode?.Trim();
    info.TaxOfficeName = req.taxOfficeName?.Trim();
    info.UpdatedAt = DateTime.UtcNow;

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

static bool IsValidVkn(string vkn)
{
    if (string.IsNullOrWhiteSpace(vkn)) return false;
    if (vkn.Length != 10 || !vkn.All(char.IsDigit)) return false;

    var digits = vkn.Select(c => c - '0').ToArray();
    var sum = 0;
    for (var i = 0; i < 9; i++)
    {
        var digit = digits[i];
        var tmp = (digit + 10 - (i + 1)) % 10;
        var pow = (int)(Math.Pow(2, 9 - i) % 9);
        var res = (tmp * pow) % 9;
        if (tmp != 0 && res == 0) res = 9;
        sum += res;
    }
    var checkDigit = (10 - (sum % 10)) % 10;
    return digits[9] == checkDigit;
}

static async Task<bool> HasCategoryCycleAsync(KtpDbContext db, Guid categoryId, Guid parentId, CancellationToken ct)
{
    var current = parentId;
    while (true)
    {
        if (current == categoryId) return true;
        var next = await db.Categories.AsNoTracking()
            .Where(x => x.Id == current)
            .Select(x => x.ParentId)
            .FirstOrDefaultAsync(ct);
        if (!next.HasValue) return false;
        current = next.Value;
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
CREATE TABLE IF NOT EXISTS market.""GoldFeedNewVersion"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""RawResponse"" text NOT NULL,
    ""FetchTime"" timestamptz NOT NULL,
    ""IsParsed"" boolean NOT NULL,
    ""ParseError"" text NULL
);
CREATE INDEX IF NOT EXISTS IX_GoldFeedNewVersion_FetchTime ON market.""GoldFeedNewVersion"" (""FetchTime"");
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

static async Task<(Guid? saleTemplateId, Guid? purchaseTemplateId)> TryGetDefaultFormulaTemplatesForAyarAsync(
    KtpDbContext db,
    AltinAyar ayar,
    CancellationToken ct)
{
    var saleCode = ayar == AltinAyar.Ayar22 ? "DEFAULT_22_SALE" : "DEFAULT_24_SALE";
    var purchaseCode = ayar == AltinAyar.Ayar22 ? "DEFAULT_22_PURCHASE" : "DEFAULT_24_PURCHASE";

    var saleTemplate = await db.GoldFormulaTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Code == saleCode, ct);
    if (saleTemplate is null) return (null, null);
    var purchaseTemplate = await db.GoldFormulaTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Code == purchaseCode, ct);
    if (purchaseTemplate is null) return (null, null);

    return (saleTemplate.Id, purchaseTemplate.Id);
}

static async Task EnsureFormulaBindingRowAsync(
    KtpDbContext db,
    Guid productId,
    Guid templateId,
    GoldFormulaDirection direction,
    CancellationToken ct)
{
    var exists = await db.GoldProductFormulaBindings.AsNoTracking()
        .AnyAsync(x => x.GoldProductId == productId && x.FormulaTemplateId == templateId && x.Direction == direction && x.IsActive, ct);
    if (exists) return;

    db.GoldProductFormulaBindings.Add(new GoldProductFormulaBinding
    {
        Id = Guid.NewGuid(),
        GoldProductId = productId,
        FormulaTemplateId = templateId,
        Direction = direction,
        IsActive = true
    });
}

static async Task<(GoldFormulaTemplate? Template, string? Error)> ResolveFormulaTemplateAsync(
    KtpDbContext db,
    Product product,
    GoldFormulaDirection direction,
    CancellationToken ct)
{
    GoldFormulaTemplate? template = null;

    if (product.RequiresFormula)
    {
        var binding = await db.GoldProductFormulaBindings.AsNoTracking()
            .Where(x => x.GoldProductId == product.Id && x.Direction == direction && x.IsActive)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        if (binding is not null)
            template = await db.GoldFormulaTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == binding.FormulaTemplateId, ct);
        else if (product.DefaultFormulaId.HasValue)
            template = await db.GoldFormulaTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == product.DefaultFormulaId.Value, ct);
        else
            return (null, direction == GoldFormulaDirection.Sale
                ? "Ürün için satış formülü tanımlı değil"
                : "Ürün için alış formülü tanımlı değil");
    }
    else if (product.DefaultFormulaId.HasValue)
    {
        template = await db.GoldFormulaTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == product.DefaultFormulaId.Value, ct);
    }

    if (template is null) return (null, "Formül bulunamadı");
    if (!template.IsActive) return (null, "Formül aktif değil");

    if (template.FormulaType != GoldFormulaType.Both)
    {
        if (direction == GoldFormulaDirection.Sale && template.FormulaType != GoldFormulaType.Sale)
            return (null, "Formül satış için uygun değil");
        if (direction == GoldFormulaDirection.Purchase && template.FormulaType != GoldFormulaType.Purchase)
            return (null, "Formül alış için uygun değil");
    }

    return (template, null);
}

static async Task<(decimal? Price, string? Error)> ResolveHasGoldPriceAsync(
    MarketDbContext mdb,
    KtpDbContext db,
    Guid productId,
    GoldFormulaDirection direction,
    CancellationToken ct)
{
    var ayar = await ProductAyarResolver.TryResolveAsync(db, productId, ct);
    if (ayar.HasValue)
    {
        var priceData = await mdb.GetLatestPriceForAyarAsync(ayar.Value, useBuyMargin: direction == GoldFormulaDirection.Purchase, ct);
        if (priceData is null) return (null, "Kayıtlı altın fiyatı bulunamadı");
        return (priceData.Price, null);
    }

    var record = await mdb.GetLatestAltinRecordAsync(ct);
    if (record is null) return (null, "Kayıtlı altın fiyatı bulunamadı");
    var price = direction == GoldFormulaDirection.Purchase ? record.FinalAlis : record.FinalSatis;
    return (price, null);
}

static (Guid? UserId, string? Email) GetCurrentUserInfo(HttpContext http)
{
    var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.User.FindFirst("sub")?.Value;
    Guid? uid = Guid.TryParse(sub, out var uidVal) ? uidVal : null;
    if (uid is null)
    {
        var hdrSub = http.Request.Headers["X-User-Id"].FirstOrDefault();
        if (Guid.TryParse(hdrSub, out var uid2)) uid = uid2;
    }

    var email = http.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
        ?? http.User.FindFirst(ClaimTypes.Email)?.Value
        ?? http.Request.Headers["X-User-Email"].FirstOrDefault();

    return (uid, email);
}

static decimal? TryGetVariable(IReadOnlyDictionary<string, decimal> variables, string key)
{
    if (variables.TryGetValue(key, out var value))
        return value;
    return null;
}

FormulaPreviewTheme? TryParsePreviewTheme(string definitionJson)
{
    if (string.IsNullOrWhiteSpace(definitionJson))
        return null;
    try
    {
        var parsed = JsonSerializer.Deserialize<FormulaPreviewDefinition>(definitionJson, previewJsonOptions);
        return parsed?.Preview;
    }
    catch (JsonException)
    {
        return null;
    }
}

static FormulaPreviewTheme? NormalizePreviewTheme(FormulaPreviewTheme? theme)
{
    if (theme is null) return null;
    var fields = theme.Fields ?? new List<FormulaPreviewField>();
    var normalized = fields
        .Where(x => !string.IsNullOrWhiteSpace(x.Key))
        .Select(x => new FormulaPreviewField
        {
            Key = x.Key?.Trim(),
            Label = string.IsNullOrWhiteSpace(x.Label) ? null : x.Label.Trim(),
            Format = NormalizePreviewFormat(x.Format)
        })
        .ToList();
    if (normalized.Count == 0) return null;
    return new FormulaPreviewTheme
    {
        Title = string.IsNullOrWhiteSpace(theme.Title) ? null : theme.Title.Trim(),
        Fields = normalized
    };
}

static string NormalizePreviewFormat(string? format)
{
    if (string.IsNullOrWhiteSpace(format)) return "number";
    var normalized = format.Trim().ToLowerInvariant();
    return normalized is "currency" or "number" or "text" ? normalized : "number";
}

List<object> BuildPreviewFields(
    FormulaPreviewTheme? theme,
    GoldCalculationResult result,
    IReadOnlyDictionary<string, decimal> variables)
{
    var fields = theme?.Fields?.Count > 0 ? theme.Fields : defaultPreviewTheme.Fields;
    var output = new List<object>();
    foreach (var field in fields)
    {
        var key = field.Key?.Trim();
        if (string.IsNullOrWhiteSpace(key)) continue;
        if (TryGetResultValue(result, key, out var value) || TryGetVariableValue(variables, key, out value))
        {
            output.Add(new
            {
                key,
                label = string.IsNullOrWhiteSpace(field.Label) ? key : field.Label,
                format = NormalizePreviewFormat(field.Format),
                value
            });
        }
    }
    return output;
}

static bool TryGetResultValue(GoldCalculationResult result, string key, out decimal value)
{
    switch (key.Trim().ToLowerInvariant())
    {
        case "gram":
            value = result.Gram;
            return true;
        case "amount":
            value = result.Amount;
            return true;
        case "goldservice":
            value = result.GoldServiceAmount;
            return true;
        case "laborgross":
            value = result.LaborGross;
            return true;
        case "labornet":
            value = result.LaborNet;
            return true;
        case "vat":
            value = result.Vat;
            return true;
        case "unithaspriceused":
            value = result.UnitHasPriceUsed;
            return true;
        default:
            value = 0m;
            return false;
    }
}

static bool TryGetVariableValue(IReadOnlyDictionary<string, decimal> variables, string key, out decimal value)
{
    if (variables.TryGetValue(key, out value)) return true;
    foreach (var pair in variables)
    {
        if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
        {
            value = pair.Value;
            return true;
        }
    }
    value = 0m;
    return false;
}

sealed class FormulaPreviewDefinition
{
    public FormulaPreviewTheme? Preview { get; set; }
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
public record UpdateCompanyInfoRequest(
    string? companyName,
    string? taxNo,
    string? address,
    string? tradeRegistryNo,
    string? phone,
    string? email,
    string? cityName,
    string? townName,
    string? postalCode,
    string? taxOfficeName);
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
public record UpdateInvoicePreviewRequest(decimal Tutar, decimal GramDegeri, string Mode, int? AltinAyar);
public record UpdateExpensePreviewRequest(decimal Tutar, decimal GramDegeri, string Mode, int? AltinAyar);
public record FinalizeRequest(decimal UrunFiyati);
public record GoldPriceUpdateRequest(decimal Price);
public record GoldOpeningInventoryRequest(int Karat, decimal Gram, DateTime Date, string? Description);
public record ProductCreateRequest(string Code, string Name, bool? IsActive, bool? ShowInSales, int? AccountingType, decimal? Gram, bool? RequiresFormula, Guid? DefaultFormulaId);
public record ProductUpdateRequest(string Code, string Name, bool? IsActive, bool? ShowInSales, int? AccountingType, decimal? Gram, bool? RequiresFormula, Guid? DefaultFormulaId);
public record ProductOpeningInventoryRequest(Guid ProductId, decimal Quantity, DateTime Date);
public record CategoryCreateRequest(string Name, Guid? ParentId);
public record CategoryUpdateRequest(string Name, Guid? ParentId);
public record CategoryProductCreateRequest(Guid CategoryId, Guid ProductId);
public record CategoryProductUpdateRequest(Guid CategoryId, Guid ProductId);
public record FormulaUpsertRequest(string Code, string Name, GoldFormulaScope? Scope, GoldFormulaType? FormulaType, int? DslVersion, string DefinitionJson, bool? IsActive);
public record FormulaValidateRequest(decimal? Amount, decimal? HasGoldPrice, decimal? VatRate, decimal? ProductGram, int? AccountingType, GoldFormulaDirection? Direction, GoldFormulaOperationType? OperationType, GoldFormulaMode? Mode, decimal? AltinSatisFiyati);
public record FormulaBindingCreateRequest(Guid ProductId, Guid TemplateId, GoldFormulaDirection Direction, bool? IsActive);
public record FormulaBindingUpdateRequest(Guid? TemplateId, GoldFormulaDirection? Direction, bool? IsActive);
public record FormulaPreviewTheme
{
    public string? Title { get; set; }
    public List<FormulaPreviewField> Fields { get; set; } = new();
}
public record FormulaPreviewField
{
    public string? Key { get; set; }
    public string? Label { get; set; }
    public string? Format { get; set; }
}
public record PosPreviewRequest(Guid ProductId, decimal Amount, GoldFormulaDirection Direction, GoldFormulaOperationType OperationType, decimal? VatRate);
public record PosFinalizeRequest(Guid ProductId, decimal Amount, GoldFormulaDirection Direction, GoldFormulaOperationType OperationType, decimal? VatRate, OdemeSekli? OdemeSekli, string? PreviewHash, string? IdempotencyKey);

public record KaratRange(double min, double max, string colorHex);
public record KaratDiffSettings
{
    public KaratRange[] ranges { get; set; } = Array.Empty<KaratRange>();
    public double alertThreshold { get; set; } = 1000;
}
public record UpdateKaratSettingsRequest(KaratRange[] ranges, double alertThreshold);

public record PrintMultiRequest([property: Required] List<string> Values);
