using System;
using System.Threading.Tasks;
using KuyumculukTakipProgrami.Application.DTOs;
using KuyumculukTakipProgrami.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KuyumculukTakipProgrami.Infrastructure.Integration.Turmob;

public sealed class TurmobInvoiceGateway : ITurmobInvoiceGateway
{
    private readonly IOptionsMonitor<TurmobOptions> _options;
    private readonly TurmobSoapClient _soapClient;
    private readonly TurmobInvoiceMapper _mapper;
    private readonly ILogger<TurmobInvoiceGateway> _logger;

    public TurmobInvoiceGateway(
        IOptionsMonitor<TurmobOptions> options,
        TurmobSoapClient soapClient,
        TurmobInvoiceMapper mapper,
        ILogger<TurmobInvoiceGateway> logger)
    {
        _options = options;
        _soapClient = soapClient;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<TurmobSendResult> SendAsync(TurmobInvoiceDto invoice)
    {
        var options = _options.CurrentValue;

        if (!options.Enabled)
        {
            _logger.LogInformation("TURMOB integration disabled. Skipping send.");
            return TurmobSendResult.Skipped("TURMOB integration disabled.");
        }

        var environment = options.GetSelectedEnvironment();
        if (environment == null || string.IsNullOrWhiteSpace(environment.ServiceUrl))
        {
            _logger.LogError("TURMOB configuration invalid. Environment: {Environment}", options.Environment);
            return TurmobSendResult.Failed("Invalid TURMOB environment configuration.");
        }

        try
        {
            var healthy = await _soapClient.HealthCheckAsync(environment, options, default).ConfigureAwait(false);
            if (!healthy)
            {
                _logger.LogWarning("TURMOB health check failed. Skipping send.");
                return TurmobSendResult.Skipped("TURMOB integration disabled.");
            }

            var xmlPayload = invoice.IsArchive
                ? _mapper.MapToArchiveInvoiceXml(invoice, environment)
                : _mapper.MapToInvoiceXml(invoice, environment);
            return await _soapClient.SendAsync(xmlPayload, invoice.IsArchive, environment, options, default).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TURMOB send failed.");
            return TurmobSendResult.Failed("TURMOB send failed.");
        }
    }
}
