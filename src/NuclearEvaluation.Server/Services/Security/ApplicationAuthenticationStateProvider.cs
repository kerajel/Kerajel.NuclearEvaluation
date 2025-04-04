using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using NuclearEvaluation.Kernel.Models.Identity;

namespace NuclearEvaluation.Server.Services.Security;

public class ApplicationAuthenticationStateProvider : AuthenticationStateProvider
{
    readonly SecurityService securityService;
    ApplicationAuthenticationState authenticationState;

    public ApplicationAuthenticationStateProvider(SecurityService securityService)
    {
        this.securityService = securityService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = new ClaimsIdentity();

        try
        {
            var state = await GetApplicationAuthenticationStateAsync();

            if (state.IsAuthenticated)
            {
                identity = new ClaimsIdentity(state.Claims.Select(c => new Claim(c.Type, c.Value)), "NuclearEvaluation.Server");
            }
        }
        catch (HttpRequestException ex)
        {
        }

        var result = new AuthenticationState(new ClaimsPrincipal(identity));

        await securityService.InitializeAsync(result);

        return result;
    }

    async Task<ApplicationAuthenticationState> GetApplicationAuthenticationStateAsync()
    {
        if (authenticationState == null)
        {
            authenticationState = await securityService.GetAuthenticationStateAsync();
        }

        return authenticationState;
    }
}