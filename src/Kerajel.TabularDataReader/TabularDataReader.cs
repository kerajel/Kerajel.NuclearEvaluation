using System.Globalization;
using System.IO.Pipes;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;

namespace Kerajel.TabularDataReader;

public class TabularDataReader : IDisposable
{
    private const int _batchSize = 10_000;
    private AnonymousPipeServerStream _pipeServer = null!;
    private AnonymousPipeClientStream _pipeClient = null!;
    private Task _writerTask = null!;
    private CsvReader _csvReader = null!;
    private StreamReader _streamReader = null!;
    private bool _disposed;

    static readonly HashSet<string> _supportedSpreadSheetFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx",
        ".xlsb",
        ".xls"
    };

    static readonly CsvConfiguration _csvConfig = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
    };

    static TabularDataReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public CsvReader GetCsvReader(Stream stream, string fileName, string? sheetName = default)
    {
        string fileExtension = Path.GetExtension(fileName);

        if (_supportedSpreadSheetFormats.Contains(fileExtension))
        {
            return FromSpreadsheet(stream, sheetName);
        }
        else
        {
            return FromPlainText(stream);
        }
    }

    private static CsvReader FromPlainText(Stream stream)
    {
        StreamReader plainTextReader = new(stream);
        CsvReader plainCsvReader = new(plainTextReader, _csvConfig);
        return plainCsvReader;
    }

    private CsvReader FromSpreadsheet(Stream stream, string? sheetName)
    {
        _pipeServer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
        _pipeClient = new AnonymousPipeClientStream(PipeDirection.In, _pipeServer.ClientSafePipeHandle);

        _writerTask = Task.Run(async () =>
        {
            using IExcelDataReader excelReader = ExcelReaderFactory.CreateReader(stream);
            using StreamWriter writer = new(_pipeServer);
            using CsvWriter csvWriter = new(writer, _csvConfig);

            ResolveTargetWorksheet(sheetName, excelReader);

            int recordCount = 0;
            while (excelReader.Read())
            {
                object[] values = new object[excelReader.FieldCount];
                int valueCount = excelReader.GetValues(values);
                for (int i = 0; i < valueCount; i++)
                {
                    csvWriter.WriteField(values[i]);
                }
                csvWriter.NextRecord();
                recordCount++;
                if (recordCount % _batchSize == 0)
                {
                    await writer.FlushAsync().ConfigureAwait(false);
                }
            }
            await writer.FlushAsync().ConfigureAwait(false);
        });

        _streamReader = new StreamReader(_pipeClient);
        _csvReader = new CsvReader(_streamReader, _csvConfig);
        return _csvReader;
    }

    private static void ResolveTargetWorksheet(string? sheetName, IExcelDataReader excelReader)
    {
        if (!string.IsNullOrEmpty(sheetName))
        {
            bool sheetFound = false;
            do
            {
                if (excelReader.Name == sheetName)
                {
                    sheetFound = true;
                    break;
                }
            }
            while (excelReader.NextResult());
            if (!sheetFound)
            {
                throw new ArgumentException($"Sheet '{sheetName}' not found in Excel file");
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                if (_writerTask != null)
                {
                    _writerTask.Wait(TimeSpan.FromSeconds(30));
                }
            }
            catch
            {
                
            }
            if (_csvReader != null)
            {
                _csvReader.Dispose();
            }
            if (_streamReader != null)
            {
                _streamReader.Dispose();
            }
            if (_pipeServer != null)
            {
                _pipeServer.Dispose();
            }
            if (_pipeClient != null)
            {
                _pipeClient.Dispose();
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}