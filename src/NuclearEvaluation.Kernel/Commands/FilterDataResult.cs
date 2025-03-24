using Kerajel.Primitives.Models;
using Kerajel.Primitives.Enums;

namespace NuclearEvaluation.Kernel.Commands;

public class FilterDataResult<T>() : OperationResultBase
{
    public IEnumerable<T> Entries { get; set; } = Enumerable.Empty<T>();

    public int TotalCount { get; set; } = 0;

    public static FilterDataResult<T> Succeeded(IEnumerable<T> entries)
    {
        return new()
        {
            Entries = entries,
            OperationStatus = OperationStatus.Succeeded,
        };
    }

    public static FilterDataResult<T> Faulted(Exception ex)
    {
        return new FilterDataResult<T>
        {
            Exception = ex,
            OperationStatus = OperationStatus.Faulted,
        };
    }
}