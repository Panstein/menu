using System.ComponentModel.DataAnnotations;

namespace Chamados.Shared.Projetos;

/// <summary>Projeto (tabela CH_PROJETOS). Nome gravado em maiúsculas.</summary>
public class ProjetoDto
{
    /// <summary>PJ_ID: gerado na inclusão (maior código + 1); não editável.</summary>
    public long? Id { get; set; }

    /// <summary>PJ_NOME: nome do projeto.</summary>
    [Required(ErrorMessage = "Informe o nome.")]
    public string Nome { get; set; } = "";

    public void Normalizar() => Nome = Nome.Trim().ToUpperInvariant();
}
