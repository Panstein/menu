namespace Chamados.Web.Services;

/// <summary>
/// Projeto (CH_PROJETOS.PJ_ID) deste sistema: base para a montagem do menu lateral, que mostra
/// só os itens da CH_MENU com MN_PROJETO igual a ele.
/// </summary>
public sealed record ProjetoAtual(long Id)
{
    /// <summary>Este sistema é o projeto 1 (MENU).</summary>
    public const long Padrao = 1;
}
