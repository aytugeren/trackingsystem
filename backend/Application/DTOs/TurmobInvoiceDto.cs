namespace KuyumculukTakipProgrami.Application.DTOs;

public sealed class TurmobInvoiceDto
{
    public bool IsArchive { get; init; }
    public string CompanyTaxCode { get; init; } = string.Empty;
    public TurmobCompanyBranchAddressDto CompanyBranchAddress { get; init; } = new();
    public string CurrencyCode { get; init; } = "TRY";
    public string ExternalArchiveInvoiceCode { get; init; } = string.Empty;
    public string ExternalInvoiceCode { get; init; } = string.Empty;
    public string InvoiceDate { get; init; } = string.Empty;
    public string InvoiceType { get; init; } = "SATIS";
    public bool IsArchived { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
    public string OrderDate { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public TurmobReceiverDto Receiver { get; init; } = new();
    public TurmobReceiverBranchAddressDto ReceiverBranchAddress { get; init; } = new();
    public IReadOnlyList<TurmobDispatchDto> Dispatches { get; init; } = Array.Empty<TurmobDispatchDto>();
    public string ReceiverInboxTag { get; init; } = string.Empty;
    public string ScenarioType { get; init; } = string.Empty;
    public bool SendMailAutomatically { get; init; }
    public string TotalDiscountAmount { get; init; } = "0";
    public string TotalLineExtensionAmount { get; init; } = string.Empty;
    public string TotalPayableAmount { get; init; } = string.Empty;
    public string TotalTaxInclusiveAmount { get; init; } = string.Empty;
    public string TotalVATAmount { get; init; } = string.Empty;
    public IReadOnlyList<TurmobInvoiceDetailDto> InvoiceDetails { get; init; } = Array.Empty<TurmobInvoiceDetailDto>();
}

public sealed class TurmobCompanyBranchAddressDto
{
    public string BoulevardAveneuStreetName { get; init; } = string.Empty;
    public string CityName { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string TownName { get; init; } = string.Empty;
    public string TaxOfficeName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class TurmobReceiverDto
{
    public TurmobReceiverAddressDto Address { get; init; } = new();
    public string ReceiverName { get; init; } = string.Empty;
    public string ReceiverTaxCode { get; init; } = string.Empty;
    public string SendingType { get; init; } = "KAGIT";
    public string RecipientType { get; init; } = string.Empty;
}

public sealed class TurmobReceiverAddressDto
{
    public string CityCode { get; init; } = string.Empty;
    public string CityName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class TurmobReceiverBranchAddressDto
{
    public string CityCode { get; init; } = string.Empty;
    public string CityName { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string TaxOfficeName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class TurmobInvoiceDetailDto
{
    public string CurrencyCode { get; init; } = "TRY";
    public string LineExtensionAmount { get; init; } = string.Empty;
    public TurmobProductDto Product { get; init; } = new();
    public string Quantity { get; init; } = string.Empty;
    public string TaxExemptionReason { get; init; } = string.Empty;
    public string VATAmount { get; init; } = string.Empty;
    public string VATRate { get; init; } = string.Empty;
}

public sealed class TurmobProductDto
{
    public string ExternalProductCode { get; init; } = string.Empty;
    public string MeasureUnit { get; init; } = string.Empty;
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string UnitPrice { get; init; } = string.Empty;
}

public sealed class TurmobDispatchDto
{
    public string DispatchDate { get; init; } = string.Empty;
    public string DispatchNumber { get; init; } = string.Empty;
}
