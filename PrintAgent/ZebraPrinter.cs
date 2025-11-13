using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RawPrint;

namespace PrintAgent;

public sealed class ZebraPrinter
{
    private readonly string _printerName;
    private readonly ILogger<ZebraPrinter> _logger;

    public ZebraPrinter(IConfiguration configuration, ILogger<ZebraPrinter> logger)
    {
        _printerName = configuration["Printer:Name"] ?? throw new InvalidOperationException("Printer:Name configuration is required.");
        _logger = logger;
    }

    public void PrintZpl(string zpl)
    {
        if (string.IsNullOrWhiteSpace(zpl))
        {
            throw new ArgumentException("ZPL payload must not be empty.", nameof(zpl));
        }

        try
        {
            _logger.LogInformation("Sending payload to printer {PrinterName}.", _printerName);
            var payload = Encoding.ASCII.GetBytes(zpl);
            using var stream = new MemoryStream(payload);
            var printer = new Printer();
            printer.PrintRawStream(_printerName, stream, "PrintAgent", paused: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to print via {PrinterName}.", _printerName);
            throw;
        }
    }
}
