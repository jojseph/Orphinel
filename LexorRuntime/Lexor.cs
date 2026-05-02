using System;
using System.Collections.Generic;

namespace orphinel.LexorRuntime;

internal sealed class Lexor
{
    private readonly string _source;
    private readonly List<Token> _tokens = new();
    private int _index;
    private int _line = 1;
    private int _column = 1;

    public Lexor(string source)
    {
        _source = source ?? string.Empty;
    }

    public List<Token> Tokenize()
    {
        while (!IsAtEnd())
        {
            SkipWhitespace();
            if (IsAtEnd())
            {
                break;
            }

            if (TryKeyword("SCRIPT AREA", TokenType.SCRIPT_AREA)) continue;
            if (TryKeyword("START SCRIPT", TokenType.START_SCRIPT)) continue;
            if (TryKeyword("END SCRIPT", TokenType.END_SCRIPT)) continue;
            if (TryKeyword("START IF", TokenType.START_IF)) continue;
            if (TryKeyword("END IF", TokenType.END_IF)) continue;
            if (TryKeyword("ELSE IF", TokenType.ELSE_IF)) continue;
            if (TryKeyword("START FOR", TokenType.START_FOR)) continue;
            if (TryKeyword("END FOR", TokenType.END_FOR)) continue;
            if (TryKeyword("REPEAT WHEN", TokenType.REPEAT_WHEN)) continue;
            if (TryKeyword("START REPEAT", TokenType.START_REPEAT)) continue;
            if (TryKeyword("END REPEAT", TokenType.END_REPEAT)) continue;

            if (TryKeyword("DECLARE", TokenType.DECLARE)) continue;
            if (TryKeyword("INT", TokenType.INT)) continue;
            if (TryKeyword("CHAR", TokenType.CHAR)) continue;
            if (TryKeyword("BOOL", TokenType.BOOL)) continue;
            if (TryKeyword("FLOAT", TokenType.FLOAT)) continue;
            if (TryKeyword("TRUE", TokenType.BOOLEAN_TRUE)) continue;
            if (TryKeyword("FALSE", TokenType.BOOLEAN_FALSE)) continue;

            if (TryKeyword("PRINT:", TokenType.PRINT, requiresBoundary: false)) continue;
            if (TryKeyword("PRINT", TokenType.PRINT)) continue;
            if (TryKeyword("SCAN:", TokenType.SCAN, requiresBoundary: false)) continue;
            if (TryKeyword("SCAN", TokenType.SCAN)) continue;
            if (TryKeyword("IF", TokenType.IF)) continue;
            if (TryKeyword("ELSE", TokenType.ELSE)) continue;
            if (TryKeyword("FOR", TokenType.FOR)) continue;
            if (TryKeyword("AND", TokenType.AND)) continue;
            if (TryKeyword("OR", TokenType.OR)) continue;
            if (TryKeyword("NOT", TokenType.NOT)) continue;

            int startLine = _line;
            int startColumn = _column;
            char current = Peek();

            if (current == '\'')
            {
                ReadChar(startLine, startColumn);
                continue;
            }

            if (current == '$')
            {
                Advance();
                _tokens.Add(new Token(TokenType.DOLLAR_SIGN, "$", startLine, startColumn));
                continue;
            }

            if (current == '&')
            {
                Advance();
                _tokens.Add(new Token(TokenType.AMPERSAND, "&", startLine, startColumn));
                continue;
            }

            if (current == ',')
            {
                Advance();
                _tokens.Add(new Token(TokenType.COMMA, ",", startLine, startColumn));
                continue;
            }

            if (current == ':')
            {
                Advance();
                _tokens.Add(new Token(TokenType.COLON, ":", startLine, startColumn));
                continue;
            }

            if (current == '[')
            {
                ReadEscapeCode(startLine, startColumn);
                continue;
            }

            if (current == '(')
            {
                Advance();
                _tokens.Add(new Token(TokenType.PARENTHESIS_OPEN, "(", startLine, startColumn));
                continue;
            }

            if (current == ')')
            {
                Advance();
                _tokens.Add(new Token(TokenType.PARENTHESIS_CLOSE, ")", startLine, startColumn));
                continue;
            }

            if (current == '*')
            {
                Advance();
                _tokens.Add(new Token(TokenType.MULTIPLY, "*", startLine, startColumn));
                continue;
            }

            if (current == '/')
            {
                Advance();
                _tokens.Add(new Token(TokenType.DIVIDE, "/", startLine, startColumn));
                continue;
            }

            if (current == '%')
            {
                Advance();
                if (!IsAtEnd() && Peek() == '%')
                {
                    Advance();
                    while (!IsAtEnd() && Peek() != '\n')
                    {
                        Advance();
                    }
                    continue;
                }

                _tokens.Add(new Token(TokenType.MODULO, "%", startLine, startColumn));
                continue;
            }

            if (current == '+')
            {
                Advance();
                _tokens.Add(new Token(TokenType.PLUS, "+", startLine, startColumn));
                continue;
            }

            if (current == '-')
            {
                Advance();
                _tokens.Add(new Token(TokenType.MINUS, "-", startLine, startColumn));
                continue;
            }

            if (current == '>')
            {
                Advance();
                if (!IsAtEnd() && Peek() == '=')
                {
                    Advance();
                    _tokens.Add(new Token(TokenType.GREATER_THAN_OR_EQUAL, ">=", startLine, startColumn));
                }
                else
                {
                    _tokens.Add(new Token(TokenType.GREATER_THAN, ">", startLine, startColumn));
                }
                continue;
            }

            if (current == '<')
            {
                Advance();
                if (!IsAtEnd() && Peek() == '=')
                {
                    Advance();
                    _tokens.Add(new Token(TokenType.LESS_THAN_OR_EQUAL, "<=", startLine, startColumn));
                }
                else if (!IsAtEnd() && Peek() == '>')
                {
                    Advance();
                    _tokens.Add(new Token(TokenType.NOT_EQUALS, "<>", startLine, startColumn));
                }
                else
                {
                    _tokens.Add(new Token(TokenType.LESS_THAN, "<", startLine, startColumn));
                }
                continue;
            }

            if (current == '=')
            {
                Advance();
                if (!IsAtEnd() && Peek() == '=')
                {
                    Advance();
                    _tokens.Add(new Token(TokenType.EQUALS, "==", startLine, startColumn));
                }
                else
                {
                    _tokens.Add(new Token(TokenType.ASSIGN, "=", startLine, startColumn));
                }
                continue;
            }

            if (current == '"')
            {
                ReadString(startLine, startColumn);
                continue;
            }

            if (char.IsDigit(current))
            {
                ReadNumber(startLine, startColumn);
                continue;
            }

            if (char.IsLetter(current) || current == '_')
            {
                ReadIdentifier(startLine, startColumn);
                continue;
            }

            throw new Exception($"Unexpected character '{current}' at line {startLine}, column {startColumn}");
        }

        _tokens.Add(new Token(TokenType.EOF, string.Empty, _line, _column));
        return _tokens;
    }

