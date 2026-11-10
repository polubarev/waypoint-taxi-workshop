using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Auth.Domain;
using Npgsql;
using Shared.Contracts;
using Shared.Infrastructure;
namespace Auth.Api;
public sealed class Tokens(Database db,IConfiguration config,AuthStore users)
{
    private TokenResponse Make(UserInfo user,string refresh)
    {
        var expiry=DateTimeOffset.UtcNow.AddMinutes(15);
        var claims=new List<Claim> { new(Claims.Subject,user.Id.ToString()),new(Claims.Team,user.TeamId),new(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString()) };
        if(user.ManagerId.HasValue) claims.Add(new(Claims.Manager,user.ManagerId.Value.ToString()));
        claims.AddRange(user.Roles.Select(r=>new Claim(Claims.Role,r)));
        var token=new JwtSecurityToken("taxi-auth","taxi-services",claims,expires:expiry.UtcDateTime,signingCredentials:new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token),refresh,expiry,user);
    }
    public async Task<TokenResponse> Issue(UserInfo user)
    {
        var refresh=Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        await db.Execute("INSERT INTO refresh_tokens VALUES($1,$2,$3)",Passwords.TokenHash(refresh),user.Id,DateTimeOffset.UtcNow.AddDays(1));
        return Make(user,refresh);
    }
    public async Task<TokenResponse> Rotate(string old)
    {
        await using var conn=await db.Source.OpenConnectionAsync();
        await using var tx=await conn.BeginTransactionAsync();
        await using var cmd=new NpgsqlCommand("DELETE FROM refresh_tokens WHERE hash=$1 AND expires_at>now() RETURNING user_id",conn,tx);
        Database.Add(cmd,Passwords.TokenHash(old));
        var id=await cmd.ExecuteScalarAsync();
        if(id is not Guid userId) throw new ApiError(401,"Refresh token is invalid or expired");
        var user=await users.Find(userId) ?? throw new ApiError(401,"User no longer exists");
        var refresh=Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        await using var insert=new NpgsqlCommand("INSERT INTO refresh_tokens VALUES($1,$2,$3)",conn,tx);
        Database.Add(insert,Passwords.TokenHash(refresh),user.Id,DateTimeOffset.UtcNow.AddDays(1));
        await insert.ExecuteNonQueryAsync();
        await tx.CommitAsync();
        return Make(user,refresh);
    }
}
