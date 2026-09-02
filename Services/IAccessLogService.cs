namespace AiHelpers.Services;

public interface IAccessLogService
{
    Task LogAsync(string email, CancellationToken cancellationToken = default);
}
