﻿using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Models.Filters;
using NuclearEvaluation.Kernel.Data.Context;

namespace NuclearEvaluation.Shared.Validators;

public class PresetFilterValidator : AbstractValidator<PresetFilter>
{
    private readonly IDbContextFactory<NuclearEvaluationServerDbContext> _dbContextFactory;

    public PresetFilterValidator(IDbContextFactory<NuclearEvaluationServerDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;

        int minLength = 5;
        int maxLength = 25;

        RuleFor(x => x.Name).Must((value) =>
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length >= minLength && value.Length <= maxLength;
        }).WithMessage($"Name must be between {minLength} and {maxLength} characters long");

        RuleFor(x => x.Name).MustAsync(async (filter, value, ct) =>
        {
            using NuclearEvaluationServerDbContext dbContext = _dbContextFactory.CreateDbContext();
            bool exists = await dbContext.PresetFilter.AnyAsync(d => d.Name == filter.Name && d.Id != filter.Id, ct);
            return !exists;
        }).WithMessage("Name is already in use");
        _dbContextFactory = dbContextFactory;
    }
}