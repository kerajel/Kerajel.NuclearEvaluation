namespace NuclearEvaluation.Shared.Contracts;

public class DataResult<T>
{
    public List<T> Entries { get; set; } = [];

    public int TotalCount { get; set; }

    public bool IsSuccessful { get; set; } = true;

    public string? ErrorMessage { get; set; }

    public static DataResult<T> Succeeded(List<T> entries, int totalCount)
    {
        return new() { Entries = entries, TotalCount = totalCount };
    }

    public static DataResult<T> Faulted(string? errorMessage = null)
    {
        return new() { IsSuccessful = false, ErrorMessage = errorMessage };
    }
}
