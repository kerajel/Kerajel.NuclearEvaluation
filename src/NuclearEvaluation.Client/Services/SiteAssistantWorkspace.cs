using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Services;

public sealed class SiteAssistantWorkspace
{
    public SiteAssistantTarget? Current { get; private set; }
    public event Action? Changed;

    public void Set(
        object owner,
        SiteAssistantContext context,
        Func<AssistantAction, Task<bool>> apply,
        object? scope = null
    )
    {
        if (
            Current is { } existing
            && ReferenceEquals(existing.Owner, owner)
            && existing.Context == context
            && Equals(existing.Scope, scope)
        )
            return;

        Current = new SiteAssistantTarget(owner, context, apply, scope);
        Changed?.Invoke();
    }

    public void Remove(object owner)
    {
        if (Current is null || !ReferenceEquals(Current.Owner, owner))
            return;

        Current = null;
        Changed?.Invoke();
    }
}

public sealed record SiteAssistantTarget(
    object Owner,
    SiteAssistantContext Context,
    Func<AssistantAction, Task<bool>> ApplyAsync,
    object? Scope
);
