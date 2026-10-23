using System.Collections.Generic;

/// <summary>
/// Parser for PoE2's UI DSL (<c>metadata/ui/**.ui</c>, UTF-16LE text). Full format description:
/// docs/research/ui-layouts.md. Grammar handled:
///   include "path"                              -- recorded implicitly, never followed.
///   const $x = ...;                              -- ignored.
///   &lt;DEFINITIONS&gt;                                 -- ignored marker.
///   begin &lt;Type&gt; [based on &lt;Base&gt;] ... end          -- a control; nested by containment.
///   template &lt;Type&gt; id = NAME; ... end             -- a reusable base; same block/nesting rules,
///                                                    but never emitted as a control itself.
/// Properties inside a block are "name = value;", possibly spanning multiple lines until the ';'.
/// Comments start with "//" and run to end of line (both whole-line and trailing same-line).
/// </summary>
internal sealed class UiScriptParser
{
    private readonly Dictionary<string, TemplateInfo> _templates;
    private readonly List<RawControl> _controls;
    private readonly Dictionary<string, Dictionary<string, RawControl>> _controlsByFile;

    private List<Token> _tokens = new();
    private int _pos;
    private string _screen = "";
    private readonly Stack<string> _idStack = new();
    private int _depth;

    public UiScriptParser(
        Dictionary<string, TemplateInfo> templates,
        List<RawControl> controls,
        Dictionary<string, Dictionary<string, RawControl>> controlsByFile)
    {
        _templates = templates;
        _controls = controls;
        _controlsByFile = controlsByFile;
    }

    public void ParseFile(string text, string screenRelPath)
    {
        _tokens = new UiScriptLexer(text).Tokenize();
        _pos = 0;
        _screen = screenRelPath;
        _idStack.Clear();
        _depth = 0;

        while (Peek().Kind != TokenKind.Eof)
        {
            var t = Peek();
            if (t.Kind == TokenKind.Word && t.Text == "include") { Next(); SkipInclude(); continue; }
            if (t.Kind == TokenKind.Word && t.Text == "const") { Next(); SkipToSemicolon(); continue; }
            if (t.Kind == TokenKind.Word && t.Text == "begin") { Next(); ParseBlock(isTemplate: false); continue; }
            if (t.Kind == TokenKind.Word && t.Text == "template") { Next(); ParseBlock(isTemplate: true); continue; }
            if (t.Kind == TokenKind.Symbol && t.Text == "<") { SkipAngleMarker(); continue; }
            if (t.Kind == TokenKind.Word && t.Text == "end") { Next(); continue; } // stray, defensive

            // Unrecognized top-level token: skip defensively so a surprise construct can't hang the parser.
            Next();
        }
    }

    private Token Peek() => _pos < _tokens.Count ? _tokens[_pos] : new Token(TokenKind.Eof, "");

    private Token Next() => _pos < _tokens.Count ? _tokens[_pos++] : new Token(TokenKind.Eof, "");

    private void SkipAngleMarker()
    {
        Next(); // consume '<'
        while (Peek().Kind != TokenKind.Eof && !(Peek().Kind == TokenKind.Symbol && Peek().Text == ">"))
            Next();
        if (Peek().Kind == TokenKind.Symbol && Peek().Text == ">") Next();
    }

    private void SkipInclude()
    {
        if (Peek().Kind == TokenKind.String) Next();
        if (Peek().Kind == TokenKind.Symbol && Peek().Text == ";") Next(); // defensive, not seen in samples
    }

    /// <summary>Consumes tokens up to and including the next top-level ';', treating (), [], {} as
    /// nesting so a ';' can never appear as a live top-level terminator while inside one of them
    /// (quoted strings are already single tokens, so a ';' inside a string never confuses this).</summary>
    private void SkipToSemicolon()
    {
        int depth = 0;
        while (Peek().Kind != TokenKind.Eof)
        {
            var t = Next();
            if (t.Kind != TokenKind.Symbol) continue;
            switch (t.Text)
            {
                case "(" or "[" or "{": depth++; break;
                case ")" or "]" or "}": depth--; break;
                case ";" when depth <= 0: return;
            }
        }
    }

