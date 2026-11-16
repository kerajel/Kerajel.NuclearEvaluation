namespace NuclearEvaluation.Shared.Contracts;

public sealed record AssistantStatus(bool ChatAvailable);

public sealed record AssistantQuestion(string Message);

public sealed record AssistantReply(string Message, SeriesGridQuery? Query);
