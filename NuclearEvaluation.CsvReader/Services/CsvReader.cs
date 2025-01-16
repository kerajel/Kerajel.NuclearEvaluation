using Rustic.TabularDataReader.Interfaces;
using System.Text;

namespace Rustic.TabularDataReader.Services;

public class CsvReader : ICsvReader
{
    readonly HashSet<string> _supportedExtensions = ["csv", "txt"];

    public bool CanHandle(string extension)
    {
        return _supportedExtensions.Contains(extension);
    }

    public string Read(byte[] bytea)
    {
        return Encoding.UTF8.GetString(bytea);
    }
}