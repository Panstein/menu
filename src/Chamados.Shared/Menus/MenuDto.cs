using System.ComponentModel.DataAnnotations;

namespace Chamados.Shared.Menus;

/// <summary>Item de menu (tabela CH_MENU). MN_NOME gravado em maiúsculas; MN_LABEL como digitado.</summary>
public class MenuDto
{
    /// <summary>MN_ID: gerado pela sequence SEQ_CH_MENU na inclusão; não editável.</summary>
    public long? Id { get; set; }

    /// <summary>MN_NOME: identificador do item (ex.: CAD_USUARIOS).</summary>
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(100, ErrorMessage = "Máximo de 100 caracteres.")]
    public string Nome { get; set; } = "";

    /// <summary>MN_LABEL: texto exibido no menu (ex.: USUARIOS).</summary>
    [Required(ErrorMessage = "Informe o label.")]
    [StringLength(100, ErrorMessage = "Máximo de 100 caracteres.")]
    public string Label { get; set; } = "";

    /// <summary>MN_DEPENDE: MN_ID do menu pai (deve existir na CH_MENU).</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Informe um código de menu válido.")]
    public long? Depende { get; set; }

    /// <summary>MN_ORDEM: posição entre os itens do mesmo nível.</summary>
    [Required(ErrorMessage = "Informe a ordem.")]
    [Range(0, 99999, ErrorMessage = "A ordem deve estar entre 0 e 99999.")]
    public int? Ordem { get; set; }

    /// <summary>MN_PROJETO: PJ_ID do projeto (FK para CH_PROJETOS). Cada sistema exibe só os menus do seu projeto.</summary>
    [Required(ErrorMessage = "Informe o projeto.")]
    [Range(1, long.MaxValue, ErrorMessage = "Informe um código de projeto válido.")]
    public long? Projeto { get; set; }

    /// <summary>Somente leitura: MN_LABEL do menu pai (para exibição).</summary>
    public string? DependeLabel { get; set; }

    /// <summary>Somente leitura: PJ_NOME do projeto (para exibição).</summary>
    public string? ProjetoNome { get; set; }

    public void Normalizar()
    {
        Nome = Nome.Trim().ToUpperInvariant();
        Label = Label.Trim();
    }
}
