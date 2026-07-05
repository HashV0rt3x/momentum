using Momentum.Application.Users;

namespace Momentum.Application.Auth;

/// <summary>POST /auth/telegram response (spec section 1).</summary>
public sealed record AuthResponse(
    UserDto User,
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);

/// <summary>POST /auth/refresh response.</summary>
public sealed record TokenPairResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);
