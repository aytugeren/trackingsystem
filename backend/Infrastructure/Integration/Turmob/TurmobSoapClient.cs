using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using KuyumculukTakipProgrami.Application.DTOs;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace KuyumculukTakipProgrami.Infrastructure.Integration.Turmob;

public sealed class TurmobSoapClient
{
    private const string ArchiveAction = "SendArchiveInvoice";
    private const string InvoiceAction = "SendInvoice";
    private const string SoapActionBase = "http://tempuri.org/IInvoiceService/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TurmobSoapClient> _logger;

    public TurmobSoapClient(IHttpClientFactory httpClientFactory, ILogger<TurmobSoapClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<TurmobSendResult> SendAsync(
        string xmlPayload,
        bool isArchive,
        TurmobEnvironmentOptions environment,
        TurmobOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(xmlPayload))
        {
            _logger.LogError("TURMOB XML payload is empty.");
            return Task.FromResult(TurmobSendResult.Failed("TURMOB XML payload is empty."));
        }

        var action = isArchive ? ArchiveAction : InvoiceAction;
        var soapAction = $"{SoapActionBase}{action}";
        return SendInternalAsync(xmlPayload, environment, options, action, soapAction, cancellationToken);
    }

    private async Task<TurmobSendResult> SendInternalAsync(
        string xmlPayload,
        TurmobEnvironmentOptions environment,
        TurmobOptions options,
        string action,
        string soapAction,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds = Math.Max(environment.TimeoutSeconds, 60);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var client = _httpClientFactory.CreateClient("TurmobSoap");
            var content = new StringContent(xmlPayload, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/xml")
            {
                CharSet = "utf-8"
            };
            var request = new HttpRequestMessage(HttpMethod.Post, environment.ServiceUrl)
            {
                Content = content
            };
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            request.Headers.TryAddWithoutValidation("SOAPAction", soapAction);

            if (options.Logging.LogRequest)
            {
                _logger.LogInformation("TURMOB request sent. Action: {Action}, SOAPAction: {SoapAction}, Length: {Length}", action, soapAction, xmlPayload.Length);
                _logger.LogInformation("TURMOB request XML: {XmlPayload}", xmlPayload);
            }

            var response = await client.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);

            if (options.Logging.LogResponse)
            {
                _logger.LogInformation(
                    "TURMOB response received. Status: {StatusCode}, Length: {Length}",
                    response.StatusCode,
                    responseContent.Length);
                _logger.LogInformation("TURMOB response XML: {ResponseXml}", responseContent);
            }

            if (!response.IsSuccessStatusCode)
            {
                var fault = TryGetFaultString(responseContent);
                var detail = string.IsNullOrWhiteSpace(fault)
                    ? $"TURMOB HTTP error: {(int)response.StatusCode}"
                    : $"TURMOB HTTP error: {(int)response.StatusCode}. {fault}";
                return TurmobSendResult.Failed(detail);
            }

            var sendResult = TryGetSendResult(responseContent);
            if (sendResult is { IsSuccess: false })
            {
                return TurmobSendResult.Failed(sendResult.ErrorMessage ?? "TURMOB send failed.");
            }

            return TurmobSendResult.Success();
        }
        catch (OperationCanceledException)
        {
            return TurmobSendResult.Failed($"TURMOB request timed out after {timeoutSeconds} seconds.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TURMOB SOAP request failed. Detail: {Detail}", GetExceptionDetail(ex));
            return TurmobSendResult.Failed($"TURMOB SOAP request failed. {GetExceptionDetail(ex)}");
        }
    }

    private static TurmobSoapSendResult? TryGetSendResult(string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return null;
        }

        try
        {
            var doc = XDocument.Parse(responseContent);
            var resultElement = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "Result");
            if (resultElement is null)
            {
                return null;
            }

            var isSuccess = string.Equals(resultElement.Value?.Trim(), "Success", StringComparison.OrdinalIgnoreCase);
            var errorElement = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "ErrorMessage");
            var errorMessage = string.IsNullOrWhiteSpace(errorElement?.Value) ? null : errorElement?.Value.Trim();
            return new TurmobSoapSendResult(isSuccess, errorMessage);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetFaultString(string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return null;
        }

        try
        {
            var doc = XDocument.Parse(responseContent);
            var fault = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "Fault");
            if (fault is null) return null;
            var faultString = fault.Descendants().FirstOrDefault(x => x.Name.LocalName == "faultstring");
            return faultString?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static string GetExceptionDetail(Exception ex)
    {
        var inner = ex.InnerException?.Message;
        return string.IsNullOrWhiteSpace(inner)
            ? ex.Message
            : $"{ex.Message} | Inner: {inner}";
    }

    private sealed record TurmobSoapSendResult(bool IsSuccess, string? ErrorMessage);
}
