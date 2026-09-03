using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Application.Tests;

public class ResultTests
{
	[Fact]
	public void Success_CreatesSuccessfulResultWithoutError()
	{
		var result = Result.Success();

		Assert.True(result.IsSuccess);
		Assert.Null(result.Error);
	}

	[Fact]
	public void Failure_CreatesFailedResultWithError()
	{
		var result = Result.Failure("Algo deu errado.");

		Assert.False(result.IsSuccess);
		Assert.Equal("Algo deu errado.", result.Error);
	}

	[Fact]
	public void SuccessOfT_CarriesValueAndNoError()
	{
		var result = Result<int>.Success(42);

		Assert.True(result.IsSuccess);
		Assert.Equal(42, result.Value);
		Assert.Null(result.Error);
	}

	[Fact]
	public void FailureOfT_CarriesErrorAndDefaultValue()
	{
		var result = Result<int>.Failure("Algo deu errado.");

		Assert.False(result.IsSuccess);
		Assert.Equal(0, result.Value);
		Assert.Equal("Algo deu errado.", result.Error);
	}

	[Fact]
	public void FailureOfT_WithReferenceType_LeavesValueNull()
	{
		var result = Result<string>.Failure("Algo deu errado.");

		Assert.False(result.IsSuccess);
		Assert.Null(result.Value);
	}
}
