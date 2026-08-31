using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Api.Validation;

public sealed class StrongPasswordAttribute : ValidationAttribute
{
	private const int MinLength = 12;

	protected override ValidationResult? IsValid(
		object? value,
		ValidationContext validationContext
	)
	{
		if (value is not string password || password.Length == 0)
		{
			return ValidationResult.Success;
		}

		if (password.Length < MinLength)
		{
			return new ValidationResult($"A senha deve ter pelo menos {MinLength} caracteres.");
		}

		if (!password.Any(char.IsUpper))
		{
			return new ValidationResult("A senha deve conter pelo menos uma letra maiúscula.");
		}

		if (!password.Any(char.IsLower))
		{
			return new ValidationResult("A senha deve conter pelo menos uma letra minúscula.");
		}

		if (!password.Any(char.IsDigit))
		{
			return new ValidationResult("A senha deve conter pelo menos um número.");
		}

		if (password.All(char.IsLetterOrDigit))
		{
			return new ValidationResult("A senha deve conter pelo menos um caractere especial.");
		}

		return ValidationResult.Success;
	}
}
