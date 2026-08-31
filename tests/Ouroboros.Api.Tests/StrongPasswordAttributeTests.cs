using System.ComponentModel.DataAnnotations;
using Ouroboros.Api.Validation;

namespace Ouroboros.Api.Tests;

public class StrongPasswordAttributeTests
{
	private static ValidationResult? Validate(string password)
	{
		var attribute = new StrongPasswordAttribute();
		var validationContext = new ValidationContext(new object());

		return attribute.GetValidationResult(password, validationContext);
	}

	[Fact]
	public void Validate_WithStrongPassword_ReturnsSuccess()
	{
		Assert.Equal(ValidationResult.Success, Validate("S3nhaF0rte#2026"));
	}

	[Fact]
	public void Validate_WithEmptyPassword_ReturnsSuccess()
	{
		Assert.Equal(ValidationResult.Success, Validate(string.Empty));
	}

	[Theory]
	[InlineData("Curta1!")]
	[InlineData("semmaiuscula1!")]
	[InlineData("SEMMINUSCULA1!")]
	[InlineData("SemNumeroAqui!")]
	[InlineData("SemCaractereEspecial1")]
	public void Validate_WithWeakPassword_ReturnsFailure(string password)
	{
		Assert.NotEqual(ValidationResult.Success, Validate(password));
	}
}
