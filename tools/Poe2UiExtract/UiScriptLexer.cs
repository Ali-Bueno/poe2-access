using System.Collections.Generic;

internal enum TokenKind
{
    Word,
    String,
    Symbol,
    Eof,
}

internal readonly record struct Token(TokenKind Kind, string Text);

/// <summary>Tokenizes decoded UTF-16 .ui source text. A "word" is any run of letters, digits,
/// underscores or '$' (covers identifiers, type names, keywords and plain numbers alike, since the
/// parser only ever needs to recognize whole identifiers, not evaluate arithmetic expressions).
/// "//" starts a comment that runs to end of line, whether it is the whole line or trails code.</summary>
internal sealed class UiScriptLexer
{
    private readonly string _text;
    private int _pos;

    public UiScriptLexer(string text)
    {
        _text = text;
        _pos = 0;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            SkipWhitespaceAndComments();
            if (_pos >= _text.Length)
            {
                tokens.Add(new Token(TokenKind.Eof, ""));
                break;
            }

            char c = _text[_pos];
            if (c == '"')
            {
                tokens.Add(new Token(TokenKind.String, ReadString()));
            }
            else if (IsWordChar(c))
            {
                tokens.Add(new Token(TokenKind.Word, ReadWord()));
            }
            else
            {
                _pos++;
                tokens.Add(new Token(TokenKind.Symbol, c.ToString()));
            }
        }
        return tokens;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';

    private void SkipWhitespaceAndComments()
    {
        while (_pos < _text.Length)
        {
            char c = _text[_pos];
            if (char.IsWhiteSpace(c)) { _pos++; continue; }
            if (c == '/' && _pos + 1 < _text.Length && _text[_pos + 1] == '/')
            {
                while (_pos < _text.Length && _text[_pos] != '\n') _pos++;
                continue;
            }
            break;
        }
    }

    private string ReadWord()
    {
        int start = _pos;
        while (_pos < _text.Length && IsWordChar(_text[_pos])) _pos++;
        return _text[start.._pos];
    }

    private string ReadString()
    {
        _pos++; // opening quote
        int start = _pos;
        while (_pos < _text.Length && _text[_pos] != '"') _pos++;
        string s = _text[start.._pos];
        if (_pos < _text.Length) _pos++; // closing quote
        return s;
    }
}
