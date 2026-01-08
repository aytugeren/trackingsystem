using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KuyumculukTakipProgrami.Application.DTOs;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KuyumculukTakipProgrami.Infrastructure.Integration.Turmob;

public sealed class TurmobInvoiceBuilder
{
    private const string DefaultCurrency = "TRY";
    private const string DefaultInvoiceType = "SATIS";
    private const string DefaultScenario = "TEMELFATURA";
    private const string DefaultSendingTypeArchive = "KAGIT";
    private const string DefaultRecipientType = "NONE";
    private const string DefaultSendingTypeInvoice = "NONE";
    private const string DefaultCityCode = "34";

    private readonly KtpDbContext _db;
    private readonly ILogger<TurmobInvoiceBuilder> _logger;
    private readonly IOptionsMonitor<TurmobOptions> _options;

    public TurmobInvoiceBuilder(
        KtpDbContext db,
        ILogger<TurmobInvoiceBuilder> logger,
        IOptionsMonitor<TurmobOptions> options)
    {
        _db = db;
        _logger = logger;
        _options = options;
    }

    public async Task<TurmobInvoiceDto?> BuildAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);

        if (invoice is null)
        {
            return null;
        }

        var company = await _db.CompanyInfos
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            _logger.LogWarning("TURMOB CompanyInfo not found. InvoiceId: {InvoiceId}", invoiceId);
        }

        var invoiceDate = FormatDate(invoice.Tarih);
        var externalCode = $"{invoice.Tarih:yyyyMMdd}{invoice.SiraNo}";
        var orderNumber = invoice.SiraNo.ToString("D5", CultureInfo.InvariantCulture);

        var gram = invoice.GramDegeri ?? 0m;
        var safAltin = invoice.SafAltinDegeri ?? 0m;
        var altinLineAmount = gram * safAltin;
        var iscilik = invoice.Iscilik ?? 0m;
        var iscilikVat = iscilik * 0.20m;

        var totalLineExtension = altinLineAmount + iscilik;
        var totalPayable = invoice.UrunFiyati ?? invoice.Tutar;
        if (totalPayable <= 0m)
        {
            totalPayable = totalLineExtension + iscilikVat;
        }

        var details = BuildDetails(invoice, gram, safAltin, iscilik);
        var customerName = invoice.Customer?.AdSoyad ?? invoice.MusteriAdSoyad ?? string.Empty;
        var customerTckn = invoice.Customer?.TCKN ?? invoice.TCKN ?? string.Empty;
        if (IsTestEnvironment())
        {
            customerTckn = "11111111111";
        }
        var companyName = invoice.Customer?.CompanyName ?? customerName;
        var companyTaxCode = invoice.Customer?.VknNo ?? string.Empty;

        return new TurmobInvoiceDto
        {
            IsArchive = !invoice.IsForCompany,
            CompanyTaxCode = company?.TaxNo ?? string.Empty,
            CompanyBranchAddress = new TurmobCompanyBranchAddressDto
            {
                BoulevardAveneuStreetName = company?.Address ?? string.Empty,
                CityName = company?.CityName ?? string.Empty,
                PostalCode = company?.PostalCode ?? string.Empty,
                TownName = company?.TownName ?? string.Empty,
                TaxOfficeName = company?.TaxOfficeName ?? string.Empty,
                Email = company?.Email ?? string.Empty
            },
            CurrencyCode = DefaultCurrency,
            ExternalArchiveInvoiceCode = externalCode,
            ExternalInvoiceCode = externalCode,
            InvoiceDate = invoiceDate,
            InvoiceType = DefaultInvoiceType,
            IsArchived = false,
            Notes = new[] { "." },
            OrderDate = invoiceDate,
            OrderNumber = orderNumber,
            Receiver = invoice.IsForCompany
                ? new TurmobReceiverDto
                {
                    ReceiverName = companyName,
                    ReceiverTaxCode = companyTaxCode,
                    RecipientType = DefaultRecipientType,
                    SendingType = DefaultSendingTypeInvoice
                }
                : new TurmobReceiverDto
                {
                    Address = new TurmobReceiverAddressDto
                    {
                        CityCode = DefaultCityCode,
                        CityName = company?.CityName ?? string.Empty,
                        Email = company?.Email ?? string.Empty
                    },
                    ReceiverName = customerName,
                    ReceiverTaxCode = customerTckn,
                    SendingType = DefaultSendingTypeArchive
                },
            ReceiverBranchAddress = new TurmobReceiverBranchAddressDto
            {
                CityCode = DefaultCityCode,
                CityName = FormatCityTown(company?.TownName, company?.CityName),
                PostalCode = company?.PostalCode ?? string.Empty,
                TaxOfficeName = company?.TaxOfficeName ?? string.Empty,
                Email = company?.Email ?? string.Empty
            },
            Dispatches = new[]
            {
                new TurmobDispatchDto
                {
                    DispatchDate = FormatDispatchDate(invoice.Tarih),
                    DispatchNumber = $"A-{invoice.SiraNo}"
                }
            },
            ReceiverInboxTag = "urn:mail:defaultpk@luca.com.tr",
            ScenarioType = DefaultScenario,
            SendMailAutomatically = false,
            TotalDiscountAmount = "0",
            TotalLineExtensionAmount = FormatDecimal(totalLineExtension),
            TotalPayableAmount = FormatDecimal(totalPayable),
            TotalTaxInclusiveAmount = FormatDecimal(totalPayable),
            TotalVATAmount = FormatDecimal(iscilikVat),
            InvoiceDetails = details
        };
    }

    private static IReadOnlyList<TurmobInvoiceDetailDto> BuildDetails(Invoice invoice, decimal gram, decimal safAltin, decimal iscilik)
    {
        var details = new List<TurmobInvoiceDetailDto>();
        if (gram > 0m && safAltin > 0m)
        {
            details.Add(new TurmobInvoiceDetailDto
            {
                CurrencyCode = DefaultCurrency,
                LineExtensionAmount = FormatDecimal(gram * safAltin),
                Product = new TurmobProductDto
                {
                    ExternalProductCode = "ERN0001",
                    MeasureUnit = "GRM",
                    ProductCode = "001",
                    ProductName = $"{(int)invoice.AltinAyar} AYAR ALTIN",
                    UnitPrice = FormatDecimal(safAltin)
                },
                Quantity = FormatDecimal(gram),
                TaxExemptionReason = ".",
                VATAmount = "0",
                VATRate = "0.00"
            });
        }

        if (iscilik > 0m)
        {
            details.Add(new TurmobInvoiceDetailDto
            {
                CurrencyCode = DefaultCurrency,
                LineExtensionAmount = FormatDecimal(iscilik),
                Product = new TurmobProductDto
                {
                    ExternalProductCode = "ERN0002",
                    MeasureUnit = "NIU",
                    ProductCode = "001",
                    ProductName = "ISCILIK",
                    UnitPrice = FormatDecimal(iscilik)
                },
                Quantity = "1",
                VATAmount = FormatDecimal(iscilik * 0.20m),
                VATRate = "20.00"
            });
        }

        return details;
    }

    private bool IsTestEnvironment()
    {
        return string.Equals(_options.CurrentValue.Environment, "Test", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatCityTown(string? townName, string? cityName)
    {
        townName = townName?.Trim();
        cityName = cityName?.Trim();
        if (!string.IsNullOrWhiteSpace(townName) && !string.IsNullOrWhiteSpace(cityName))
        {
            return $"{townName}/{cityName}";
        }

        return townName ?? cityName ?? string.Empty;
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatDispatchDate(DateOnly date)
    {
        return date.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
