using System.Net;
using System.Net.Http.Json;
using Chamados.Shared.Menus;

namespace Chamados.Web.Services;

public sealed class MenusApi(HttpClient http)
{
    private const string Base = "api/menus";

    /// <summary>Com <paramref name="projeto"/>, só os itens daquele MN_PROJETO.</summary>
    public async Task<List<MenuDto>> ListarAsync(long? projeto = null) =>
        await http.GetFromJsonAsync<List<MenuDto>>(projeto is null ? Base : $"{Base}?projeto={projeto}") ?? [];

    public async Task<MenuDto?> ObterAsync(long id)
    {
        var resp = await http.GetAsync($"{Base}/{id}");
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<MenuDto>();
    }

    /// <summary>Retorna o MN_ID salvo e os erros de validação por campo (vazio = sucesso).</summary>
    public async Task<(long? Id, IDictionary<string, string[]> Erros)> SalvarAsync(MenuDto dto)
    {
        var resp = dto.Id is null
            ? await http.PostAsJsonAsync(Base, dto)
            : await http.PutAsJsonAsync($"{Base}/{dto.Id}", dto);

        var erros = await ErrosAsync(resp);
        if (erros.Count > 0) return (null, erros);

        if (dto.Id is null)
            return ((await resp.Content.ReadFromJsonAsync<MenuDto>())?.Id, erros);
        return (dto.Id, erros);
    }

    public async Task<IDictionary<string, string[]>> ExcluirAsync(long id) =>
        await ErrosAsync(await http.DeleteAsync($"{Base}/{id}"));

    private static async Task<IDictionary<string, string[]>> ErrosAsync(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.BadRequest)
        {
            var problema = await resp.Content.ReadFromJsonAsync<ProblemaValidacao>();
            return problema?.Errors ?? new Dictionary<string, string[]> { [""] = ["Dados inválidos."] };
        }
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return new Dictionary<string, string[]> { [""] = ["Menu não encontrado."] };

        resp.EnsureSuccessStatusCode();
        return new Dictionary<string, string[]>();
    }
}
