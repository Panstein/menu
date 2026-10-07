namespace Chamados.Api.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Key { get; set; } = "";
    public int ExpiracaoHoras { get; set; } = 8;
}
