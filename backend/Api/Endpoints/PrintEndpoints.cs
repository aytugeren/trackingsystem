using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using KuyumculukTakipProgrami.Api;
using KuyumculukTakipProgrami.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace KuyumculukTakipProgrami.Api.Endpoints;

public static class PrintEndpoints
{
    public static IEndpointRouteBuilder MapPrintEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/print")
            .WithTags("Etiket Basma");

        group.MapPost("/multi", async (PrintMultiRequest request, IPrintQueueService queueService, CancellationToken cancellationToken) =>
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

        return app;
    }

    public sealed record PrintMultiRequest([property: Required] List<string> Values);
}
