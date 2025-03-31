using NuclearEvaluation.Kernel.Commands;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IGenericService
{
    Task<FetchDataResult<dynamic>> GetFilterOptions<T>(FetchDataCommand<T> command, string propertyName) where T : class;
}