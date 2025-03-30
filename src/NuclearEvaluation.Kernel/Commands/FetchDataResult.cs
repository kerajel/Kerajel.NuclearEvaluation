using Kerajel.Primitives.Models;
using Kerajel.Primitives.Enums;

namespace NuclearEvaluation.Kernel.Commands;

public class FetchDataResult<T>() : OperationResultBase
{
    public IEnumerable<T> Entries { get; set; } = Enumerable.Empty<T>();

    public int TotalCount { get; set; } = 0;

    public static FetchDataResult<T> Succeeded(IEnumerable<T> entries)
    {
        return new()
        {
            Entries = entries,
            OperationStatus = OperationStatus.Succeeded,
        };
    }

    public static FetchDataResult<T> Faulted(Exception ex)
    {
        return new FetchDataResult<T>
        {
            Exception = ex,
            OperationStatus = OperationStatus.Faulted,
        };
    }
}