using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Models.Views;

namespace NuclearEvaluation.Library.Interfaces;

public interface IStemPreviewEntryService
{
    Task<FilterDataResponse<StemPreviewEntryView>> GetStemPreviewEntryViews(FilterDataCommand<StemPreviewEntryView> command);
}