    private bool TryKeyword(string word, TokenType type, bool requiresBoundary = true)
    {
        if (!Matches(word))
        {
            return false;
        }

        int nextIndex = _index + word.Length;
        if (requiresBoundary && nextIndex < _source.Length)
        {
            char nextChar = _source[nextIndex];
            if (char.IsLetterOrDigit(nextChar) || nextChar == '_')
            {
                return false;
            }
        }

        Token token = new(type, word, _line, _column);
        for (int i = 0; i < word.Length; i++)
        {
            Advance();
        }

        _tokens.Add(token);
        return true;
    }

    private void ReadString(int startLine, int startColumn)
    {
        Advance();
        int start = _index;

        while (!IsAtEnd() && Peek() != '"')
        {
            Advance();
        }

        if (IsAtEnd())
        {
            throw new Exception($"Unterminated string at line {startLine}, column {startColumn}");
        }

        string value = _source[start.._index];
        Advance();
        _tokens.Add(new Token(TokenType.STRING, value, startLine, startColumn));
    }

    private void ReadNumber(int startLine, int startColumn)
    {
        int start = _index;
        while (!IsAtEnd() && char.IsDigit(Peek()))
        {
            Advance();
        }

        if (!IsAtEnd() && Peek() == '.' && CanBeFloat())
        {
            Advance();
            while (!IsAtEnd() && char.IsDigit(Peek()))
            {
                Advance();
            }
        }

        string value = _source[start.._index];
        _tokens.Add(new Token(TokenType.NUMBER, value, startLine, startColumn));
    }

    private void ReadIdentifier(int startLine, int startColumn)
    {
        int start = _index;
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            Advance();
        }

        string value = _source[start.._index];
        _tokens.Add(new Token(TokenType.IDENTIFIER, value, startLine, startColumn));
    }

    private void SkipWhitespace()
    {
        while (!IsAtEnd() && char.IsWhiteSpace(Peek()))
        {
            Advance();
        }
    }

    private bool Matches(string word)
    {
        if (_index + word.Length > _source.Length)
        {
            return false;
        }

        for (int i = 0; i < word.Length; i++)
        {
            if (_source[_index + i] != word[i])
            {
                return false;
            }
        }

        return true;
    }

    private bool CanBeFloat()
    {
        if (_index + 1 >= _source.Length)
        {
            return false;
        }

        return char.IsDigit(_source[_index + 1]);
    }

    private bool IsAtEnd() => _index >= _source.Length;

    private char Peek() => _source[_index];

    private void Advance()
    {
        if (IsAtEnd())
        {
            return;
        }

        if (_source[_index] == '\n')
        {
            _line++;
            _column = 1;
            _index++;
            return;
        }

        _index++;
        _column++;
    }

    private void ReadChar(int startLine, int startColumn)
    {
        Advance();
        int start = _index;

        while (!IsAtEnd() && Peek() != '\'')
        {
            Advance();
        }

        if (IsAtEnd())
        {
            throw new Exception($"Unterminated character literal at line {startLine}, column {startColumn}");
        }

        string value = _source[start.._index];
        if (value.Length != 1)
        {
            throw new Exception($"Character literal must contain exactly one character at line {startLine}, column {startColumn}");
        }

        Advance();
        _tokens.Add(new Token(TokenType.CHARACTER_LITERAL, value, startLine, startColumn));
    }

    private void ReadEscapeCode(int startLine, int startColumn)
    {
        Advance();
        int start = _index;

        if (!IsAtEnd() && Peek() == ']')
        {
            Advance();
            if (!IsAtEnd() && Peek() == ']')
            {
                string closingValue = _source[start.._index];
                Advance();
                _tokens.Add(new Token(TokenType.ESCAPE_CODE, closingValue, startLine, startColumn));
                return;
            }
        }

        while (!IsAtEnd() && Peek() != ']')
        {
            Advance();
        }

        if (IsAtEnd())
        {
            throw new Exception($"Unterminated escape code at line {startLine}, column {startColumn}");
        }

        string value = _source[start.._index];
        Advance();
        _tokens.Add(new Token(TokenType.ESCAPE_CODE, value, startLine, startColumn));
    }
}
