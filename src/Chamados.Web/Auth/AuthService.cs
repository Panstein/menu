using System.Net.Http.Json;
using Chamados.Shared.Auth;
using Chamados.Web.Layout.Menu;

namespace Chamados.Web.Auth;

public sealed class AuthService(HttpClient http, JwtAuthStateProvider auth, MenuLateralService menu)
{
    /// <summary>Retorna null em caso de sucesso, ou a mensagem de erro para exibir.</summary>
    public async Task<string?> EntrarAsync(LoginRequest req)
    {
        HttpResponseMessage resp;
        try
        {
            resp = await http.PostAsJsonAsync("api/auth/login", req);
        }
        catch (HttpRequestException)
        {
            return "Não foi possível conectar ao servidor. Verifique sua conexão.";
        }

        if (resp.IsSuccessStatusCode)
        {
            var dados = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (dados is null) return "Resposta inválida do servidor.";
            await auth.EntrarAsync(dados.Token);
            menu.Limpar(); // o menu lateral é lido da CH_MENU após a validação do usuário
            return null;
        }

        try
        {
            var erro = await resp.Content.ReadFromJsonAsync<LoginErro>();
            if (!string.IsNullOrEmpty(erro?.Mensagem)) return erro.Mensagem;
        }
        catch
        {
            // corpo não é LoginErro (ex.: validação 400)
        }

        return "Não foi possível entrar. Verifique o e-mail e a senha.";
    }

    public async Task SairAsync()
    {
        await auth.SairAsync();
        menu.Limpar();
    }
}
