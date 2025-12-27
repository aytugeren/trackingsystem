using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using KuyumculukTakipProgrami.Domain.Entities.Market;

namespace KuyumculukTakipProgrami.Infrastructure.Pricing;

public sealed record GoldFeedParsedHeader(
    decimal UsdAlis,
    decimal UsdSatis,
    decimal EurAlis,
    decimal EurSatis,
    decimal EurUsd,
    decimal Ons,
    decimal Has,
    decimal GumusHas);

public sealed record GoldFeedParsedResult(
    GoldFeedParsedHeader Header,
    IReadOnlyList<decimal?> IndexedValues);

public static class GoldFeedNewVersionParser
{
    public static bool TryParse(string payload, out GoldFeedParsedResult? result, out string? error)
    {
        result = null;
        error = null;

        if (payload is null)
        {
            error = "Payload is null.";
            return false;
        }
        var headerValues = ParseHeaderRates(payload);
        if (!TryBuildHeader(headerValues, out var header, out error))
        {
            return false;
        }

        var indexedValues = ParseIndexedValues(payload);
        if (indexedValues.Count == 0)
        {
            error = "Indexed values could not be parsed.";
            return false;
        }

        result = new GoldFeedParsedResult(header, indexedValues);
        return true;
    }

    private static bool TryBuildHeader(
        IReadOnlyDictionary<string, List<decimal>> headerValues,
        out GoldFeedParsedHeader header,
        out string? error)
    {
        header = default;
        error = null;

        if (!TryGetSingle(headerValues, "USD", 2, out var usdAlis, out var usdSatis)
            || !TryGetSingle(headerValues, "EURO", 2, out var eurAlis, out var eurSatis)
            || !TryGetSingle(headerValues, "EURO/USD", 1, out var eurUsd)
            || !TryGetSingle(headerValues, "ONS", 1, out var ons)
            || !TryGetSingle(headerValues, "HAS", 1, out var has)
            || !TryGetSingle(headerValues, "GUMUSHAS", 1, out var gumusHas))
        {
            error = "Header values are incomplete.";
            return false;
        }

        header = new GoldFeedParsedHeader(
            usdAlis,
            usdSatis,
            eurAlis,
            eurSatis,
            eurUsd,
            ons,
            has,
            gumusHas);
        return true;
    }

    private static bool TryGetSingle(
        IReadOnlyDictionary<string, List<decimal>> headerValues,
        string key,
        int expectedCount,
        out decimal first,
        out decimal second)
    {
        first = 0;
        second = 0;
        if (!headerValues.TryGetValue(key, out var values) || values.Count < expectedCount)
        {
            return false;
        }

        first = values[0];
        second = values[1];
        return true;
    }

    private static bool TryGetSingle(
        IReadOnlyDictionary<string, List<decimal>> headerValues,
        string key,
        int expectedCount,
        out decimal value)
    {
        value = 0;
        if (!headerValues.TryGetValue(key, out var values) || values.Count < expectedCount)
        {
            return false;
        }

        value = values[0];
        return true;
    }

    private static Dictionary<string, List<decimal>> ParseHeaderRates(string input)
    {
        var normalized = input
            .Replace("GümüşHAS", "GUMUSHAS", StringComparison.OrdinalIgnoreCase)
            .Replace("G�m��HAS", "GUMUSHAS", StringComparison.OrdinalIgnoreCase)
            .Replace("GUMUS_HAS", "GUMUSHAS", StringComparison.OrdinalIgnoreCase)
            .Replace("İAB", "IAB", StringComparison.OrdinalIgnoreCase)
            .Replace("�AB", "IAB", StringComparison.OrdinalIgnoreCase);

        var tokens = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        var labelMap = new Dictionary<string, (string Key, int Count)>(StringComparer.OrdinalIgnoreCase)
        {
            { "USD", ("USD", 2) },
            { "EURO", ("EURO", 2) },
            { "EURO/USD", ("EURO/USD", 1) },
            { "ONS", ("ONS", 1) },
            { "HAS", ("HAS", 1) },
            { "GUMUSHAS", ("GUMUSHAS", 1) }
        };

        var result = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < tokens.Length; i++)
        {
            if (!labelMap.TryGetValue(tokens[i], out var meta))
            {
                continue;
            }

            var values = new List<decimal>();
            for (var j = 0; j < meta.Count && i + 1 + j < tokens.Length; j++)
            {
                if (decimal.TryParse(tokens[i + 1 + j], NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                {
                    values.Add(value);
                }
            }

            if (values.Count > 0)
            {
                result[meta.Key] = values;
            }
        }

        return result;
    }

    private static List<decimal?> ParseIndexedValues(string input)
    {
        var normalized = input
            .Replace("İAB", "IAB", StringComparison.OrdinalIgnoreCase)
            .Replace("�AB", "IAB", StringComparison.OrdinalIgnoreCase);

        var iabIndex = normalized.IndexOf("IAB", StringComparison.OrdinalIgnoreCase);
        if (iabIndex < 0)
        {
            return new List<decimal?>();
        }

        var afterIab = normalized[(iabIndex + 3)..];
        var tildeIndex = afterIab.IndexOf('~');
        if (tildeIndex < 0)
        {
            return new List<decimal?>();
        }

        decimal? iabValue = null;
        var iabToken = afterIab[..tildeIndex];
        if (decimal.TryParse(iabToken, NumberStyles.Number, CultureInfo.InvariantCulture, out var iabParsed))
        {
            iabValue = iabParsed;
        }
        else if (TryExtractDecimal(iabToken, out var extractedIab))
        {
            iabValue = extractedIab;
        }

        var afterTilde = afterIab[(tildeIndex + 1)..];
        var endIndex = afterTilde.IndexOf("~~~", StringComparison.OrdinalIgnoreCase);
        if (endIndex >= 0)
        {
            afterTilde = afterTilde[..endIndex];
        }

        var numbers = new List<decimal>();
        foreach (var token in afterTilde.Split('*', StringSplitOptions.RemoveEmptyEntries))
        {
            if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                numbers.Add(value);
                continue;
            }

            if (TryExtractDecimal(token, out var extracted))
            {
                numbers.Add(extracted);
            }
        }

        var expectedCount = GoldFeedNewVersionMapping.Indexes.Count;
        var indexed = new List<decimal?>(expectedCount);
        indexed.Add(iabValue);

        var remainingCount = expectedCount - 1;
        var takeCount = Math.Min(remainingCount, numbers.Count);
        for (var i = 0; i < takeCount; i++)
        {
            indexed.Add(numbers[i]);
        }

        if (indexed.Count < expectedCount)
        {
            indexed.AddRange(Enumerable.Repeat<decimal?>(null, expectedCount - indexed.Count));
        }

        return indexed;
    }

    private static bool TryExtractDecimal(string token, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var sb = new StringBuilder(token.Length);
        var started = false;
        foreach (var ch in token)
        {
            if ((ch >= '0' && ch <= '9') || ch == '.' || ch == ',')
            {
                sb.Append(ch == ',' ? '.' : ch);
                started = true;
                continue;
            }

            if (started)
            {
                break;
            }
        }

        if (!started)
        {
            return false;
        }

        return decimal.TryParse(sb.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
