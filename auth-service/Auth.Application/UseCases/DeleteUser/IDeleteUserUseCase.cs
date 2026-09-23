namespace Ouroboros.Auth.Application.UseCases.DeleteUser;

public interface IDeleteUserUseCase
{
    Task<DeleteUserResponse> ExecuteAsync(DeleteUserRequest request);
}
