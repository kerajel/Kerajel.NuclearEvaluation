using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Kernel.Interfaces
{
    public interface ISeriesService
    {
        Task<Series> CreateSeriesFromView(SeriesView seriesView);
        Task Delete(params SeriesView[] seriesViews);
        Task<SeriesCountsView> GetSeriesCounts(FilterDataCommand<SeriesView> command);
        Task<FilterDataResult<SeriesView>> GetSeriesViews(FilterDataCommand<SeriesView> command);
        Task LoadSamples(SeriesView seriesView);
        void ResetPendingChanges(params SeriesView[] seriesViews);
        Task UpdateSeriesFromView(SeriesView seriesView);
    }
}