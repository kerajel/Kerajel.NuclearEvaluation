using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Plotting;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Services.Evaluation;

public class ChartService : DbServiceBase, IChartService
{
    public ChartService(NuclearEvaluationServerDbContext dbContext)
        : base(dbContext) { }

    public async Task<ILookup<string, BinCount>> GetProjectParticleUraniumBinCounts(int projectId)
    {
        return await GetProjectParticleUraniumBinCounts(
            new FetchDataCommand<ParticleView> { Query = await CreateProjectChartQuery(projectId) }
        );
    }

    public async Task<ILookup<string, BinCount>> GetProjectApmUraniumBinCounts(int projectId)
    {
        return await GetProjectApmUraniumBinCounts(
            new FetchDataCommand<ApmView> { Query = await CreateProjectChartQuery(projectId) }
        );
    }

    public async Task<ILookup<string, BinCount>> GetProjectParticleUraniumBinCounts(
        FetchDataCommand<ParticleView> command
    )
    {
        int? projectId = command.Query?.ProjectId;

        IQueryable<ParticleView> baseQuery =
            command.QueryKind == QueryKind.DecayCorrected
                ? _dbContext.ProjectDecayCorrectedParticleView.Where(x => x.ProjectId == projectId)
                : _dbContext.ParticleView;

        if (command.QueryKind != QueryKind.DecayCorrected && projectId.HasValue)
        {
            baseQuery = baseQuery.Where(pv =>
                pv.SubSample.Sample.Series.ProjectSeries.Any(y => y.ProjectId == projectId)
            );
        }

        IQueryable<ParticleView> filteredQuery = GetFilteredQuery(baseQuery, command);

        (string Isotope, Expression<Func<ParticleView, decimal?>>)[] isotopeSelectors =
        [
            (nameof(ParticleBase.U234), pv => pv.U234),
            (nameof(ParticleBase.U235), pv => pv.U235),
        ];

        return await GetUraniumBinCounts(
            filteredQuery,
            isotopeSelectors,
            command.CancellationToken
        );
    }

    public async Task<ILookup<string, BinCount>> GetProjectApmUraniumBinCounts(
        FetchDataCommand<ApmView> command
    )
    {
        int? projectId = command.Query?.ProjectId;

        IQueryable<ApmView> baseQuery =
            command.QueryKind == QueryKind.DecayCorrected
                ? _dbContext.ProjectDecayCorrectedApmView.Where(x => x.ProjectId == projectId)
                : _dbContext.ApmView;

        if (command.QueryKind != QueryKind.DecayCorrected && projectId.HasValue)
        {
            baseQuery = baseQuery.Where(pv =>
                pv.SubSample.Sample.Series.ProjectSeries.Any(y => y.ProjectId == projectId)
            );
        }

        IQueryable<ApmView> filteredQuery = GetFilteredQuery(baseQuery, command);

        (string Isotope, Expression<Func<ApmView, decimal?>>)[] isotopeSelectors =
        [
            (nameof(ApmBase.U234), pv => pv.U234),
            (nameof(ApmBase.U235), pv => pv.U235),
            (nameof(ApmBase.U236), pv => pv.U236),
            (nameof(ApmBase.U238), pv => pv.U238),
        ];

        return await GetUraniumBinCounts(
            filteredQuery,
            isotopeSelectors,
            command.CancellationToken
        );
    }

    private static async Task<ILookup<string, BinCount>> GetUraniumBinCounts<T>(
        IQueryable<T> baseQuery,
        IEnumerable<(string Isotope, Expression<Func<T, decimal?>> ValueSelector)> isotopeSelectors,
        CancellationToken ct
    )
        where T : class
    {
        var combinedQuery = isotopeSelectors
            .Select(selector =>
                baseQuery
                    .Select(selector.ValueSelector)
                    .Select(value => new { selector.Isotope, Value = value })
            )
            .Aggregate((current, next) => current.Concat(next))
            .Select(x => new
            {
                x.Isotope,
                Bin = x.Value == null ? "n.m."
                : x.Value < 1 ? "< 1"
                : x.Value >= 1 && x.Value < 2 ? "1-2"
                : x.Value >= 2 && x.Value < 3 ? "2-3"
                : x.Value >= 3 && x.Value < 4 ? "3-4"
                : x.Value >= 4 && x.Value < 5 ? "4-5"
                : x.Value >= 5 && x.Value < 6 ? "5-6"
                : x.Value >= 6 && x.Value < 7 ? "6-7"
                : x.Value >= 7 && x.Value < 8 ? "7-8"
                : x.Value >= 8 && x.Value < 9 ? "8-9"
                : "≥ 9",
            })
            .GroupBy(x => new { x.Isotope, x.Bin })
            .Select(g => new
            {
                g.Key.Isotope,
                Name = g.Key.Bin,
                Count = g.Count(),
            });

        var results = await combinedQuery.ToArrayAsync(ct);

        string[] binNames =
        [
            "n.m.",
            "< 1",
            "1-2",
            "2-3",
            "3-4",
            "4-5",
            "5-6",
            "6-7",
            "7-8",
            "8-9",
            "≥ 9",
        ];
        return results
            .GroupBy(x => x.Isotope)
            .OrderBy(group => group.Key)
            .SelectMany(group =>
            {
                Dictionary<string, int> counts = group.ToDictionary(x => x.Name, x => x.Count);
                return binNames.Select(name =>
                    (
                        Isotope: group.Key,
                        Bin: new BinCount { Name = name, Count = counts.GetValueOrDefault(name) }
                    )
                );
            })
            .ToLookup(x => x.Isotope, x => x.Bin);
    }

    async Task<DataQuery> CreateProjectChartQuery(int projectId)
    {
        bool useDecayCorrection = await _dbContext.Project.AnyAsync(x =>
            x.Id == projectId && x.DecayCorrectionDate.HasValue
        );

        return new DataQuery { ProjectId = projectId, DecayCorrected = useDecayCorrection };
    }
}
