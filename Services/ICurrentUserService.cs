namespace AiHelpers.Services;

public interface ICurrentUserService
{
    string Email { get; }
    string DisplayName { get; }
}
