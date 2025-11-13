using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Mime;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using KuyumculukTakipProgrami.Api;
using KuyumculukTakipProgrami.Api.Services;
using KuyumculukTakipProgrami.Domain.Entities;
using KuyumculukTakipProgrami.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace KuyumculukTakipProgrami.Api.Endpoints;

public static class PrintEndpoints
{
    public static IEndpointRouteBuilder MapPrintEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/print")
            .WithTags("Etiket Basma");

        group.MapPost("/multi", async (PrintMultiRequest request, IPrintQueueService queueService, HttpContext http, KtpDbContext db, CancellationToken cancellationToken) =>
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

    private static async Task<bool> HasEtiketPermissionAsync(HttpContext http, KtpDbContext db)
    {
        if (http.User.IsInRole(Role.Yonetici.ToString())) return true;
        var sub = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var uid)) return false;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
        if (user?.AssignedRoleId is Guid rid)
        {
            var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rid);
            if (role?.CanPrintLabels == true) return true;
        }
        return false;
    }

    public sealed record PrintMultiRequest([property: Required] List<string> Values);
}
