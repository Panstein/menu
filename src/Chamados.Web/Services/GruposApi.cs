using System.Net;
using System.Net.Http.Json;
using Chamados.Shared.Grupos;

namespace Chamados.Web.Services;

public sealed class GruposApi(HttpClient http)
{
    private const string Base = "api/grupos";

    /// <summary>Com <paramref name="projeto"/>, só os grupos daquele projeto.</summary>
    public async Task<List<GrupoDto>> ListarAsync(long? projeto = null) =>
        await http.GetFromJsonAsync<List<GrupoDto>>(projeto is null ? Base : $"{Base}?projeto={projeto}") ?? [];

    public async Task<GrupoDto?> ObterAsync(long id)
    {
        var resp = await http.GetAsync($"{Base}/{id}");
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<GrupoDto>();
    }

    /// <summary>Retorna o CH_ID_GRUPO salvo e os erros de validação por campo (vazio = sucesso).</summary>
    public async Task<(long? Id, IDictionary<string, string[]> Erros)> SalvarAsync(GrupoDto dto)
    {
        var resp = dto.Id is null
            ? await http.PostAsJsonAsync(Base, dto)
            : await http.PutAsJsonAsync($"{Base}/{dto.Id}", dto);

        var erros = await ErrosAsync(resp);
        if (erros.Count > 0) return (null, erros);

        if (dto.Id is null)
            return ((await resp.Content.ReadFromJsonAsync<GrupoDto>())?.Id, erros);
        return (dto.Id, erros);
    }

    public async Task<IDictionary<string, string[]>> ExcluirAsync(long id) =>
        await ErrosAsync(await http.DeleteAsync($"{Base}/{id}"));

    /// <summary>ID_USER dos usuários do grupo (CH_USU_GRP).</summary>
    public async Task<List<long>> UsuariosAsync(long id) =>
        await http.GetFromJsonAsync<List<long>>($"{Base}/{id}/usuarios") ?? [];

    /// <summary>Substitui os usuários do grupo pelos informados.</summary>
    public async Task<IDictionary<string, string[]>> DefinirUsuariosAsync(long id, IEnumerable<long> usuarios) =>
        await ErrosAsync(await http.PutAsJsonAsync($"{Base}/{id}/usuarios", usuarios));

    /// <summary>MN_ID dos menus que o grupo acessa (CH_GRP_MNU), do projeto do grupo.</summary>
    public async Task<List<long>> MenusAsync(long id) =>
        await http.GetFromJsonAsync<List<long>>($"{Base}/{id}/menus") ?? [];

    /// <summary>Define os menus que o grupo acessa (a API grava só a diferença).</summary>
    public async Task<IDictionary<string, string[]>> DefinirMenusAsync(long id, IEnumerable<long> menus) =>
        await ErrosAsync(await http.PutAsJsonAsync($"{Base}/{id}/menus", menus));

    private static async Task<IDictionary<string, string[]>> ErrosAsync(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.BadRequest)
        {
            var problema = await resp.Content.ReadFromJsonAsync<ProblemaValidacao>();
            return problema?.Errors ?? new Dictionary<string, string[]> { [""] = ["Dados inválidos."] };
        }
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return new Dictionary<string, string[]> { [""] = ["Grupo não encontrado."] };

        resp.EnsureSuccessStatusCode();
        return new Dictionary<string, string[]>();
    }
}
