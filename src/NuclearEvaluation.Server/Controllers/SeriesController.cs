using Microsoft.AspNetCore.Mvc;
using NuclearEvaluation.Server.Interfaces.Data;
using NuclearEvaluation.Shared.Models.Domain;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Controllers;

[ApiController]
[Route("api/series")]
public class SeriesController : ControllerBase
{
    readonly ISeriesService _seriesService;

    public SeriesController(ISeriesService seriesService)
    {
        _seriesService = seriesService;
    }

    [HttpPost]
    public async Task<int> Create([FromBody] SeriesView seriesView)
    {
        Series series = await _seriesService.CreateSeriesFromView(seriesView);
        return series.Id;
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] SeriesView seriesView)
    {
        await _seriesService.UpdateSeriesFromView(seriesView);
        return Ok();
    }

    [HttpDelete]
    public async Task<IActionResult> Delete([FromBody] List<int> seriesIds)
    {
        SeriesView[] views = [.. seriesIds.Select(id => new SeriesView { Id = id })];
        await _seriesService.Delete(views);
        return Ok();
    }

    [HttpGet("{seriesId:int}/samples")]
    public async Task<List<SampleView>> GetSamples(int seriesId)
    {
        SeriesView seriesView = new() { Id = seriesId };
        await _seriesService.LoadSamples(seriesView);
        return [.. seriesView.Samples];
    }
}
