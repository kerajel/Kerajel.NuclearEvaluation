namespace NuclearEvaluation.Client.Services;

using System.Threading.Tasks;
using NuclearEvaluation.Shared.Contracts;

public interface IDataGridComponent
{
    Task Reset(bool resetColumnState = true, bool resetRowState = false);

    Task SetAssistantGridQuery(AssistantGridQuery? query) =>
        Task.FromException(
            new NotSupportedException("This grid does not accept an assistant query.")
        );

    bool Visible { get; set; }

    string EntityDisplayName { get; }
}
