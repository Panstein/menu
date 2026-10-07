using Chamados.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace Chamados.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(IDbConnectionFactory db, ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet("db")]
    public async Task<IActionResult> Database(CancellationToken ct)
    {
        try
        {
            await using var connection = await db.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT current_database(), current_user, now()::timestamp";

            await using var reader = await command.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);

            return Ok(new DbStatus(
                Conectado: true,
                Banco: reader.GetString(0),
                Usuario: reader.GetString(1),
                DataServidor: reader.GetDateTime(2),
                Versao: connection.ServerVersion,
                Erro: null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao conectar no PostgreSQL");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new DbStatus(false, null, null, null, null, ex.Message));
        }
    }
}

public record DbStatus(bool Conectado, string? Banco, string? Usuario, DateTime? DataServidor, string? Versao, string? Erro);
