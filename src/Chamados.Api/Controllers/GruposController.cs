using Chamados.Api.Data;
using Chamados.Shared.Grupos;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Chamados.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GruposController(GrupoRepository repo, ProjetoRepository projetos, ILogger<GruposController> logger) : ControllerBase
{
    /// <summary>Com ?projeto=N, só os grupos daquele projeto (CH_PROJETO_GRUPO).</summary>
    [HttpGet]
    public Task<IReadOnlyList<GrupoDto>> Listar([FromQuery] long? projeto, CancellationToken ct) => repo.ListarAsync(projeto, ct);

    [HttpGet("{id:long}")]
    public async Task<ActionResult<GrupoDto>> Obter(long id, CancellationToken ct) =>
        await repo.ObterAsync(id, ct) is { } g ? g : NotFound();

    [HttpPost]
    public async Task<ActionResult<GrupoDto>> Inserir(GrupoDto dto, CancellationToken ct)
    {
        dto.Normalizar();
        if (await ValidarAsync(null, dto, ct) is { } erro) return erro;

        try
        {
            dto.Id = await repo.InserirAsync(dto, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation) // projeto excluído após a validação
        {
            return ProjetoInexistente(dto);
        }

        logger.LogInformation("Grupo {Id} ({Nome}) incluído por {Autor}", dto.Id, dto.Nome, UsuarioLogado);
        return CreatedAtAction(nameof(Obter), new { id = dto.Id }, await repo.ObterAsync(dto.Id.Value, ct));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(long id, GrupoDto dto, CancellationToken ct)
    {
        dto.Normalizar();
        if (await ValidarAsync(id, dto, ct) is { } erro) return erro;

        try
        {
            if (!await repo.AtualizarAsync(id, dto, ct)) return NotFound();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation) // projeto excluído após a validação
        {
            return ProjetoInexistente(dto);
        }

        logger.LogInformation("Grupo {Id} alterado por {Autor}", id, UsuarioLogado);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Excluir(long id, CancellationToken ct)
    {
        var usuarios = await repo.NomesDosUsuariosAsync(id, ct);
        if (usuarios.Count > 0)
            return Erro("", $"Não é possível excluir: os usuários {string.Join(", ", usuarios)} estão neste grupo.");
        var menus = await repo.NomesDosMenusAsync(id, ct);
        if (menus.Count > 0)
            return Erro("", $"Não é possível excluir: o grupo tem acesso aos menus {string.Join(", ", menus)}. Remova-os em Grupos x Menus.");

        if (!await repo.ExcluirAsync(id, ct)) return NotFound();
        logger.LogInformation("Grupo {Id} excluído por {Autor}", id, UsuarioLogado);
        return NoContent();
    }

    /// <summary>ID_USER dos usuários do grupo (CH_USU_GRP).</summary>
    [HttpGet("{id:long}/usuarios")]
    public async Task<ActionResult<IReadOnlyList<long>>> Usuarios(long id, CancellationToken ct)
    {
        if (await repo.ObterAsync(id, ct) is null) return NotFound();
        return Ok(await repo.UsuariosAsync(id, ct));
    }

    /// <summary>Substitui os usuários do grupo pela lista informada (ID_USER).</summary>
    [HttpPut("{id:long}/usuarios")]
    public async Task<IActionResult> DefinirUsuarios(long id, List<long> usuarios, CancellationToken ct)
    {
        if (await repo.ObterAsync(id, ct) is null) return NotFound();

        var ids = usuarios.Distinct().ToList();
        var inexistentes = await repo.UsuariosInexistentesAsync(ids, ct);
        if (inexistentes.Count > 0)
            return Erro("", $"Usuários não encontrados: {string.Join(", ", inexistentes)}.");

        await repo.DefinirUsuariosAsync(id, ids, ct);
        logger.LogInformation("Usuários do grupo {Id} definidos por {Autor}: {Usuarios}", id, UsuarioLogado, ids);
        return NoContent();
    }

    /// <summary>MN_ID dos menus que o grupo acessa (CH_GRP_MNU), do projeto do próprio grupo.</summary>
    [HttpGet("{id:long}/menus")]
    public async Task<ActionResult<IReadOnlyList<long>>> Menus(long id, CancellationToken ct)
    {
        if (await repo.ObterAsync(id, ct) is not { Projeto: { } projeto }) return NotFound();
        return Ok(await repo.MenusAsync(id, projeto, ct));
    }

    /// <summary>
    /// Define os menus que o grupo acessa: grava só a diferença (inclui os novos, remove os desmarcados).
    /// Só aceita menus do projeto do grupo.
    /// </summary>
    [HttpPut("{id:long}/menus")]
    public async Task<IActionResult> DefinirMenus(long id, List<long> menus, CancellationToken ct)
    {
        if (await repo.ObterAsync(id, ct) is not { Projeto: { } projeto }) return NotFound();

        var ids = menus.Distinct().ToList();
        var invalidos = await repo.MenusForaDoProjetoAsync(ids, projeto, ct);
        if (invalidos.Count > 0)
            return Erro("", $"Menus inexistentes ou de outro projeto: {string.Join(", ", invalidos)}.");

        var (incluidos, removidos) = await repo.DefinirMenusAsync(id, projeto, ids, ct);
        logger.LogInformation("Menus do grupo {Id} (projeto {Projeto}) alterados por {Autor}: +{Incluidos} -{Removidos}",
            id, projeto, UsuarioLogado, incluidos, removidos);
        return NoContent();
    }

    private string? UsuarioLogado => User.FindFirst("email")?.Value;

    private async Task<ActionResult?> ValidarAsync(long? id, GrupoDto dto, CancellationToken ct)
    {
        // CH_PROJETO_GRUPO: FK ch_grupos_ch_projetos_fk para CH_PROJETOS.PJ_ID
        if (dto.Projeto is not { } projeto || !await projetos.ExisteAsync(projeto, ct))
            return ProjetoInexistente(dto);
        if (await repo.NomeEmUsoAsync(dto.Nome, dto.Projeto, id, ct))
            return Erro(nameof(dto.Nome), "Já existe um grupo com este nome neste projeto.");

        // Trocar o projeto de um grupo com acessos deixaria menus de outro projeto no grupo
        if (id is { } existente && await repo.MenusDeOutroProjetoAsync(existente, dto.Projeto, ct) > 0)
            return Erro(nameof(dto.Projeto), "O grupo tem acesso a menus de outro projeto: remova-os em Grupos x Menus antes de trocar o projeto.");
        return null;
    }

    private ActionResult ProjetoInexistente(GrupoDto dto) =>
        Erro(nameof(dto.Projeto), dto.Projeto is null ? "Informe o projeto." : $"Não existe projeto com o código {dto.Projeto}.");

    private ActionResult Erro(string campo, string mensagem) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensagem] }));
}
