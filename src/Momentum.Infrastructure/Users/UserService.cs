using Microsoft.AspNetCore.Identity;
using Momentum.Application.Common.Exceptions;
using Momentum.Application.Users;
using Momentum.Infrastructure.Identity;

namespace Momentum.Infrastructure.Users;

public sealed class UserService(UserManager<ApplicationUser> userManager) : IUserService
{
    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User", userId);

        return user.ToDto();
    }

    public async Task<UserDto> UpdateMeAsync(Guid userId, UpdateMeRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User", userId);

        if (request.Name is not null)
        {
            user.Name = request.Name;
        }

        if (request.JobTitle is not null)
        {
            user.JobTitle = request.JobTitle;
        }

        if (request.Timezone is not null)
        {
            user.TimeZoneId = request.Timezone;
        }

        if (request.Locale is not null)
        {
            user.Locale = request.Locale;
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new ConflictException(
                $"Could not update user: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        return user.ToDto();
    }
}
