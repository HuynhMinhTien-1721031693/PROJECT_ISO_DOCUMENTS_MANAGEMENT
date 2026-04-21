using System.Text.Json;

namespace IsoDoc.Blazor.Services.Api;

public static class ApiProblemMessageFormatter
{
    public static string Format(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
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
                if (!string.IsNullOrWhiteSpace(t))
                    return t!;
            }
        }
        catch
        {
            // Fall through
        }

        return rawBody.Trim();
    }
}
