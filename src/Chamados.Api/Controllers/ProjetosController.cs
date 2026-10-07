using Chamados.Api.Data;
using Chamados.Shared.Projetos;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Chamados.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjetosController(ProjetoRepository repo, ILogger<ProjetosController> logger) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProjetoDto>> Listar(CancellationToken ct) => repo.ListarAsync(ct);

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ProjetoDto>> Obter(long id, CancellationToken ct) =>
        await repo.ObterAsync(id, ct) is { } p ? p : NotFound();

    [HttpPost]
    public async Task<ActionResult<ProjetoDto>> Inserir(ProjetoDto dto, CancellationToken ct)
    {
        dto.Normalizar();
        if (await ValidarAsync(null, dto, ct) is { } erro) return erro;

        // PJ_ID = MAX + 1: em inclusões simultâneas, tenta mais uma vez com o próximo código.
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                dto.Id = await repo.InserirAsync(dto, ct);
                break;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && tentativa < 3)
            {
            }
        }

        logger.LogInformation("Projeto {Id} ({Nome}) incluído por {Autor}", dto.Id, dto.Nome, User.FindFirst("email")?.Value);
        return CreatedAtAction(nameof(Obter), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(long id, ProjetoDto dto, CancellationToken ct)
    {
        dto.Normalizar();
        if (await ValidarAsync(id, dto, ct) is { } erro) return erro;

        if (!await repo.AtualizarAsync(id, dto, ct)) return NotFound();
        logger.LogInformation("Projeto {Id} alterado por {Autor}", id, User.FindFirst("email")?.Value);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Excluir(long id, CancellationToken ct)
    {
        var menus = await repo.MenusVinculadosAsync(id, ct);
        if (menus.Count > 0)
            return Erro("", $"Não é possível excluir: os menus {string.Join(", ", menus)} estão vinculados a este projeto.");
        var grupos = await repo.GruposVinculadosAsync(id, ct);
        if (grupos.Count > 0)
            return Erro("", $"Não é possível excluir: os grupos {string.Join(", ", grupos)} pertencem a este projeto.");

        try
        {
            if (!await repo.ExcluirAsync(id, ct)) return NotFound();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation) // menu vinculado entre a checagem e a exclusão
        {
            return Erro("", "Não é possível excluir: existem menus vinculados a este projeto.");
        }

        logger.LogInformation("Projeto {Id} excluído por {Autor}", id, User.FindFirst("email")?.Value);
        return NoContent();
    }

    private async Task<ActionResult?> ValidarAsync(long? id, ProjetoDto dto, CancellationToken ct) =>
        await repo.NomeEmUsoAsync(dto.Nome, id, ct) ? Erro(nameof(dto.Nome), "Já existe um projeto com este nome.") : null;

    private ActionResult Erro(string campo, string mensagem) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensagem] }));
}
