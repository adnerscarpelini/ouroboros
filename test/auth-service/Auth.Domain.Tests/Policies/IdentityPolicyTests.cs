namespace Ouroboros.Auth.Domain.Policies;

using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

// Spec 2026092508: normalizacao e formatos de login e e-mail.
public class IdentityPolicyTests
{
    [Fact]
    public void ShouldNormalizeToUpperCaseWithoutEdgeSpaces()
    {
        Assert.Equal("JOAO.SILVA@EXAMPLE.COM", IdentityPolicy.Normalize("  Joao.Silva@Example.com "));
    }

    [Fact]
    public void ShouldKeepDotsAndPlusTagsWhenNormalizingEmail()
    {
        Assert.Equal("A.B+TAG@EXAMPLE.COM", IdentityPolicy.Normalize("a.b+tag@example.com"));
    }

    [Fact]
    public void ShouldFillNormalizedValuesWhenUserIsCreated()
    {
        var user = User.Create(" JDoe ", "John Doe", " Joao@Example.com ", "hash");

        Assert.Equal("JDoe", user.Login);
        Assert.Equal("JDOE", user.NormalizedLogin);
        Assert.Equal("Joao@Example.com", user.Email);
        Assert.Equal("JOAO@EXAMPLE.COM", user.NormalizedEmail);
    }

    [Fact]
    public void ShouldFillNormalizedValuesWhenUserIsRehydrated()
    {
        var user = User.Rehydrate(
            1, Guid.NewGuid(), DateTimeOffset.UtcNow, null, "JDoe", "John Doe", "Joao@Example.com",
            true, "hash", DateTimeOffset.UtcNow, true, null, UserRole.User, null);

        Assert.Equal("JDOE", user.NormalizedLogin);
        Assert.Equal("JOAO@EXAMPLE.COM", user.NormalizedEmail);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("this-login-has-more-than-thirty-two-characters")]
    [InlineData("john doe")]
    [InlineData("john@doe")]
    [InlineData(".john")]
    [InlineData("john_")]
    [InlineData("-john")]
    [InlineData("joão")]
    [InlineData("jоhn")]
    [InlineData("john​doe")]
    public void ShouldRejectLoginOutsideAllowlist(string login)
    {
        Assert.Throws<DomainException>(() => IdentityPolicy.ValidateLogin(login));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("john.doe")]
    [InlineData("john_doe-1")]
    [InlineData("A1b")]
    public void ShouldAcceptLoginInsideAllowlist(string login)
    {
        Assert.Equal(login, IdentityPolicy.ValidateLogin(login));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("two@@example.com")]
    [InlineData("a@b@c")]
    [InlineData("@example.com")]
    [InlineData("john@")]
    [InlineData("jo hn@example.com")]
    public void ShouldRejectMalformedEmail(string email)
    {
        Assert.Throws<DomainException>(() => IdentityPolicy.ValidateEmail(email));
    }

    [Fact]
    public void ShouldRejectEmailLongerThan254Characters()
    {
        var email = new string('a', 250) + "@x.co";

        Assert.Throws<DomainException>(() => IdentityPolicy.ValidateEmail(email));
    }

    [Fact]
    public void ShouldAcceptEmailWithExactly254Characters()
    {
        var email = new string('a', 249) + "@x.co";

        Assert.Equal(254, email.Length);
        Assert.Equal(email, IdentityPolicy.ValidateEmail(email));
    }
}
