using System.Net;
using System.Net.Http.Json;
using Chamados.Shared.Projetos;

namespace Chamados.Web.Services;

public sealed class ProjetosApi(HttpClient http)
{
    private const string Base = "api/projetos";

    public async Task<List<ProjetoDto>> ListarAsync() =>
        await http.GetFromJsonAsync<List<ProjetoDto>>(Base) ?? [];

    public async Task<ProjetoDto?> ObterAsync(long id)
    {
        var resp = await http.GetAsync($"{Base}/{id}");
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ProjetoDto>();
    }

    /// <summary>Retorna o PJ_ID salvo e os erros de validação por campo (vazio = sucesso).</summary>
    public async Task<(long? Id, IDictionary<string, string[]> Erros)> SalvarAsync(ProjetoDto dto)
    {
        var resp = dto.Id is null
            ? await http.PostAsJsonAsync(Base, dto)
            : await http.PutAsJsonAsync($"{Base}/{dto.Id}", dto);

        var erros = await ErrosAsync(resp);
        if (erros.Count > 0) return (null, erros);

        if (dto.Id is null)
            return ((await resp.Content.ReadFromJsonAsync<ProjetoDto>())?.Id, erros);
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
            return new Dictionary<string, string[]> { [""] = ["Projeto não encontrado."] };

        resp.EnsureSuccessStatusCode();
        return new Dictionary<string, string[]>();
    }
}
