using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Controllers;

/// <summary>Translates the wire <see cref="DataQuery"/> into the server-side command and back.</summary>
internal static class ApiMapping
{
    public static FetchDataCommand<T> ToCommand<T>(this DataQuery query)
    {
        return new FetchDataCommand<T> { Query = query };
    }

    public static DataResult<T> ToDataResult<T>(this FetchDataResult<T> result)
    {
        if (!result.IsSuccessful && !result.NotFound)
        {
            return DataResult<T>.Faulted(result.Exception?.Message);
        }

        return DataResult<T>.Succeeded([.. result.Entries], result.TotalCount);
    }
}
