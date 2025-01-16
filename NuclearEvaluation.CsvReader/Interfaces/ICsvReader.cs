namespace Rustic.TabularDataReader.Interfaces
{
    public interface ICsvReader
    {
        bool CanHandle(string extension);
        string Read(byte[] bytea);
    }
}