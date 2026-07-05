namespace Momentum.Application.Users;

public interface IUserService
{
    Task<UserDto> GetMeAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserDto> UpdateMeAsync(Guid userId, UpdateMeRequest request, CancellationToken cancellationToken);
}