    private void ParseBlock(bool isTemplate)
    {
        string className = Peek().Kind == TokenKind.Word ? Next().Text : "";

        string? baseName = null;
        if (Peek().Kind == TokenKind.Word && Peek().Text == "based")
        {
            Next();
            if (Peek().Kind == TokenKind.Word && Peek().Text == "on") Next();
            if (Peek().Kind == TokenKind.Word) baseName = Next().Text;
        }

        string? parentId = _idStack.Count > 0 ? _idStack.Peek() : null;
        int myDepth = _depth;
        _depth++;

        string? id = null;
        string? textKey = null, textLiteral = null, tipTextKey = null, tag = null;
        bool? focusable = null, keyboardEnabled = null;
        int? tabIndex = null;
        bool pushedId = false;

        while (true)
        {
            var t = Peek();
            if (t.Kind == TokenKind.Eof) break; // malformed input; stop rather than loop forever
            if (t.Kind == TokenKind.Word && t.Text == "end") { Next(); break; }
            if (t.Kind == TokenKind.Word && t.Text == "begin") { Next(); ParseBlock(isTemplate: false); continue; }
            if (t.Kind == TokenKind.Word && t.Text == "template") { Next(); ParseBlock(isTemplate: true); continue; }

            if (t.Kind != TokenKind.Word) { Next(); continue; } // stray symbol; defensive skip

            string propName = Next().Text;
            if (Peek().Kind == TokenKind.Symbol && Peek().Text == "=") Next();

            switch (propName)
            {
                case "id":
                    if (Peek().Kind == TokenKind.Word || Peek().Kind == TokenKind.String) id = Peek().Text;
                    SkipToSemicolon();
                    if (id != null && !pushedId) { _idStack.Push(id); pushedId = true; }
                    break;
                case "text":
                    ReadBraceOrStringValue(out textKey, out textLiteral);
                    break;
                case "tip_text":
                    ReadBraceOrStringValue(out tipTextKey, out _);
                    break;
                case "tag":
                    if (Peek().Kind == TokenKind.String) tag = Next().Text;
                    SkipToSemicolon();
                    break;
                case "tab_index":
                    if (Peek().Kind == TokenKind.Word && int.TryParse(Peek().Text, out int ti)) tabIndex = ti;
                    SkipToSemicolon();
                    break;
                case "can_focus_with_controller":
                    focusable = ReadBoolValue();
                    break;
                case "keyboard_enabled":
                    keyboardEnabled = ReadBoolValue();
                    break;
                default:
                    SkipToSemicolon();
                    break;
            }
        }

        _depth--;
        if (pushedId) _idStack.Pop();

        if (isTemplate)
        {
            // Templates are registered for "based on" resolution only; they are never controls.
            if (id != null)
                _templates[id] = new TemplateInfo { DeclaredType = className, BaseName = baseName };
            return;
        }

        if (id == null) return; // "Only emit blocks that have an id."

        var control = new RawControl
        {
            Id = id,
            ClassName = className,
            BaseName = baseName,
            Screen = _screen,
            ParentId = parentId,
            Depth = myDepth,
            TextKey = textKey,
            TextLiteral = textLiteral,
            TipTextKey = tipTextKey,
            Focusable = focusable,
            TabIndex = tabIndex,
            Tag = tag,
            KeyboardEnabled = keyboardEnabled,
        };
        _controls.Add(control);

        if (!_controlsByFile.TryGetValue(_screen, out var byId))
        {
            byId = new Dictionary<string, RawControl>();
            _controlsByFile[_screen] = byId;
        }
        byId[id] = control; // last declaration wins for a repeated id within the same file
    }

    private bool? ReadBoolValue()
    {
        bool? result = Peek() switch
        {
            { Kind: TokenKind.Word, Text: "true" } => true,
            { Kind: TokenKind.Word, Text: "false" } => false,
            _ => null,
        };
        SkipToSemicolon();
        return result;
    }

    /// <summary>Reads a "{ Key }" text-table reference (optionally followed by more tokens such as
    /// ", mouse" before the ';') into <paramref name="key"/>, or a "literal" string into
    /// <paramref name="literal"/>.</summary>
    private void ReadBraceOrStringValue(out string? key, out string? literal)
    {
        key = null;
        literal = null;
        if (Peek().Kind == TokenKind.Symbol && Peek().Text == "{")
        {
            Next();
            var parts = new List<string>();
            while (Peek().Kind != TokenKind.Eof && !(Peek().Kind == TokenKind.Symbol && Peek().Text == "}"))
                parts.Add(Next().Text);
            if (Peek().Kind == TokenKind.Symbol && Peek().Text == "}") Next();
            key = string.Concat(parts).Trim();
        }
        else if (Peek().Kind == TokenKind.String)
        {
            literal = Next().Text;
        }
        SkipToSemicolon();
    }
}
