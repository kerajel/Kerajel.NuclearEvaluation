namespace NuclearEvaluation.Library.Interfaces;

public interface ISessionCache
{
    void Add<T>(string key, T? value);
    T? GetOrDefault<T>(string key);
    bool TryGetValue<T>(string key, out T? value);
}