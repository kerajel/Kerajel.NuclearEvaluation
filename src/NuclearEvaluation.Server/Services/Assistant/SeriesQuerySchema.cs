using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Services.Assistant;

internal sealed record SeriesQueryField(
    PropertyInfo Property,
    IReadOnlyList<SeriesComparison> Operators
)
{
    public string Name => Property.Name;
    public Type ValueType => Property.PropertyType;
    public bool AllowsNull =>
        !ValueType.IsValueType || Nullable.GetUnderlyingType(ValueType) is not null;
}

internal static class SeriesQuerySchema
{
    static readonly SeriesComparison[] Equality =
    [
        SeriesComparison.Equal,
        SeriesComparison.NotEqual,
    ];
    static readonly SeriesComparison[] OrderedComparisons =
    [
        .. Equality,
        SeriesComparison.GreaterThan,
        SeriesComparison.GreaterOrEqual,
        SeriesComparison.LessThan,
        SeriesComparison.LessOrEqual,
    ];
    static readonly SeriesComparison[] StringComparisons =
    [
        .. Equality,
        SeriesComparison.Contains,
        SeriesComparison.StartsWith,
        SeriesComparison.EndsWith,
    ];
    static readonly IReadOnlyDictionary<
        SeriesQueryEntity,
        IReadOnlyList<SeriesQueryField>
    > FieldsByEntity = new Dictionary<SeriesQueryEntity, IReadOnlyList<SeriesQueryField>>
    {
        [SeriesQueryEntity.Series] = FindScalarFields<SeriesView>(),
        [SeriesQueryEntity.Samples] = FindScalarFields<SampleView>(),
        [SeriesQueryEntity.SubSamples] = FindScalarFields<SubSampleView>(),
        [SeriesQueryEntity.Apms] = FindScalarFields<ApmView>(),
        [SeriesQueryEntity.Particles] = FindScalarFields<ParticleView>(),
        [SeriesQueryEntity.Projects] = FindScalarFields<ProjectView>(),
    };

    public static IReadOnlyList<SeriesQueryField> GetFields(SeriesQueryEntity entity) =>
        FieldsByEntity.TryGetValue(entity, out IReadOnlyList<SeriesQueryField>? fields)
            ? fields
            : [];

    public static SeriesQueryField? FindField(SeriesQueryEntity entity, string name) =>
        FindField(GetFields(entity), name);

    internal static SeriesQueryField? FindField(IReadOnlyList<SeriesQueryField> fields, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 96)
            return null;

        string key = FieldKey(name);
        return fields.FirstOrDefault(field =>
            FieldKey(field.Name).Equals(key, StringComparison.OrdinalIgnoreCase)
        );
    }

    public static bool IsValid(SeriesGridQuery query) =>
        Validator.TryValidateObject(query, new ValidationContext(query), null, true)
        && AreFiltersValid(GetFields(SeriesQueryEntity.Series), query.Filters)
        && query.RelatedFilters is { Count: <= 6 }
        && query.RelatedFilters.All(filter =>
            filter is not null
            && filter.Count >= 0
            && OrderedComparisons.Contains(filter.Comparison)
            && IsRelationValid(filter.Rows)
        )
        && IsOrderValid(query);

    internal static bool TryReadValue(
        SeriesQueryField field,
        SeriesFieldFilter filter,
        out object? value
    )
    {
        value = null;
        if (!field.Operators.Contains(filter.Comparison))
            return false;

        if (filter.Comparison is SeriesComparison.IsNull or SeriesComparison.IsNotNull)
            return filter.Value is null;

        if (filter.Value is not { Length: <= 250 } text)
            return false;

        Type type = Nullable.GetUnderlyingType(field.ValueType) ?? field.ValueType;
        if (type.IsEnum)
        {
            return Enum.TryParse(type, text, true, out value)
                && value is not null
                && Enum.IsDefined(type, value);
        }

        if (type == typeof(DateOnly))
        {
            bool parsed = DateOnly.TryParseExact(
                text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly date
            );
            value = parsed ? date : null;
            return parsed;
        }

        value = Type.GetTypeCode(type) switch
        {
            TypeCode.String => text,
            TypeCode.Boolean when bool.TryParse(text, out bool boolean) => boolean,
            TypeCode.Byte
                when byte.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out byte number
                ) => number,
            TypeCode.Int16
                when short.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out short number
                ) => number,
            TypeCode.Int32
                when int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int number
                ) => number,
            TypeCode.Int64
                when long.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out long number
                ) => number,
            TypeCode.Decimal
                when decimal.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out decimal number
                ) => number,
            TypeCode.Single
                when float.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float number
                ) && float.IsFinite(number) => number,
            TypeCode.Double
                when double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double number
                ) && double.IsFinite(number) => number,
            TypeCode.DateTime
                when DateTime.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime date
                ) => date,
            _ => null,
        };
        return value is not null;
    }

    internal static bool AreFiltersValid(
        IReadOnlyList<SeriesQueryField> fields,
        IReadOnlyList<SeriesFieldFilter>? filters
    ) =>
        filters is { Count: <= 12 }
        && filters.All(filter =>
            filter is not null
            && FindField(fields, filter.Field) is { } field
            && TryReadValue(field, filter, out _)
        );

    static bool IsRelationValid(SeriesRelatedRows? relation) =>
        relation is not null
        && relation.Entity != SeriesQueryEntity.Series
        && FieldsByEntity.ContainsKey(relation.Entity)
        && AreFiltersValid(GetFields(relation.Entity), relation.Filters);

    static bool IsOrderValid(SeriesGridQuery query) =>
        query.Order is not { } order
        || (
            query.SortBy == SeriesGridSort.None
            && (
                order.Related is null
                    ? order.Field is not null
                        && FindField(SeriesQueryEntity.Series, order.Field) is not null
                    : order.Field is null && IsRelationValid(order.Related)
            )
        );

    internal static IReadOnlyList<SeriesQueryField> FindScalarFields<T>() =>
        typeof(T)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property =>
                property.GetMethod?.IsPublic == true
                && property.GetIndexParameters().Length == 0
                && !property.IsDefined(typeof(NotMappedAttribute), inherit: true)
            )
            .Select(property =>
                (Property: property, Operators: GetOperators(property.PropertyType))
            )
            .Where(item => item.Operators.Length != 0)
            .Select(item => new SeriesQueryField(item.Property, item.Operators))
            .ToArray();

    static SeriesComparison[] GetOperators(Type propertyType)
    {
        Type? nullableType = Nullable.GetUnderlyingType(propertyType);
        Type type = nullableType ?? propertyType;
        SeriesComparison[] operators = type.IsEnum
            ? Equality
            : Type.GetTypeCode(type) switch
            {
                TypeCode.String => StringComparisons,
                TypeCode.Boolean => Equality,
                TypeCode.Object when type == typeof(DateOnly) => OrderedComparisons,
                TypeCode.Byte
                or TypeCode.Int16
                or TypeCode.Int32
                or TypeCode.Int64
                or TypeCode.Decimal
                or TypeCode.Single
                or TypeCode.Double
                or TypeCode.DateTime => OrderedComparisons,
                _ => [],
            };
        return operators.Length != 0 && (nullableType is not null || type == typeof(string))
            ? [.. operators, SeriesComparison.IsNull, SeriesComparison.IsNotNull]
            : operators;
    }

    static string FieldKey(string name) =>
        string.Concat(name.Where(character => character != '_' && !char.IsWhiteSpace(character)));
}
