using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Chamados.Api.Auth;

public sealed class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opt = options.Value;

    public static SymmetricSecurityKey ChaveDe(JwtOptions opt) => new(Encoding.UTF8.GetBytes(opt.Key));

    public (string Token, DateTime ExpiraEm) Gerar(string email, string nome)
    {
        var expira = DateTime.UtcNow.AddHours(_opt.ExpiracaoHoras);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _opt.Issuer,
            Audience = _opt.Audience,
            Expires = expira,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, email),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Name, nome),
                new Claim("role", "SuperUsuario")
            ]),
            SigningCredentials = new SigningCredentials(ChaveDe(_opt), SecurityAlgorithms.HmacSha256)
        });

        return (token, expira);
    }
}
