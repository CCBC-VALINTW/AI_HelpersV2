namespace AiHelpers.Data.Enums;

/// <summary>
/// Generic reasoning-effort dial. Only meaningful for models where LlmDefinition.SupportsReasoning
/// is true - the provider adapter ignores it entirely for models without a reasoning mode.
/// </summary>
public enum EffortLevel
{
    Low,
    Medium,
    High
}
