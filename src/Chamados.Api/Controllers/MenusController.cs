using Chamados.Api.Data;
using Chamados.Shared.Menus;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Chamados.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenusController(MenuRepository repo, ProjetoRepository projetos, ILogger<MenusController> logger) : ControllerBase
{
    /// <summary>Com ?projeto=N, só os itens daquele MN_PROJETO (usado pelo menu lateral de cada sistema).</summary>
    [HttpGet]
    public Task<IReadOnlyList<MenuDto>> Listar([FromQuery] long? projeto, CancellationToken ct) => repo.ListarAsync(projeto, ct);

    [HttpGet("{id:long}")]
    public async Task<ActionResult<MenuDto>> Obter(long id, CancellationToken ct) =>
        await repo.ObterAsync(id, ct) is { } m ? m : NotFound();

    [HttpPost]
    public async Task<ActionResult<MenuDto>> Inserir(MenuDto dto, CancellationToken ct)
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
        logger.LogInformation("Menu {Id} ({Nome}) incluído por {Autor}", dto.Id, dto.Nome, User.FindFirst("email")?.Value);
        return CreatedAtAction(nameof(Obter), new { id = dto.Id }, await repo.ObterAsync(dto.Id.Value, ct));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(long id, MenuDto dto, CancellationToken ct)
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
        logger.LogInformation("Menu {Id} alterado por {Autor}", id, User.FindFirst("email")?.Value);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Excluir(long id, CancellationToken ct)
    {
        var dependentes = await repo.DependentesAsync(id, ct);
        if (dependentes.Count > 0)
            return Erro("", $"Não é possível excluir: os menus {string.Join(", ", dependentes)} dependem deste item.");
        var grupos = await repo.GruposComAcessoAsync(id, ct);
        if (grupos.Count > 0)
            return Erro("", $"Não é possível excluir: os grupos {string.Join(", ", grupos)} têm acesso a este menu. Remova-o em Grupos x Menus.");

        if (!await repo.ExcluirAsync(id, ct)) return NotFound();
        logger.LogInformation("Menu {Id} excluído por {Autor}", id, User.FindFirst("email")?.Value);
        return NoContent();
    }

    private async Task<ActionResult?> ValidarAsync(long? id, MenuDto dto, CancellationToken ct)
    {
        // MN_PROJETO: FK ch_menu_ch_projetos_fk para CH_PROJETOS.PJ_ID
        if (dto.Projeto is { } projeto && !await projetos.ExisteAsync(projeto, ct))
            return ProjetoInexistente(dto);

        if (await repo.NomeEmUsoAsync(dto.Nome, dto.Projeto, id, ct))
            return Erro(nameof(dto.Nome), "Já existe um menu com este nome neste projeto.");

        if (dto.Depende is { } pai)
        {
            // MN_DEPENDE precisa apontar para um MN_ID existente na própria CH_MENU, do mesmo projeto
            var menuPai = await repo.ObterAsync(pai, ct);
            if (menuPai is null)
                return Erro(nameof(dto.Depende), $"Não existe menu com o código {pai}.");
            if (pai == id)
                return Erro(nameof(dto.Depende), "Um menu não pode depender dele mesmo.");
            if (menuPai.Projeto != dto.Projeto)
                return Erro(nameof(dto.Depende), $"O menu {pai} pertence a outro projeto.");
            if (id is { } atual && await repo.CriariaCicloAsync(atual, pai, ct))
                return Erro(nameof(dto.Depende), $"O menu {pai} já está abaixo deste item: a dependência criaria um ciclo.");
        }

        // Mudar o projeto de um item com submenus deixaria a árvore dividida entre projetos
        if (id is { } existente && await repo.FilhosDeOutroProjetoAsync(existente, dto.Projeto, ct) > 0)
            return Erro(nameof(dto.Projeto), "Os submenus deste item pertencem a outro projeto: altere-os antes.");
        return null;
    }

    private ActionResult ProjetoInexistente(MenuDto dto) =>
        Erro(nameof(dto.Projeto), $"Não existe projeto com o código {dto.Projeto}.");

    private ActionResult Erro(string campo, string mensagem) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [campo] = [mensagem] }));
}
