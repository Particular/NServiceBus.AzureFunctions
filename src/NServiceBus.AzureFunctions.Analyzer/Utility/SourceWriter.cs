#nullable enable
namespace NServiceBus.AzureFunctions.Analyzer.Utility;

using System.Text;
using Microsoft.CodeAnalysis.Text;

public sealed class SourceWriter
{
    const int CharsPerIndentation = 4;
    readonly StringBuilder builder = new();

    public int Indentation
    {
        get;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            field = value;
        }
    }

    public void WriteLine()
    {
        builder.AppendLine();
    }

    public void WriteLine(string text)
    {
        if (Indentation > 0)
        {
            builder.Append(' ', Indentation * CharsPerIndentation);
        }

        builder.AppendLine(text);
    }

    public SourceText ToSourceText() => SourceText.From(builder.ToString().TrimEnd(), Encoding.UTF8);
}
