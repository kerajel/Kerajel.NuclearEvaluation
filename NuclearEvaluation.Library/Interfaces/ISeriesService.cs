using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Models.Domain;
using NuclearEvaluation.Library.Models.Views;

namespace NuclearEvaluation.Library.Interfaces
{
    public interface ISeriesService
    {
        Task<Series> CreateSeriesFromView(SeriesView seriesView);
        Task Delete(params SeriesView[] seriesViews);
        Task<SeriesCountsView> GetSeriesCounts(FilterDataCommand<SeriesView> command);
        Task<FilterDataResponse<SeriesView>> GetSeriesViews(FilterDataCommand<SeriesView> command);
        Task LoadSamples(SeriesView seriesView);
        void ResetPendingChanges(params SeriesView[] seriesViews);
        Task UpdateSeriesFromView(SeriesView seriesView);
    }
}