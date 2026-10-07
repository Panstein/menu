using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Chamados.Web.Auth;

/// <summary>Deriva o usuário logado a partir do JWT salvo. A validação real do token é feita pela API.</summary>
public sealed class JwtAuthStateProvider(TokenStore store) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonimo = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await store.ObterAsync();
        if (string.IsNullOrEmpty(token)) return Anonimo;

        var claims = LerClaims(token);
        if (claims is null || Expirado(claims))
        {
            await store.LimparAsync();
            return Anonimo;
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", "name", "role")));
    }

    public async Task EntrarAsync(string token)
    {
        await store.SalvarAsync(token);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SairAsync()
    {
        await store.LimparAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonimo));
    }

    private static bool Expirado(IEnumerable<Claim> claims) =>
        claims.FirstOrDefault(c => c.Type == "exp") is not { } exp
        || !long.TryParse(exp.Value, out var segundos)
        || DateTimeOffset.FromUnixTimeSeconds(segundos) <= DateTimeOffset.UtcNow;

    private static List<Claim>? LerClaims(string jwt)
    {
        try
        {
            var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));

            var claims = new List<Claim>();
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Array)
                    claims.AddRange(p.Value.EnumerateArray().Select(v => new Claim(p.Name, v.ToString())));
                else
                    claims.Add(new Claim(p.Name, p.Value.ToString()));
            }
            return claims;
        }
        catch
        {
            return null;
        }
    }
}
