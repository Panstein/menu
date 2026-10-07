using System.Net;
using System.Net.Http.Json;
using Chamados.Shared.Usuarios;

namespace Chamados.Web.Services;

public sealed class UsuariosApi(HttpClient http)
{
    private const string Base = "api/usuarios";

    private static string Url(string email) => $"{Base}/{Uri.EscapeDataString(email)}";

    public async Task<List<UsuarioDto>> ListarAsync() =>
        await http.GetFromJsonAsync<List<UsuarioDto>>(Base) ?? [];

    public async Task<UsuarioDto?> ObterAsync(string email)
    {
        var resp = await http.GetAsync(Url(email));
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<UsuarioDto>();
    }

    public Task<bool> ExisteAsync(string email) =>
        http.GetFromJsonAsync<bool>($"{Base}/existe?email={Uri.EscapeDataString(email)}");

    /// <summary>Retorna os erros de validação por campo (vazio = sucesso).</summary>
    public async Task<IDictionary<string, string[]>> SalvarAsync(UsuarioDto dto, string? emailOriginal)
    {
        var resp = emailOriginal is null
            ? await http.PostAsJsonAsync(Base, dto)
            : await http.PutAsJsonAsync(Url(emailOriginal), dto);

        return await ErrosAsync(resp);
    }

    public async Task<IDictionary<string, string[]>> ExcluirAsync(string email) =>
        await ErrosAsync(await http.DeleteAsync(Url(email)));

    private static async Task<IDictionary<string, string[]>> ErrosAsync(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.BadRequest)
        {
            var problema = await resp.Content.ReadFromJsonAsync<ProblemaValidacao>();
            return problema?.Errors ?? new Dictionary<string, string[]> { [""] = ["Dados inválidos."] };
        }
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return new Dictionary<string, string[]> { [""] = ["Usuário não encontrado."] };

        resp.EnsureSuccessStatusCode();
        return new Dictionary<string, string[]>();
    }
}

/// <summary>Formato ValidationProblemDetails devolvido pela API.</summary>
public sealed record ProblemaValidacao(Dictionary<string, string[]>? Errors);
