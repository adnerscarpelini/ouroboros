namespace Ouroboros.Auth.Application.UseCases.GetUser;

public interface IGetUserUseCase
{
    Task<GetUserResponse> ExecuteAsync(GetUserRequest request);
}
