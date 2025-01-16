namespace Rustic.TabularDataReader.Interfaces
{
    public interface ISpreadsheetReader
    {
        bool CanHandle(string extension);
        string Read(byte[] bytea);
    }
}