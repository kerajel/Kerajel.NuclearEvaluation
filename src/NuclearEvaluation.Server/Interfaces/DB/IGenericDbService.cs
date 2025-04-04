using NuclearEvaluation.Kernel.Commands;

namespace NuclearEvaluation.Server.Interfaces.DB;

public interface IGenericDbService
{
    Task<FetchDataResult<dynamic>> GetFilterOptions<T>(FetchDataCommand<T> command, string propertyName) where T : class;
}