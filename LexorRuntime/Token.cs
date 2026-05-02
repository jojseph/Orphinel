namespace orphinel.LexorRuntime;

internal sealed class Token
{
    public Token(TokenType type, string value, int line, int column)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
    }

    public TokenType Type { get; set; }

    public string Value { get; set; }

    public int Line { get; set; }

    public int Column { get; set; }

    public override string ToString()
    {
        return $"Token(Type: {Type}, Value: '{Value}', Line: {Line}, Column: {Column})";
    }
}
