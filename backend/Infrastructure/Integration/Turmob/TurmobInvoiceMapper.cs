using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using KuyumculukTakipProgrami.Application.DTOs;

namespace KuyumculukTakipProgrami.Infrastructure.Integration.Turmob;

public sealed class TurmobInvoiceMapper
{
    private static readonly XNamespace SoapEnv = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace TempUri = "http://tempuri.org/";
    private static readonly XNamespace Einvoice = "http://schemas.datacontract.org/2004/07/EInvoice.Service.Model";
    private static readonly XNamespace Arrays = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";

    public string MapToArchiveInvoiceXml(TurmobInvoiceDto invoice, TurmobEnvironmentOptions environment)
    {
        if (!invoice.IsArchive)
        {
            throw new InvalidOperationException("Archive invoice mapping requires IsArchive == true.");
        }

        var notes = invoice.Notes.Count == 0 ? new[] { "." } : invoice.Notes;
        var currencyCode = string.IsNullOrWhiteSpace(invoice.CurrencyCode) ? "TRY" : invoice.CurrencyCode;
        var receiverTaxCode = invoice.Receiver.ReceiverTaxCode;
        if (invoice.IsArchive && IsTestEnvironment(environment))
        {
            receiverTaxCode = "11111111111";
        }
        var detailElements = invoice.InvoiceDetails.Select(detail =>
            new XElement(Einvoice + "ArchiveInvoiceDetail",
                new XElement(Einvoice + "CurrencyCode",
                    string.IsNullOrWhiteSpace(detail.CurrencyCode) ? currencyCode : detail.CurrencyCode),
                new XElement(Einvoice + "LineExtensionAmount", detail.LineExtensionAmount),
                new XElement(Einvoice + "Product",
                    new XElement(Einvoice + "ExternalProductCode", detail.Product.ExternalProductCode),
                    new XElement(Einvoice + "MeasureUnit", detail.Product.MeasureUnit),
                    new XElement(Einvoice + "ProductCode", detail.Product.ProductCode),
                    new XElement(Einvoice + "ProductName", detail.Product.ProductName),
                    new XElement(Einvoice + "UnitPrice", detail.Product.UnitPrice)),
                new XElement(Einvoice + "Quantity", detail.Quantity),
                CreateOptionalElement(Einvoice + "TaxExemptionReason", detail.TaxExemptionReason),
                new XElement(Einvoice + "VATAmount", detail.VATAmount),
                new XElement(Einvoice + "VATRate", detail.VATRate)));

        var archiveInvoice = new XElement(Einvoice + "ArchiveInvoice",
            new XElement(Einvoice + "CompanyBranchAddress",
                new XElement(Einvoice + "BoulevardAveneuStreetName",
                    invoice.CompanyBranchAddress.BoulevardAveneuStreetName),
                new XElement(Einvoice + "CityName", invoice.CompanyBranchAddress.CityName),
                new XElement(Einvoice + "TaxOfficeName", invoice.CompanyBranchAddress.TaxOfficeName),
                new XElement(Einvoice + "EMail", invoice.CompanyBranchAddress.Email)),
            new XElement(Einvoice + "CurrencyCode", currencyCode),
            new XElement(Einvoice + "ExternalArchiveInvoiceCode", invoice.ExternalArchiveInvoiceCode),
            new XElement(Einvoice + "InvoiceDate", invoice.InvoiceDate),
            new XElement(Einvoice + "InvoiceDetails", detailElements),
            new XElement(Einvoice + "InvoiceType", invoice.InvoiceType),
            new XElement(Einvoice + "IsArchived", invoice.IsArchived.ToString().ToLowerInvariant()),
            new XElement(Einvoice + "Notes",
                notes.Select(note => new XElement(Arrays + "string", note))),
            new XElement(Einvoice + "OrderDate", invoice.OrderDate),
            new XElement(Einvoice + "OrderNumber", invoice.OrderNumber),
            new XElement(Einvoice + "Receiver",
                new XElement(Einvoice + "Address",
                    new XElement(Einvoice + "CityCode", invoice.Receiver.Address.CityCode),
                    new XElement(Einvoice + "CityName", invoice.Receiver.Address.CityName),
                    new XElement(Einvoice + "EMail", invoice.Receiver.Address.Email)),
                new XElement(Einvoice + "ReceiverName", invoice.Receiver.ReceiverName),
                new XElement(Einvoice + "ReceiverTaxCode", receiverTaxCode),
                new XElement(Einvoice + "SendingType", invoice.Receiver.SendingType)),
            new XElement(Einvoice + "ReceiverBranchAddress",
                new XElement(Einvoice + "CityCode", invoice.ReceiverBranchAddress.CityCode),
                new XElement(Einvoice + "CityName", invoice.ReceiverBranchAddress.CityName),
                new XElement(Einvoice + "PostalCode", invoice.ReceiverBranchAddress.PostalCode),
                new XElement(Einvoice + "TaxOfficeName", invoice.ReceiverBranchAddress.TaxOfficeName),
                new XElement(Einvoice + "EMail", invoice.ReceiverBranchAddress.Email)),
            new XElement(Einvoice + "SendMailAutomatically",
                invoice.SendMailAutomatically.ToString().ToLowerInvariant()),
            new XElement(Einvoice + "TotalDiscountAmount", invoice.TotalDiscountAmount),
            new XElement(Einvoice + "TotalLineExtensionAmount", invoice.TotalLineExtensionAmount),
            new XElement(Einvoice + "TotalPayableAmount", invoice.TotalPayableAmount),
            new XElement(Einvoice + "TotalTaxInclusiveAmount", invoice.TotalTaxInclusiveAmount),
            new XElement(Einvoice + "TotalVATAmount", invoice.TotalVATAmount),
            new XElement(Einvoice + "XsltTemplate", environment.TemplateCode));

        var request = new XElement(TempUri + "request",
            new XElement(Einvoice + "ArchiveInvoices", archiveInvoice),
            new XElement(Einvoice + "CompanyTaxCode", invoice.CompanyTaxCode));

        var envelope = new XElement(SoapEnv + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", SoapEnv),
            new XAttribute(XNamespace.Xmlns + "tem", TempUri),
            new XAttribute(XNamespace.Xmlns + "ein", Einvoice),
            new XAttribute(XNamespace.Xmlns + "arr", Arrays),
            new XElement(SoapEnv + "Header"),
            new XElement(SoapEnv + "Body",
                new XElement(TempUri + "SendArchiveInvoice", request)));

        var document = new XDocument(new XDeclaration("1.0", "UTF-8", null), envelope);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    public string MapToInvoiceXml(TurmobInvoiceDto invoice, TurmobEnvironmentOptions environment)
    {
        if (invoice.IsArchive)
        {
            throw new InvalidOperationException("Company invoice mapping requires IsArchive == false.");
        }

        var notes = invoice.Notes.Count == 0 ? new[] { "." } : invoice.Notes;
        var currencyCode = string.IsNullOrWhiteSpace(invoice.CurrencyCode) ? "TRY" : invoice.CurrencyCode;
        var receiverTaxCode = invoice.Receiver.ReceiverTaxCode;
        if (!invoice.IsArchive && IsTestEnvironment(environment) && receiverTaxCode?.Length == 11)
        {
            receiverTaxCode = "11111111111";
        }
        var detailElements = invoice.InvoiceDetails.Select(detail =>
            new XElement(Einvoice + "InvoiceDetail",
                new XElement(Einvoice + "CurrencyCode",
                    string.IsNullOrWhiteSpace(detail.CurrencyCode) ? currencyCode : detail.CurrencyCode),
                new XElement(Einvoice + "LineExtensionAmount", detail.LineExtensionAmount),
                new XElement(Einvoice + "Product",
                    new XElement(Einvoice + "ExternalProductCode", detail.Product.ExternalProductCode),
                    new XElement(Einvoice + "MeasureUnit", detail.Product.MeasureUnit),
                    new XElement(Einvoice + "ProductCode", detail.Product.ProductCode),
                    new XElement(Einvoice + "ProductName", detail.Product.ProductName),
                    new XElement(Einvoice + "UnitPrice", detail.Product.UnitPrice)),
                new XElement(Einvoice + "Quantity", detail.Quantity),
                CreateOptionalElement(Einvoice + "TaxExemptionReason", detail.TaxExemptionReason),
                new XElement(Einvoice + "VATAmount", detail.VATAmount),
                new XElement(Einvoice + "VATRate", detail.VATRate)));

        var dispatchElements = invoice.Dispatches.Select(dispatch =>
            new XElement(Einvoice + "Dispatch",
                new XElement(Einvoice + "DispatchDate", dispatch.DispatchDate),
                new XElement(Einvoice + "DispatchNumber", dispatch.DispatchNumber)));

        var invoiceElement = new XElement(Einvoice + "Invoice",
            new XElement(Einvoice + "CurrencyCode", currencyCode),
            new XElement(Einvoice + "CompanyBranchAddress",
                new XElement(Einvoice + "BoulevardAveneuStreetName",
                    invoice.CompanyBranchAddress.BoulevardAveneuStreetName),
                new XElement(Einvoice + "CityName", invoice.CompanyBranchAddress.CityName),
                new XElement(Einvoice + "PostalCode", invoice.CompanyBranchAddress.PostalCode),
                new XElement(Einvoice + "TownName", invoice.CompanyBranchAddress.TownName)),
            new XElement(Einvoice + "DispatchList", dispatchElements),
            new XElement(Einvoice + "ExternalInvoiceCode", invoice.ExternalInvoiceCode),
            new XElement(Einvoice + "InvoiceDate", invoice.InvoiceDate),
            new XElement(Einvoice + "InvoiceDetails", detailElements),
            new XElement(Einvoice + "InvoiceType", invoice.InvoiceType),
            new XElement(Einvoice + "Notes",
                notes.Select(note => new XElement(Arrays + "string", note))),
            new XElement(Einvoice + "OrderDate", invoice.OrderDate),
            new XElement(Einvoice + "OrderNumber", invoice.OrderNumber),
            new XElement(Einvoice + "Receiver",
                new XElement(Einvoice + "ReceiverName", invoice.Receiver.ReceiverName),
                new XElement(Einvoice + "ReceiverTaxCode", receiverTaxCode),
                new XElement(Einvoice + "RecipientType", invoice.Receiver.RecipientType),
                new XElement(Einvoice + "SendingType", invoice.Receiver.SendingType)),
            new XElement(Einvoice + "ReceiverBranchAddress",
                new XElement(Einvoice + "CityCode", invoice.ReceiverBranchAddress.CityCode),
                new XElement(Einvoice + "CityName", invoice.ReceiverBranchAddress.CityName),
                new XElement(Einvoice + "PostalCode", invoice.ReceiverBranchAddress.PostalCode),
                new XElement(Einvoice + "TaxOfficeName", invoice.ReceiverBranchAddress.TaxOfficeName),
                new XElement(Einvoice + "EMail", invoice.ReceiverBranchAddress.Email)),
            new XElement(Einvoice + "ReceiverInboxTag", invoice.ReceiverInboxTag),
            new XElement(Einvoice + "ScenarioType", invoice.ScenarioType),
            new XElement(Einvoice + "TotalLineExtensionAmount", invoice.TotalLineExtensionAmount),
            new XElement(Einvoice + "TotalPayableAmount", invoice.TotalPayableAmount),
            new XElement(Einvoice + "TotalTaxInclusiveAmount", invoice.TotalTaxInclusiveAmount),
            new XElement(Einvoice + "TotalVATAmount", invoice.TotalVATAmount));

        var request = new XElement(TempUri + "request",
            new XElement(Einvoice + "CompanyTaxCode", invoice.CompanyTaxCode),
            new XElement(Einvoice + "Invoices", invoiceElement));

        var envelope = new XElement(SoapEnv + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", SoapEnv),
            new XAttribute(XNamespace.Xmlns + "tem", TempUri),
            new XAttribute(XNamespace.Xmlns + "ein", Einvoice),
            new XAttribute(XNamespace.Xmlns + "arr", Arrays),
            new XElement(SoapEnv + "Header"),
            new XElement(SoapEnv + "Body",
                new XElement(TempUri + "SendInvoice", request)));

        var document = new XDocument(new XDeclaration("1.0", "UTF-8", null), envelope);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement CreateOptionalElement(XName name, string value)
    {
        return string.IsNullOrWhiteSpace(value) ? new XElement(name) : new XElement(name, value);
    }

    private static bool IsTestEnvironment(TurmobEnvironmentOptions environment)
    {
        return !string.IsNullOrWhiteSpace(environment.ServiceUrl)
            && environment.ServiceUrl.Contains("test", StringComparison.OrdinalIgnoreCase);
    }
}
