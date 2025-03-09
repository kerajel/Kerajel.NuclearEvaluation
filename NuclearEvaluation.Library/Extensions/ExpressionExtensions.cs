using System.Linq.Expressions;
using System.Reflection;

namespace NuclearEvaluation.Library.Extensions;

public static class ExpressionExtensions
{
    public static PropertyInfo GetPropertyInfo<T, TProperty>(this Expression<Func<T, TProperty>> propertyLambda)
    {
        if (propertyLambda.Body is not MemberExpression member)
            throw new ArgumentException($"Expression '{propertyLambda}' refers to a method, not a property");

        if (member.Member is not PropertyInfo propInfo)
            throw new ArgumentException($"Expression '{propertyLambda}' refers to a field, not a property");

        return propInfo;
    }

    public static string GetPropertyName<T, TProperty>(this Expression<Func<T, TProperty>> propertyLambda)
    {
        return propertyLambda.GetPropertyInfo().Name;
    }

    public static string GetMemberName<T>(this Expression<Func<T, object>> expression)
    {
        if (expression.Body is MemberExpression me)
        {
            return me.Member.Name;
        }
        if (expression.Body is UnaryExpression ue && ue.Operand is MemberExpression me2)
        {
            return me2.Member.Name;
        }
        throw new NotSupportedException("Only direct property expressions are supported");
    }
}