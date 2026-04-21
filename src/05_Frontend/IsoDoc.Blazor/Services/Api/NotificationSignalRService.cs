using IsoDoc.Blazor.Services.Auth;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace IsoDoc.Blazor.Services.Api;

/// <summary>Listens for server-pushed notifications (Blazor Server → API SignalR hub).</summary>
public sealed class NotificationSignalRService : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly TokenStorageService _tokenStorage;
    private HubConnection? _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public NotificationSignalRService(IConfiguration configuration, TokenStorageService tokenStorage)
    {
        _configuration = configuration;
        _tokenStorage = tokenStorage;
    }

    public event Func<Task>? NotificationReceived;

    public async Task EnsureConnectedAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_connection?.State == HubConnectionState.Connected)
                return;

            var tokens = await _tokenStorage.GetTokensAsync();
            if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken))
                return;

            var apiBase = _configuration["Api:BaseUrl"] ?? "http://localhost:5075/api/v1/";
            var hubUri = BuildHubUri(apiBase, tokens.AccessToken);

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUri)
                .WithAutomaticReconnect()
                .Build();

            // Must match server hub event names (see NotificationHub in API / Infrastructure).
            _connection.On<object>("ReceiveNotification", OnPushPayload);
            _connection.On<object>("DocumentApproved", OnPushPayload);

            await _connection.StartAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Uri BuildHubUri(string apiBaseUrl, string accessToken)
    {
        var baseUri = new Uri(apiBaseUrl, UriKind.Absolute);
        var root = baseUri.GetLeftPart(UriPartial.Authority);
        return new Uri($"{root}/hubs/notifications?access_token={Uri.EscapeDataString(accessToken)}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
        _gate.Dispose();
    }

    private async Task OnPushPayload(object _)
    {
        if (NotificationReceived is not null)
            await NotificationReceived.Invoke();
    }
}
