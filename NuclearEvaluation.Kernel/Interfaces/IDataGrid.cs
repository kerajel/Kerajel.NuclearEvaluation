namespace NuclearEvaluation.Kernel.Interfaces;

using System.Threading.Tasks;

public interface IDataGrid
{
    Task Reset(bool resetColumnState = true, bool resetRowState = false);

    bool Visible { get; set; }

    string EntityDisplayName { get; }
}