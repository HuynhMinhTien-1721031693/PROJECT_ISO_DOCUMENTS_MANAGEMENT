using System.Text.Json;

namespace IsoDoc.MauiApp.Services;

public static class AppSettingsLoader
{
    public static ApiOptions LoadApiOptions()
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            using var document = JsonDocument.Parse(json);
            var baseUrl = document.RootElement
                .GetProperty("Api")
                .GetProperty("BaseUrl")
                .GetString();

            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                return new ApiOptions { BaseUrl = baseUrl };
            }
        }
        catch
        {
            // Fall back to default endpoint.
        }

        return new ApiOptions();
    }
}
