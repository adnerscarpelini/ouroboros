namespace Ouroboros.Auth.Domain.Policies;

using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

// Spec 2026092509: tamanho em code points, NFKC, lista local e palavras de contexto.
public class PasswordPolicyTests
{
    [Fact]
    public void ShouldNormalizeWithNfkc()
    {
        Assert.Equal("fix", PasswordPolicy.Normalize("ﬁx"));
        Assert.Equal("café", PasswordPolicy.Normalize("café"));
        Assert.Equal("Str0ng-Passphrase-1", PasswordPolicy.Normalize("Str0ng-Passphrase-1"));
    }

    [Fact]
    public void ShouldMeasureLengthAfterNormalization()
    {
        // Oito ligaduras viram 16 letras depois do NFKC: passa do minimo de 15.
        PasswordPolicy.Validate(string.Concat(Enumerable.Repeat("ﬁ", 8)), "jdoe", "jdoe@example.com");
    }

    [Fact]
    public void ShouldRejectNullPassword()
    {
        Assert.Throws<DomainException>(() => PasswordPolicy.Validate(null, "jdoe", "jdoe@example.com"));
    }

    [Fact]
    public void ShouldCompareCommonPasswordsAfterNormalizationIgnoringCase()
    {
        // Largura total (NFKC volta ao ASCII) de uma senha da lista local.
        var fullWidth = "１Ｑ２Ｗ３Ｅ４Ｒ５Ｔ６Ｙ７Ｕ８Ｉ";

        var exception = Assert.Throws<DomainException>(() => PasswordPolicy.Validate(fullWidth, "jdoe", "jdoe@example.com"));

        Assert.Equal(PasswordPolicy.CommonOrBreachedMessage, exception.Message);
    }

    [Fact]
    public void ShouldIgnoreEmailLocalPartShorterThanFourCharacters()
    {
        PasswordPolicy.Validate("my-joe-passphrase-x1", "other", "joe@example.com");
    }

    [Fact]
    public void ShouldRejectEmailLocalPartWithFourOrMoreCharacters()
    {
        Assert.Throws<DomainException>(() => PasswordPolicy.Validate("my-maria-passphrase-x1", "other", "maria@example.com"));
    }
}
