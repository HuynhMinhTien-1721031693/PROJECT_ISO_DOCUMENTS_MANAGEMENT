using IsoDoc.MauiApp.Services;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace IsoDoc.MauiApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddMudServices();
		builder.Services.AddSingleton(_ => AppSettingsLoader.LoadApiOptions());
		builder.Services.AddSingleton<SecureStorageService>();
		builder.Services.AddHttpClient<IApiService, ApiService>((serviceProvider, client) =>
		{
			var options = serviceProvider.GetRequiredService<ApiOptions>();
			client.BaseAddress = new Uri(options.BaseUrl);
		});

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
