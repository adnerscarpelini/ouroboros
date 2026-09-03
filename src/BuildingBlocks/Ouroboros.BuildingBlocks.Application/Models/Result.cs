namespace Ouroboros.BuildingBlocks.Application;

public class Result
{
	public bool IsSuccess { get; }
	public string? Error { get; }

	private Result(
		bool isSuccess,
		string? error
	)
	{
		IsSuccess = isSuccess;
		Error = error;
	}

	public static Result Success()
	{
		return new Result(isSuccess: true, error: null);
	}

	public static Result Failure(string error)
	{
		return new Result(isSuccess: false, error: error);
	}
}

public sealed class Result<T>
{
	public bool IsSuccess { get; }
	public T? Value { get; }
	public string? Error { get; }

	private Result(
		bool isSuccess,
		T? value,
		string? error
	)
	{
		IsSuccess = isSuccess;
		Value = value;
		Error = error;
	}

	public static Result<T> Success(T value)
	{
		return new Result<T>(isSuccess: true, value: value, error: null);
	}

	public static Result<T> Failure(string error)
	{
		return new Result<T>(isSuccess: false, value: default, error: error);
	}
}
