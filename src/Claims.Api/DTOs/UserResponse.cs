using Claims.Api.Enums;
using Claims.Api.Models;

namespace Claims.Api.DTOs;

public record UserResponse(Guid Id, string FullName, string Email, UserRole Role)
{
    public static UserResponse From(AppUser user) => new(user.Id, user.FullName, user.Email, user.Role);
}
