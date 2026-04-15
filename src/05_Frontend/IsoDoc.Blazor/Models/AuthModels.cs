namespace IsoDoc.Blazor.Models;

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
}

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class TokenBundle
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
}
