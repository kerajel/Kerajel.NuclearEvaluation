using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Server.Services.Assistant;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Domain;
using NuclearEvaluation.Shared.Models.Views;
using Z.EntityFramework.Plus;

namespace NuclearEvaluation.Server.Services.Data;

public class SeriesService : DbServiceBase, ISeriesService
{
    readonly ILogger<SeriesView> _logger;
    readonly SeriesSchemaQueries _schemaQueries;

    public SeriesService(NuclearEvaluationServerDbContext _dbContext, ILogger<SeriesView> logger)
        : base(_dbContext)
    {
        _logger = logger;
        _schemaQueries = new(_dbContext);
    }

    public async Task<FetchDataResult<SeriesView>> GetSeriesViews(
        FetchDataCommand<SeriesView> command
    )
    {
        try
        {
            IQueryable<SeriesView> baseQuery = _dbContext.SeriesView;
            int? projectId = command.Query?.ProjectId;
            if (projectId.HasValue)
            {
                baseQuery = baseQuery.Where(x =>
                    x.ProjectSeries.Any(s => s.ProjectId == projectId.Value)
                );
            }

            if (command.Query?.SeriesQuery is { } seriesQuery)
            {
                IQueryable<SeriesView> filtered = ApplySeriesFilters(
                    GetFilteredQuery(
                        command.AsNoTracking ? baseQuery.AsNoTracking() : baseQuery,
                        command
                    ),
                    seriesQuery
                );
                IQueryable<SeriesView> ordered = OrderAndLimitSeries(
                    filtered,
                    command,
                    seriesQuery
                );
                return await FetchPageAsync(
                    seriesQuery.Limit.HasValue ? ordered : filtered,
                    ordered,
                    command
                );
            }
            return await ExecuteQuery(baseQuery, command);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error occurred while fetching series views");
            return FetchDataResult<SeriesView>.Faulted(ex);
        }
    }

    public async Task<SeriesCountsView> GetSeriesCounts(FetchDataCommand<SeriesView> command)
    {
        IQueryable<SeriesView> baseQuery = _dbContext.SeriesView;
        if (command.Query?.ProjectId is int projectId)
        {
            baseQuery = baseQuery.Where(x => x.ProjectSeries.Any(s => s.ProjectId == projectId));
        }

        IQueryable<SeriesView> filteredQuery = GetFilteredQuery(baseQuery, command);
        if (command.Query?.SeriesQuery is { } seriesQuery)
        {
            filteredQuery = ApplySeriesFilters(filteredQuery, seriesQuery);
            if (seriesQuery.Limit.HasValue)
            {
                filteredQuery = OrderAndLimitSeries(filteredQuery, command, seriesQuery);
            }
        }

        IQueryable<int> seriesIdsQuery = filteredQuery.Select(s => s.Id);

        SeriesCountsView? result = await _dbContext
            .SeriesView.Select(s => new SeriesCountsView
            {
                SeriesCount = seriesIdsQuery.Count(),
                SampleCount = (
                    from sa in _dbContext.SampleView
                    join sId in seriesIdsQuery on sa.SeriesId equals sId
                    select sa.Id
                )
                    .Distinct()
                    .Count(),
                SubSampleCount = (
                    from ss in _dbContext.SubSampleView
                    join sa in _dbContext.SampleView on ss.SampleId equals sa.Id
                    join sId in seriesIdsQuery on sa.SeriesId equals sId
                    select ss.Id
                )
                    .Distinct()
                    .Count(),
                ParticleCount = (
                    from p in _dbContext.ParticleView
                    join ss in _dbContext.SubSampleView on p.SubSampleId equals ss.Id
                    join sa in _dbContext.SampleView on ss.SampleId equals sa.Id
                    join sId in seriesIdsQuery on sa.SeriesId equals sId
                    select p.Id
                )
                    .Distinct()
                    .Count(),
                ApmCount = (
                    from a in _dbContext.ApmView
                    join ss in _dbContext.SubSample on a.SubSampleId equals ss.Id
                    join sa in _dbContext.SampleView on ss.SampleId equals sa.Id
                    join sId in seriesIdsQuery on sa.SeriesId equals sId
                    select a.Id
                )
                    .Distinct()
                    .Count(),
            })
            .OrderBy(x => 1)
            .FirstOrDefaultAsync(command.CancellationToken);

        return result ?? new();
    }

