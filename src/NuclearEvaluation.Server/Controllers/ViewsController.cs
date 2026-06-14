using Microsoft.AspNetCore.Mvc;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Server.Interfaces.Data;
using NuclearEvaluation.Server.Interfaces.DB;
using NuclearEvaluation.Server.Interfaces.Evaluation;
using NuclearEvaluation.Server.Interfaces.STEM;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Controllers;

[ApiController]
[Route("api/views")]
public class ViewsController : ControllerBase
{
    readonly ISeriesService _seriesService;
    readonly ISampleService _sampleService;
    readonly ISubSampleService _subSampleService;
    readonly IApmService _apmService;
    readonly IParticleService _particleService;
    readonly IProjectService _projectService;
    readonly IStemPreviewEntryService _stemPreviewEntryService;
    readonly IGenericDbService _genericDbService;

    public ViewsController(
        ISeriesService seriesService,
        ISampleService sampleService,
        ISubSampleService subSampleService,
        IApmService apmService,
        IParticleService particleService,
        IProjectService projectService,
        IStemPreviewEntryService stemPreviewEntryService,
        IGenericDbService genericDbService)
    {
        _seriesService = seriesService;
        _sampleService = sampleService;
        _subSampleService = subSampleService;
        _apmService = apmService;
        _particleService = particleService;
        _projectService = projectService;
        _stemPreviewEntryService = stemPreviewEntryService;
        _genericDbService = genericDbService;
    }

    [HttpPost("series")]
    public async Task<DataResult<SeriesView>> Series([FromBody] DataQuery query, CancellationToken ct)
    {
        FetchDataCommand<SeriesView> command = query.ToCommand<SeriesView>();
        if (query.PriorityIds is { Count: > 0 } ids)
        {
            command.TopLevelOrderExpression = x => ids.Contains(x.Id) ? 0 : 1;
        }
        return (await _seriesService.GetSeriesViews(command)).ToDataResult();
    }

    [HttpPost("samples")]
    public async Task<DataResult<SampleView>> Samples([FromBody] DataQuery query, CancellationToken ct)
        => (await _sampleService.GetSampleViews(query.ToCommand<SampleView>())).ToDataResult();

    [HttpPost("subsamples")]
    public async Task<DataResult<SubSampleView>> SubSamples([FromBody] DataQuery query, CancellationToken ct)
        => (await _subSampleService.GetSubSampleViews(query.ToCommand<SubSampleView>())).ToDataResult();

    [HttpPost("apm")]
    public async Task<DataResult<ApmView>> Apm([FromBody] DataQuery query, CancellationToken ct)
        => (await _apmService.GetApmViews(query.ToCommand<ApmView>())).ToDataResult();

    [HttpPost("particles")]
    public async Task<DataResult<ParticleView>> Particles([FromBody] DataQuery query, CancellationToken ct)
        => (await _particleService.GetParticleViews(query.ToCommand<ParticleView>())).ToDataResult();

    [HttpPost("projects")]
    public async Task<DataResult<ProjectView>> Projects([FromBody] DataQuery query, CancellationToken ct)
    {
        FetchDataCommand<ProjectView> command = query.ToCommand<ProjectView>();
        command.Include(x => x.ProjectSeries);
        return (await _projectService.GetProjectViews(command)).ToDataResult();
    }

    [HttpPost("stem-entries")]
    public async Task<DataResult<StemPreviewEntryView>> StemEntries([FromBody] DataQuery query, CancellationToken ct)
    {
        if (query.StemSessionId is not Guid sessionId)
        {
            return DataResult<StemPreviewEntryView>.Succeeded([], 0);
        }
        return (await _stemPreviewEntryService.GetStemPreviewEntryViews(sessionId, query.ToCommand<StemPreviewEntryView>())).ToDataResult();
    }

    [HttpPost("series-counts")]
    public async Task<SeriesCountsView> SeriesCounts([FromBody] DataQuery query, CancellationToken ct)
        => await _seriesService.GetSeriesCounts(query.ToCommand<SeriesView>());

    [HttpPost("{entity}/enum-options")]
    public async Task<List<int>> EnumOptions(string entity, [FromBody] EnumFilterRequest request, CancellationToken ct)
    {
        FetchDataResult<int> result = entity.ToLowerInvariant() switch
        {
            "series" => await _genericDbService.GetFilterOptions(request.Query.ToCommand<SeriesView>(), request.PropertyName),
            "samples" => await _genericDbService.GetFilterOptions(request.Query.ToCommand<SampleView>(), request.PropertyName),
            "subsamples" => await _genericDbService.GetFilterOptions(request.Query.ToCommand<SubSampleView>(), request.PropertyName),
            "apm" => await _genericDbService.GetFilterOptions(request.Query.ToCommand<ApmView>(), request.PropertyName),
            "particles" => await _genericDbService.GetFilterOptions(request.Query.ToCommand<ParticleView>(), request.PropertyName),
            _ => throw new ArgumentOutOfRangeException(nameof(entity)),
        };

        return [.. result.Entries];
    }
}
