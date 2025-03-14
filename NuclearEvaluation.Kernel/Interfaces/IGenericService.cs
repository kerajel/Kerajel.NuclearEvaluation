using NuclearEvaluation.Kernel.Commands;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IGenericService
{
    Task<FilterDataResponse<dynamic>> GetFilterOptions<T>(FilterDataCommand<T> command, string propertyName) where T : class;
}