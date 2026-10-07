using Microsoft.JSInterop;

namespace Chamados.Web.Auth;

/// <summary>Guarda o token JWT no localStorage do navegador.</summary>
public sealed class TokenStore(IJSRuntime js)
{
    private const string Chave = "chamados.token";
    private string? cache;
    private bool carregado;

    public async ValueTask<string?> ObterAsync()
    {
        if (!carregado)
        {
            cache = await js.InvokeAsync<string?>("localStorage.getItem", Chave);
            carregado = true;
        }
        return cache;
    }

    public async ValueTask SalvarAsync(string token)
    {
        cache = token;
        carregado = true;
        await js.InvokeVoidAsync("localStorage.setItem", Chave, token);
    }

    public async ValueTask LimparAsync()
    {
        cache = null;
        carregado = true;
        await js.InvokeVoidAsync("localStorage.removeItem", Chave);
    }
}
