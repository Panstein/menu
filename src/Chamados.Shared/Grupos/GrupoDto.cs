using System.ComponentModel.DataAnnotations;

namespace Chamados.Shared.Grupos;

/// <summary>Grupo de usuários (tabela CH_GRUPOS), sempre de um projeto. Nome gravado em maiúsculas.</summary>
public class GrupoDto
{
    /// <summary>CH_ID_GRUPO: gerado pela sequence SEQ_CH_GRUPOS na inclusão; não editável.</summary>
    public long? Id { get; set; }

    /// <summary>CH_NOME_GRUPO: nome do grupo (não se repete dentro do projeto).</summary>
    [Required(ErrorMessage = "Informe o nome.")]
    public string Nome { get; set; } = "";

    /// <summary>CH_PROJETO_GRUPO: PJ_ID do projeto (FK para CH_PROJETOS). Define quais menus o grupo pode acessar.</summary>
    [Required(ErrorMessage = "Informe o projeto.")]
    [Range(1, long.MaxValue, ErrorMessage = "Informe um projeto válido.")]
    public long? Projeto { get; set; }

    /// <summary>Somente leitura: PJ_NOME do projeto (para exibição).</summary>
    public string? ProjetoNome { get; set; }

    /// <summary>Somente leitura: quantidade de usuários no grupo (CH_USU_GRP).</summary>
    public int Usuarios { get; set; }

    public void Normalizar() => Nome = Nome.Trim().ToUpperInvariant();
}
