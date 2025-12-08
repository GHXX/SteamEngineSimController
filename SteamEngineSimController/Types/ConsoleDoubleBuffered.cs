namespace SteamEngineSimController.Types;

internal class ConsoleDoubleBuffered {
    private readonly List<char> buffer = [];

    private int prevLineCount = 0;

    public bool DisableBuffering { get; set; }

    public void ShowBuffer() {
        Console.SetCursorPosition(0, 0);
        var charsPerLine = Console.BufferWidth;
        var lines = string.Join("", this.buffer).Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var currLineCount = lines.Length;
        var linesToAddAsPadding = Math.Max(0, this.prevLineCount - currLineCount);
        if (linesToAddAsPadding > 0) {
            lines = lines.Concat(Enumerable.Repeat("", linesToAddAsPadding)).ToArray();
        }

        var fullLines = lines.Select(l => l.PadRight(charsPerLine)).ToArray();

        Console.Out.Write(fullLines.Aggregate((a, b) => a + b));
        this.buffer.Clear();
        this.prevLineCount = currLineCount;
    }

    public void WriteLine(string s) => Write(s + "\n");
    public void Write(string s) {
        if (DisableBuffering)
            Console.Write(s);
        else
            this.buffer.AddRange(s);
    }
}