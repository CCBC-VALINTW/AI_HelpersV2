namespace AiHelpers.Services;

public class FeedbackPromptService : IFeedbackPromptService
{
    public Func<string?, int?, Task>? ShowHandler { get; set; }
    public Task RequestAsync(string? helperName, int? helperDefinitionId) =>
        ShowHandler?.Invoke(helperName, helperDefinitionId) ?? Task.CompletedTask;

    public event Action? RunCompleted;
    public void NotifyRunCompleted() => RunCompleted?.Invoke();
}
