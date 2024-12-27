using NuclearEvaluation.Library.Models.Filters;
using FluentValidation;
using NuclearEvaluation.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace NuclearEvaluation.Server.Validators;

public class PresetFilterValidator : AbstractValidator<PresetFilter>
{
    private readonly IDbContextFactory<NuclearEvaluationServerDbContext> _dbContextFactory;

    public PresetFilterValidator(IDbContextFactory<NuclearEvaluationServerDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;

        int minLength = 5;
        int maxLength = 25;

        RuleFor(x => x.Name).Must((name) =>
        {
            return !string.IsNullOrWhiteSpace(name) && name.Length >= minLength && name.Length <= maxLength;
        }).WithMessage($"Name must be between {minLength} and {maxLength} characters long");

        RuleFor(x => x.Name).MustAsync(async (filter, name, ct) =>
        {
            using NuclearEvaluationServerDbContext dbContext = _dbContextFactory.CreateDbContext();
            bool exists = await dbContext.PresetFilter.AnyAsync(d => d.Name == filter.Name && d.Id != filter.Id, ct);
            return !exists;
        }).WithMessage("Name is already in use");
        this._dbContextFactory = dbContextFactory;
    }
}