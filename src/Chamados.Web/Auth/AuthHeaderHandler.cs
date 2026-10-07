using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;

namespace Chamados.Web.Auth;

/// <summary>Anexa o token às chamadas da API e manda para o login quando a sessão expira (401).</summary>
public sealed class AuthHeaderHandler(TokenStore store, JwtAuthStateProvider auth, NavigationManager nav) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await store.ObterAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, ct);

        var ehLogin = request.RequestUri?.AbsolutePath.EndsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase) == true;
        if (response.StatusCode == HttpStatusCode.Unauthorized && !ehLogin)
        {
            await auth.SairAsync();
            nav.NavigateTo($"login?returnUrl={Uri.EscapeDataString(nav.ToBaseRelativePath(nav.Uri))}");
        }

        return response;
    }
}
