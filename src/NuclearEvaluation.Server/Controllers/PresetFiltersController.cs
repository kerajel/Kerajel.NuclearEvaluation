using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Shared.Models.Filters;
using Z.EntityFramework.Plus;

namespace NuclearEvaluation.Server.Controllers;

[ApiController]
[Route("api/preset-filters")]
public class PresetFiltersController : ControllerBase
{
    readonly NuclearEvaluationServerDbContext _dbContext;

    public PresetFiltersController(NuclearEvaluationServerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<List<PresetFilter>> GetAll()
    {
        return await _dbContext.PresetFilter
            .AsNoTracking()
            .Include(x => x.Entries)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<int> Create([FromBody] PresetFilter filter)
    {
        filter.Id = 0;
        foreach (PresetFilterEntry entry in filter.Entries)
        {
            entry.Id = 0;
            entry.PresetFilter = filter;
        }
        _dbContext.Add(filter);
        await _dbContext.SaveChangesAsync();
        return filter.Id;
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] PresetFilter filter)
    {
        PresetFilter? existing = await _dbContext.PresetFilter
            .Include(x => x.Entries)
            .SingleOrDefaultAsync(x => x.Id == filter.Id);

        if (existing is null)
        {
            return NotFound();
        }

        existing.Name = filter.Name;
        _dbContext.PresetFilterEntry.RemoveRange(existing.Entries);
        existing.Entries = filter.Entries.Select(e =>
        {
            e.Id = 0;
            e.PresetFilterId = existing.Id;
            e.PresetFilter = existing;
            return e;
        }).ToList();

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _dbContext.PresetFilter.Where(x => x.Id == id).DeleteFromQueryAsync();
        return Ok();
    }

    [HttpGet("name-available")]
    public async Task<bool> NameAvailable([FromQuery] string name, [FromQuery] int excludeId = 0)
    {
        bool exists = await _dbContext.PresetFilter.AnyAsync(x => x.Name == name && x.Id != excludeId);
        return !exists;
    }
}
