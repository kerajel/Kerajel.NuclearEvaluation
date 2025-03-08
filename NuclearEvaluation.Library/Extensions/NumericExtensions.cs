namespace NuclearEvaluation.Library.Extensions;

public static class NumericExtensions
{
    public static double AsMegabytes(this long value)
    {
        return value / 1048576.0;
    }
}