using System.Text.Json;

namespace IsoDoc.Blazor.Services.Api;

public static class ApiProblemMessageFormatter
{
    private static readonly HashSet<string> GenericTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Error",
        "An error occurred.",
        "One or more errors occurred."
    };

    public static string Format(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return string.Empty;

        var trimmed = rawBody.Trim();
        if (GenericTitles.Contains(trimmed))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var parts = new List<string>();
                foreach (var prop in errors.EnumerateObject())
                {
                    foreach (var msg in prop.Value.EnumerateArray())
                    {
                        var s = msg.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            parts.Add(s);
                    }
                }

                if (parts.Count > 0)
                    return string.Join(" ", parts.Distinct());
            }

            if (root.TryGetProperty("detail", out var detail))
            {
                var d = detail.GetString();
                if (!string.IsNullOrWhiteSpace(d))
                    return d!;
            }

            if (root.TryGetProperty("title", out var title))
            {
                var t = title.GetString();
                if (!string.IsNullOrWhiteSpace(t) && !GenericTitles.Contains(t))
                    return t!;
            }

            if (root.TryGetProperty("message", out var message))
            {
                var m = message.GetString();
                if (!string.IsNullOrWhiteSpace(m) && !GenericTitles.Contains(m))
                    return m!;
            }

            if (root.TryGetProperty("error", out var error))
            {
                var e = error.GetString();
                if (!string.IsNullOrWhiteSpace(e) && !GenericTitles.Contains(e))
                    return e!;
            }
        }
        catch
        {
            // Fall through
        }

        return GenericTitles.Contains(trimmed) ? string.Empty : trimmed;
    }
}
