namespace NuclearEvaluation.Kernel.Commands;

public class FilterDataResponse<T>()
{
    public IEnumerable<T> Entries { get; set; } = Enumerable.Empty<T>();

    public int TotalCount { get; set; } = 0;
}