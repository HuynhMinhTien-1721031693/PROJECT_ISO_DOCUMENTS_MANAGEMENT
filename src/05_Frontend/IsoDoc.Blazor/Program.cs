using IsoDoc.Blazor.Components;
using IsoDoc.Blazor.Services.Api;
using IsoDoc.Blazor.Services.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<ProtectedLocalStorage>();
builder.Services.AddScoped<TokenStorageService>();
builder.Services.AddScoped<JwtParser>();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddHttpClient("IsoDocApi", client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5075/api/v1/";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("IsoDocApi"));
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<DocumentApiService>();
builder.Services.AddScoped<WorkflowApiService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
