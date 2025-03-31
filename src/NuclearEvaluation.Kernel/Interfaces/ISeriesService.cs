using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Kernel.Interfaces
{
    public interface ISeriesService
    {
        Task<Series> CreateSeriesFromView(SeriesView seriesView);
        Task Delete(params SeriesView[] seriesViews);
        Task<SeriesCountsView> GetSeriesCounts(FetchDataCommand<SeriesView> command);
        Task<FetchDataResult<SeriesView>> GetSeriesViews(FetchDataCommand<SeriesView> command);
        Task LoadSamples(SeriesView seriesView);
        void ResetPendingChanges(params SeriesView[] seriesViews);
        Task UpdateSeriesFromView(SeriesView seriesView);
    }
}