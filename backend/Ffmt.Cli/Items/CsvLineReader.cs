using System.Text;

namespace Ffmt.Cli.Items;

internal sealed class CsvLineReader(TextReader reader, char delimiter = ',')
{
    private readonly StringBuilder _field = new();
    private readonly List<string> _fields = [];

    public string[]? ReadRow()
    {
        _field.Clear();
        _fields.Clear();

        var inQuotes = false;
        var sawAnything = false;

        while (true)
        {
            var ch = reader.Read();
            if (ch == -1)
            {
                if (!sawAnything) return null;
                _fields.Add(_field.ToString());
                return _fields.ToArray();
            }

            sawAnything = true;
            var c = (char)ch;

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        _field.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    _field.Append(c);
                }
                continue;
            }

            if (c == '"' && _field.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                _fields.Add(_field.ToString());
                _field.Clear();
            }
            else if (c == '\r')
            {
                if (reader.Peek() == '\n') reader.Read();
                _fields.Add(_field.ToString());
                return _fields.ToArray();
            }
            else if (c == '\n')
            {
                _fields.Add(_field.ToString());
                return _fields.ToArray();
            }
            else
            {
                _field.Append(c);
            }
        }
    }
}
