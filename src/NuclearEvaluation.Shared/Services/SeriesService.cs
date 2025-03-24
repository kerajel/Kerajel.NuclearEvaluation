using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Kernel.Models.Views;
using Z.EntityFramework.Plus;

namespace NuclearEvaluation.Shared.Services;

public class SeriesService : DbServiceBase, ISeriesService
{
    readonly ILogger<SeriesView> _logger;

    public SeriesService(
        NuclearEvaluationServerDbContext _dbContext,
        ILogger<SeriesView> logger) : base(_dbContext)
    {
        _logger = logger;
    }

    public async Task<FilterDataResult<SeriesView>> GetSeriesViews(FilterDataCommand<SeriesView> command)
    {
        try
        {
            IQueryable<SeriesView> baseQuery = _dbContext.SeriesView;
            return await ExecuteQuery(baseQuery, command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "");
            return FilterDataResult<SeriesView>.Faulted(ex);
        }
    }

    public async Task<SeriesCountsView> GetSeriesCounts(FilterDataCommand<SeriesView> command)
    {
        IQueryable<SeriesView> baseQuery = _dbContext.SeriesView;

        IQueryable<SeriesView> filteredQuery = GetFilteredQuery(baseQuery, command, false);

        IQueryable<int> seriesIdsQuery = filteredQuery.Select(s => s.Id);

        SeriesCountsView? result = await _dbContext.SeriesView
            .Select(s => new SeriesCountsView
            {
                SeriesCount = seriesIdsQuery.Count(),
                SampleCount = (from sa in _dbContext.SampleView
                               join sId in seriesIdsQuery on sa.SeriesId equals sId
                               select sa.Id).Distinct().Count(),
                SubSampleCount = (from ss in _dbContext.SubSampleView
                                  join sa in _dbContext.SampleView on ss.SampleId equals sa.Id
                                  join sId in seriesIdsQuery on sa.SeriesId equals sId
                                  select ss.Id).Distinct().Count(),
                ParticleCount = (from p in _dbContext.ParticleView
                                 join ss in _dbContext.SubSampleView on p.SubSampleId equals ss.Id
                                 join sa in _dbContext.SampleView on ss.SampleId equals sa.Id
                                 join sId in seriesIdsQuery on sa.SeriesId equals sId
                                 select p.Id).Distinct().Count(),
                ApmCount = (from a in _dbContext.ApmView
                            join ss in _dbContext.SubSample on a.SubSampleId equals ss.Id
                            join sa in _dbContext.SampleView on ss.SampleId equals sa.Id
                            join sId in seriesIdsQuery on sa.SeriesId equals sId
                            select a.Id).Distinct().Count(),
            })
            .OrderBy(x => 1)
            .FirstOrDefaultAsync();

        return result ?? new();
    }

    public async Task<Series> CreateSeriesFromView(SeriesView seriesView)
    {
        Series series = new()
        {
            SeriesType = seriesView.SeriesType,
            WorkingPaperLink = seriesView.WorkingPaperLink,
            IsDu = seriesView.IsDu,
            IsNu = seriesView.IsNu,
            AnalysisCompleteDate = seriesView.AnalysisCompleteDate,
            CreatedAt = seriesView.CreatedAt,
        };

        _dbContext.Series.Add(series);
        await _dbContext.SaveChangesAsync();
        return series;
    }

    public async Task UpdateSeriesFromView(SeriesView seriesView)
    {
        await _dbContext.Series.Where(x => x.Id == seriesView.Id)
            .UpdateFromQueryAsync(x => new Series
            {
                SeriesType = seriesView.SeriesType,
                WorkingPaperLink = seriesView.WorkingPaperLink,
                IsDu = seriesView.IsDu,
                IsNu = seriesView.IsNu,
                AnalysisCompleteDate = seriesView.AnalysisCompleteDate,
            });
    }

    public async Task LoadSamples(SeriesView seriesView)
    {
        seriesView.Samples = await _dbContext.SampleView
            .Where(x => x.SeriesId == seriesView.Id)
            .OrderBy(x => x.Sequence)
            .ToListAsync();
    }

    public async Task Delete(params SeriesView[] seriesViews)
    {
        IEnumerable<int> seriesIds = seriesViews.Select(x => x.Id);
        await _dbContext.Series
            .Where(x => seriesIds.Contains(x.Id))
            .DeleteFromQueryAsync();
    }

    public void ResetPendingChanges(params SeriesView[] seriesViews)
    {
        foreach (SeriesView seriesView in seriesViews)
        {
            EntityEntry<SeriesView> seriesViewEntry = _dbContext.Entry(seriesView);
            if (seriesViewEntry.State == EntityState.Modified)
            {
                seriesViewEntry.CurrentValues.SetValues(seriesViewEntry.OriginalValues);
                seriesViewEntry.State = EntityState.Unchanged;
            }
        }
    }
}