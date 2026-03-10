#nullable enable
namespace NServiceBus.AzureFunctions.Analyzer.Utility;

using System.Text;
using Microsoft.CodeAnalysis.Text;

public sealed class SourceWriter
{
    const int CharsPerIndentation = 4;
    readonly StringBuilder builder = new();
    int indentation;

    public int Indentation
    {
        get => indentation;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            indentation = value;
        }
    }

    public void WriteLine()
    {
        builder.AppendLine();
    }

    public void WriteLine(string text)
    {
        if (indentation > 0)
        {
            builder.Append(' ', indentation * CharsPerIndentation);
        }

        builder.AppendLine(text);
    }

    public SourceText ToSourceText() => SourceText.From(builder.ToString().TrimEnd(), Encoding.UTF8);
}
