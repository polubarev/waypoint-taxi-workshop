using Auth.Domain;
using Shared.Contracts;
public class UserTests
{
    [Fact] public void PasswordHashIsSaltedAndVerified(){var a=Passwords.Hash("TaxiDemo!2026");var b=Passwords.Hash("TaxiDemo!2026");Assert.NotEqual(a,b);Assert.True(Passwords.Verify("TaxiDemo!2026",a));Assert.False(Passwords.Verify("wrong",a));}
    [Theory][InlineData("")][InlineData("bad")][InlineData("bad.hash")]
    public void BadHashDoesNotAuthenticate(string hash)=>Assert.False(Passwords.Verify("password",hash));
    [Fact] public void RoleValidationRejectsUnknownAndDuplicates(){Assert.True(UserRules.ValidRoles([Roles.Employee,Roles.Manager]));Assert.False(UserRules.ValidRoles([]));Assert.False(UserRules.ValidRoles(["admin"]));Assert.False(UserRules.ValidRoles([Roles.Manager,Roles.Manager]));}
    [Fact] public void TeamLookupDoesNotExposeOtherTeams()
    {
        var actor=Guid.NewGuid();var other=Guid.NewGuid();
        Assert.True(UserRules.CanSeeUser(actor,[],actor,null));Assert.True(UserRules.CanSeeUser(actor,[Roles.Manager],other,actor));Assert.True(UserRules.CanSeeUser(actor,[Roles.SystemAdmin],other,null));Assert.False(UserRules.CanSeeUser(actor,[Roles.Manager],other,Guid.NewGuid()));Assert.False(UserRules.CanSeeUser(actor,[Roles.Employee],other,null));
    }
    [Fact] public void RefreshTokensAreStoredAsHashes(){var hash=Passwords.TokenHash("secret");Assert.Equal(64,hash.Length);Assert.NotEqual("secret",hash);Assert.Equal(hash,Passwords.TokenHash("secret"));Assert.NotEqual(hash,Passwords.TokenHash("different"));}
}