    IQueryable<SeriesView> ApplySeriesFilters(IQueryable<SeriesView> series, SeriesGridQuery query)
    {
        series = _schemaQueries.ApplyFilters(series, query);
        if (query.Type is { } type)
            series = series.Where(row => row.SeriesType == type);
        if (query.SeriesId is int id)
            series = series.Where(row => row.Id == id);
        if (query.IsDu is bool isDu)
            series = series.Where(row => row.IsDu == isDu);
        if (query.IsNu is bool isNu)
            series = series.Where(row => row.IsNu == isNu);
        if (query.IsComplete is bool isComplete)
            series = series.Where(row => row.AnalysisCompleteDate.HasValue == isComplete);
        if (query.CreatedFrom is { } from)
        {
            DateTime lowerDate = from.ToDateTime(TimeOnly.MinValue);
            series = series.Where(row => row.CreatedAt >= lowerDate);
        }
        if (query.CreatedBefore is { } before)
        {
            DateTime upperDate = before.ToDateTime(TimeOnly.MinValue);
            series = series.Where(row => row.CreatedAt < upperDate);
        }
        if (query.MinSamples is int minSamples)
            series = series.Where(row => row.SampleCount >= minSamples);
        if (query.MaxSamples is int maxSamples)
            series = series.Where(row => row.SampleCount <= maxSamples);

        IQueryable<Sample> qcSamples = _dbContext.Sample.Where(sample =>
            sample.SampleType == SampleType.Qc
        );
        if (query.MinQcSamples is int minQcSamples)
            series = series.Where(row =>
                qcSamples.Count(sample => sample.SeriesId == row.Id) >= minQcSamples
            );
        if (query.MaxQcSamples is int maxQcSamples)
            series = series.Where(row =>
                qcSamples.Count(sample => sample.SeriesId == row.Id) <= maxQcSamples
            );
        if (query.CommentContains is { } comment)
            series = series.Where(row => row.SgasComment.Contains(comment));

        return series;
    }

    IQueryable<SeriesView> OrderAndLimitSeries(
        IQueryable<SeriesView> series,
        FetchDataCommand<SeriesView> command,
        SeriesGridQuery query
    )
    {
        Expression<Func<SeriesView, object?>>? orderKey = query.Order is { } order
            ? _schemaQueries.GetOrderKey(order)
            : query.SortBy switch
            {
                SeriesGridSort.QcSampleCount => row =>
                    _dbContext.Sample.Count(sample =>
                        sample.SeriesId == row.Id && sample.SampleType == SampleType.Qc
                    ),
                SeriesGridSort.SampleCount => row => row.SampleCount,
                SeriesGridSort.CreatedAt => row => row.CreatedAt,
                SeriesGridSort.AnalysisCompleteDate => row => row.AnalysisCompleteDate,
                SeriesGridSort.Id => row => row.Id,
                _ => null,
            };

        // Totals need the same top set even when only the grid endpoint supplied a priority order.
        if (
            orderKey is null
            && command.TopLevelOrderExpression is null
            && command.Query?.PriorityIds is { Count: > 0 } priorityIds
        )
        {
            series = series.OrderBy(row => priorityIds.Contains(row.Id) ? 0 : 1);
        }

        IQueryable<SeriesView> ordered = orderKey is null
            ? GetOrderedQuery(series, command)
            : (
                (query.Order?.Descending ?? query.Descending)
                    ? series.OrderByDescending(orderKey)
                    : series.OrderBy(orderKey)
            ).ThenBy(row => row.Id);
        return query.Limit is int limit ? ordered.Take(limit) : ordered;
    }

    public async Task<Series> CreateSeriesFromView(SeriesView seriesView)
    {
        Series series = new()
        {
            SeriesType = seriesView.SeriesType,
            SgasComment = seriesView.SgasComment,
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
        await _dbContext
            .Series.Where(x => x.Id == seriesView.Id)
            .UpdateFromQueryAsync(x => new Series
            {
                SeriesType = seriesView.SeriesType,
                SgasComment = seriesView.SgasComment,
                WorkingPaperLink = seriesView.WorkingPaperLink,
                IsDu = seriesView.IsDu,
                IsNu = seriesView.IsNu,
                AnalysisCompleteDate = seriesView.AnalysisCompleteDate,
            });
    }

    public async Task LoadSamples(SeriesView seriesView)
    {
        seriesView.Samples = await _dbContext
            .SampleView.Where(x => x.SeriesId == seriesView.Id)
            .OrderBy(x => x.Sequence)
            .ToListAsync();
    }

    public async Task Delete(params SeriesView[] seriesViews)
    {
        IEnumerable<int> seriesIds = seriesViews.Select(x => x.Id);
        await _dbContext.Series.Where(x => seriesIds.Contains(x.Id)).DeleteFromQueryAsync();
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
