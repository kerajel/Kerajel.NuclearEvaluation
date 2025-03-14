using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Plotting;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Server.Data;
using NuclearEvaluation.SharedServices.Services;
using System.Linq.Expressions;

namespace NuclearEvaluation.Server.Services;

public class ChartService : DbServiceBase, IChartService
{
    public ChartService(NuclearEvaluationServerDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<ILookup<string, BinCount>> GetProjectParticleUraniumBinCounts(int projectId)
    {
        bool useDecayCorrection = await _dbContext.Project
            .AnyAsync(x => x.Id == projectId && x.DecayCorrectionDate.HasValue);

        IQueryable<ParticleView> baseQuery = useDecayCorrection
            ? _dbContext.ProjectDecayCorrectedParticleView.Where(x => x.ProjectId == projectId)
            : _dbContext.ParticleView.Where(pv => pv.SubSample.Sample.Series.ProjectSeries.Any(y => y.ProjectId == projectId));

        (string Isotope, Expression<Func<ParticleView, decimal?>>)[] isotopeSelectors =
        [
            (nameof(ParticleBase.U234), pv => pv.U234),
            (nameof(ParticleBase.U235), pv => pv.U235),
        ];

        return await GetUraniumBinCounts(baseQuery, isotopeSelectors);
    }

    public async Task<ILookup<string, BinCount>> GetProjectApmUraniumBinCounts(int projectId)
    {
        bool useDecayCorrection = await _dbContext.Project
            .AnyAsync(x => x.Id == projectId && x.DecayCorrectionDate.HasValue);

        IQueryable<ApmView> baseQuery = useDecayCorrection
            ? _dbContext.ProjectDecayCorrectedApmView.Where(x => x.ProjectId == projectId)
            : _dbContext.ApmView.Where(pv => pv.SubSample.Sample.Series.ProjectSeries.Any(y => y.ProjectId == projectId));

        (string Isotope, Expression<Func<ApmView, decimal?>>)[] isotopeSelectors =
        [
            (nameof(ApmBase.U234), pv => pv.U234),
            (nameof(ApmBase.U235), pv => pv.U235),
            (nameof(ApmBase.U236), pv => pv.U236),
            (nameof(ApmBase.U238), pv => pv.U238),
        ];

        return await GetUraniumBinCounts(baseQuery, isotopeSelectors);
    }

    private static async Task<ILookup<string, BinCount>> GetUraniumBinCounts<T>(
        IQueryable<T> baseQuery,
        IEnumerable<(string Isotope, Expression<Func<T, decimal?>> ValueSelector)> isotopeSelectors)
    {
        var combinedQuery = isotopeSelectors
            .Select(selector => baseQuery.Select(selector.ValueSelector).Select(value => new { selector.Isotope, Value = value }))
            .Aggregate((current, next) => current.Concat(next))
            .Select(x => new
            {
                x.Isotope,
                Bin = x.Value == null ? "n.m." :
                      (x.Value < 1) ? "< 1" :
                      (x.Value >= 1 && x.Value < 2) ? "1-2" :
                      (x.Value >= 2 && x.Value < 3) ? "2-3" :
                      (x.Value >= 3 && x.Value < 4) ? "3-4" :
                      (x.Value >= 4 && x.Value < 5) ? "4-5" :
                      (x.Value >= 5 && x.Value < 6) ? "5-6" :
                      (x.Value >= 6 && x.Value < 7) ? "6-7" :
                      (x.Value >= 7 && x.Value < 8) ? "7-8" :
                      (x.Value >= 8 && x.Value < 9) ? "8-9" :
                      "> 9",
            })
            .GroupBy(x => new { x.Isotope, x.Bin })
            .Select(g => new
            {
                Isotope = g.Key.Isotope,
                Name = g.Key.Bin,
                Count = g.Count(),
            });

        var results = await combinedQuery.ToArrayAsync();

        ILookup<string, BinCount> groupedResults = results
            .OrderBy(x => x.Isotope)
            .ThenBy(x => x.Name switch
            {
                "n.m." => 0,
                "< 1" => 1,
                "> 9" => 10,
                _ => int.Parse(x.Name.Split('-')[0])
            })
            .ToLookup(
                x => x.Isotope,
                x => new BinCount { Name = x.Name, Count = x.Count, }
            );

        return groupedResults;
    }
}