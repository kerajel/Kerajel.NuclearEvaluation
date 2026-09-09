using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;

namespace Kerajel.TabularDataReader;

/// <summary>Reads delimited text or a worksheet through the same streaming CSV interface.</summary>
public sealed class TabularDataReader : IDisposable
{
    readonly List<CsvReader> _readers = [];
    bool _disposed;

    static readonly HashSet<string> SpreadsheetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx",
        ".xlsb",
        ".xls",
    };

    static CsvConfiguration CreateCsvConfiguration() =>
        new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            DetectDelimiter = true,
            DetectDelimiterValues = [",", "\t", ";", "|"],
        };

    static TabularDataReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>The returned reader owns the input stream and is also disposed with this instance.</summary>
    public CsvReader GetCsvReader(Stream stream, string fileName, string? sheetName = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TextReader textReader = SpreadsheetExtensions.Contains(Path.GetExtension(fileName))
            ? new WorksheetTextReader(stream, sheetName)
            : new StreamReader(stream);
        CsvReader reader = new(textReader, CreateCsvConfiguration());
        _readers.Add(reader);
        return reader;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (CsvReader reader in _readers)
        {
            reader.Dispose();
        }
        _readers.Clear();
    }

    // Convert one row at a time on demand. A producer task and OS pipe are unnecessary:
    // they hide workbook errors and can block when the consumer stops reading early.
    sealed class WorksheetTextReader : TextReader
    {
        readonly IExcelDataReader _excel;
        readonly StringWriter _text = new(CultureInfo.InvariantCulture);
        readonly CsvWriter _csv;
        int _position;
        bool _disposed;

        public WorksheetTextReader(Stream stream, string? sheetName)
        {
            _excel = ExcelReaderFactory.CreateReader(stream);
            try
            {
                if (!string.IsNullOrEmpty(sheetName))
                {
                    while (_excel.Name != sheetName)
                    {
                        if (!_excel.NextResult())
                        {
                            throw new ArgumentException(
                                $"Sheet '{sheetName}' not found in Excel file",
                                nameof(sheetName)
                            );
                        }
                    }
                }
                _csv = new CsvWriter(_text, CultureInfo.InvariantCulture, leaveOpen: true);
            }
            catch
            {
                _excel.Dispose();
                _text.Dispose();
                throw;
            }
        }

        public override int Read(char[] buffer, int index, int count) =>
            Read(buffer.AsSpan(index, count));

        public override int Read(Span<char> buffer)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            int written = 0;
            StringBuilder row = _text.GetStringBuilder();
            while (written < buffer.Length)
            {
                if (_position == row.Length)
                {
                    if (!_excel.Read())
                        break;
                    row.Clear();
                    _position = 0;
                    for (int column = 0; column < _excel.FieldCount; column++)
                    {
                        _csv.WriteField(_excel.GetValue(column));
                    }
                    _csv.NextRecord();
                    _csv.Flush();
                }

                int count = Math.Min(row.Length - _position, buffer.Length - written);
                row.CopyTo(_position, buffer.Slice(written, count), count);
                _position += count;
                written += count;
            }
            return written;
        }

        public override Task<int> ReadAsync(char[] buffer, int index, int count) =>
            Task.FromResult(Read(buffer, index, count));

        public override ValueTask<int> ReadAsync(
            Memory<char> buffer,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _csv.Dispose();
                _text.Dispose();
                _excel.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
