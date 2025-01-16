using Rustic.TabularDataReader.Interfaces;
using System.Text;

namespace Rustic.TabularDataReader.Services;

public class SpreadsheetReader : ISpreadsheetReader
{
    readonly HashSet<string> _supportedExtensions = ["xls", "xlsx", "xlsm", "xlsb", "xla", "xlam", "ods"];

    public bool CanHandle(string extension)
    {
        return _supportedExtensions.Contains(extension);
    }

    public string Read(byte[] bytea)
    {
        return Encoding.UTF8.GetString(bytea);
    }
}