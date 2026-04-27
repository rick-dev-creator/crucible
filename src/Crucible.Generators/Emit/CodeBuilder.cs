using System.Text;

namespace Crucible.Generators.Emit;

internal sealed class CodeBuilder
{
    private readonly StringBuilder _sb = new();
    private int _indent;

    public CodeBuilder Line(string text = "")
    {
        if (text.Length == 0) { _sb.AppendLine(); return this; }
        _sb.Append(new string(' ', _indent * 4)).AppendLine(text);
        return this;
    }

    public System.IDisposable Block()
    {
        Line("{");
        _indent++;
        return new Closer(this);
    }

    public override string ToString() => _sb.ToString();

    private sealed class Closer : System.IDisposable
    {
        private readonly CodeBuilder _b;
        public Closer(CodeBuilder b) => _b = b;
        public void Dispose() { _b._indent--; _b.Line("}"); }
    }
}
