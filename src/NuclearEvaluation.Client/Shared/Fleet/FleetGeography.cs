using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NuclearEvaluation.Client.Shared.Fleet;

internal sealed class FleetGeography
{
    public const int Width = 900;
    public const int Height = 535;
    const int Padding = 25;

    // Spherical Albers equal-area conic projection with the standard parallels used for the contiguous US.
    const double Radians = Math.PI / 180;
    static readonly double Northing = (Math.Sin(29.5 * Radians) + Math.Sin(45.5 * Radians)) / 2;
    static readonly double RadiusOffset =
        Math.Pow(Math.Cos(29.5 * Radians), 2) + 2 * Northing * Math.Sin(29.5 * Radians);

    readonly double _scale;
    readonly double _offsetX;
    readonly double _offsetY;

    public IReadOnlyList<StateOutline> States { get; }

    public FleetGeography(JsonElement geometry)
    {
        var states = geometry
            .GetProperty("features")
            .EnumerateArray()
            .Select(feature => new
            {
                Name = feature.TryGetProperty("properties", out JsonElement properties)
                && properties.TryGetProperty("name", out JsonElement name)
                    ? name.GetString()
                    : null,
                Rings = ReadRings(feature.GetProperty("geometry")),
            })
            .Where(state => state.Rings.Length > 0)
            .ToArray();
        Point[] points = states.SelectMany(state => state.Rings).SelectMany(ring => ring).ToArray();
        if (points.Length == 0)
            throw new JsonException("The state outline contains no contiguous US geometry.");

        double minX = points.Min(point => point.X);
        double maxX = points.Max(point => point.X);
        double minY = points.Min(point => point.Y);
        double maxY = points.Max(point => point.Y);
        _scale = Math.Min(
            (Width - 2 * Padding) / (maxX - minX),
            (Height - 2 * Padding) / (maxY - minY)
        );
        _offsetX = (Width - (minX + maxX) * _scale) / 2;
        _offsetY = (Height - (minY + maxY) * _scale) / 2;
        States = states
            .Select(state => new StateOutline(state.Name, BuildPath(state.Rings)))
            .ToArray();
    }

    public Point Locate(double longitude, double latitude) =>
        ToScreen(Project(longitude, latitude));

    static Point[][] ReadRings(JsonElement geometry)
    {
        JsonElement coordinates = geometry.GetProperty("coordinates");
        IEnumerable<JsonElement> polygons = geometry.GetProperty("type").GetString() switch
        {
            "Polygon" => [coordinates],
            "MultiPolygon" => coordinates.EnumerateArray(),
            _ => [],
        };
        return polygons
            .SelectMany(polygon => polygon.EnumerateArray())
            .Select(ring =>
                ring.EnumerateArray()
                    .Select(coordinate => new Point(
                        coordinate[0].GetDouble(),
                        coordinate[1].GetDouble()
                    ))
                    .ToArray()
            )
            .Where(ring => ring.Length >= 4 && ring.All(point => IsContiguous(point.X, point.Y)))
            .Select(ring => ring.Select(point => Project(point.X, point.Y)).ToArray())
            .ToArray();
    }

    public static bool IsContiguous(double longitude, double latitude) =>
        longitude is >= -126 and <= -65 && latitude is >= 24 and <= 50;

    static Point Project(double longitude, double latitude)
    {
        double theta = Northing * (longitude + 96) * Radians;
        double radius =
            Math.Sqrt(RadiusOffset - 2 * Northing * Math.Sin(latitude * Radians)) / Northing;
        return new Point(radius * Math.Sin(theta), radius * Math.Cos(theta));
    }

    Point ToScreen(Point point) => new(point.X * _scale + _offsetX, point.Y * _scale + _offsetY);

    string BuildPath(Point[][] rings)
    {
        StringBuilder path = new();
        foreach (Point[] ring in rings)
        {
            path.Append('M');
            foreach (Point point in ring)
            {
                Point screen = ToScreen(point);
                path.Append(CultureInfo.InvariantCulture, $"{screen.X:0.##},{screen.Y:0.##} ");
            }

            path.Append('Z');
        }

        return path.ToString();
    }

    internal readonly record struct Point(double X, double Y);

    internal sealed record StateOutline(string? Name, string Path);
}
