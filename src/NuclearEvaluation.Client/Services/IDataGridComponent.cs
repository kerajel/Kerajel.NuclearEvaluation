namespace NuclearEvaluation.Client.Services;

using System.Threading.Tasks;

public interface IDataGridComponent
{
    Task Reset(bool resetColumnState = true, bool resetRowState = false);

    bool Visible { get; set; }

    string EntityDisplayName { get; }
}