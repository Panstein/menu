using Chamados.Web.Services;

namespace Chamados.Web.Layout.Menu;

/// <summary>
/// Guarda o menu lateral lido da CH_MENU (só os itens do projeto deste sistema). É carregado
/// após o login e recarregado ao entrar/sair e quando o cadastro de menus é alterado.
/// </summary>
public sealed class MenuLateralService(MenusApi api, ProjetoAtual projeto)
{
    private Task<IReadOnlyList<MenuItem>>? carga;

    /// <summary>Disparado quando o menu precisa ser lido de novo.</summary>
    public event Action? Alterado;

    public Task<IReadOnlyList<MenuItem>> ObterAsync()
    {
        if (carga is null || carga.IsFaulted || carga.IsCanceled)
            carga = CarregarAsync();
        return carga;
    }

    /// <summary>Descarta o menu em memória: será lido de novo na próxima exibição (usado ao entrar/sair).</summary>
    public void Limpar() => carga = null;

    /// <summary>Relê o menu já exibido (usado após alterações no cadastro de menus).</summary>
    public void Recarregar()
    {
        Limpar();
        Alterado?.Invoke();
    }

    private async Task<IReadOnlyList<MenuItem>> CarregarAsync() =>
        MenuConfig.Montar(await api.ListarAsync(projeto.Id));
}
