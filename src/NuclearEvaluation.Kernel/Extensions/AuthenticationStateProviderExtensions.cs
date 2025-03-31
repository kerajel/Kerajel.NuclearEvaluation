using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace NuclearEvaluation.Kernel.Extensions;

public static class AuthenticationStateProviderExtensions
{
    public static async Task<string> GetCurrentUserId(this AuthenticationStateProvider provider)
    {
        AuthenticationState authState = await provider.GetAuthenticationStateAsync();
        ClaimsPrincipal user = authState.User;
        if (user.Identity?.IsAuthenticated ?? false)
        {
            return user.FindFirst(c => c.Type.Equals(ClaimTypes.NameIdentifier))?.Value ?? string.Empty;
        }
        return string.Empty;
    }
}
