using System;
using System.Collections.Generic;
using System.Globalization;

namespace orphinel.LexorRuntime;

internal sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _current;

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    public ScriptProgram ParseProgram()
    {
        Expect(TokenType.SCRIPT_AREA, "Expected SCRIPT AREA");
        Expect(TokenType.START_SCRIPT, "Expected START SCRIPT");

        List<DeclarationStatement> declarations = new();
        while (CheckToken(TokenType.DECLARE))
        {
            declarations.Add(ParseDeclareStatement());
        }

        List<Statement> statements = new();
        while (!CheckToken(TokenType.END_SCRIPT) && !IsAtEnd())
        {
            if (Match(TokenType.DOLLAR_SIGN))
            {
                continue;
            }

            statements.Add(ParseStatement());
        }

        Expect(TokenType.END_SCRIPT, "Expected END SCRIPT");
        Expect(TokenType.EOF, "Expected end of file");

        return new ScriptProgram(declarations, statements);
    }

    private Statement ParseStatement()
    {
        if (CheckToken(TokenType.DECLARE))
        {
            throw Error("Variable declarations must appear immediately after START SCRIPT");
        }

        if (Match(TokenType.PRINT))
        {
            return ParsePrintStatement();
        }

        if (Match(TokenType.SCAN))
        {
            return ParseScanStatement();
        }

        if (CheckToken(TokenType.IF))
        {
            return ParseIfStatement();
        }

        if (CheckToken(TokenType.FOR))
        {
            return ParseForStatement();
        }

        if (CheckToken(TokenType.REPEAT_WHEN))
        {
            return ParseRepeatWhenStatement();
        }

        if (CheckToken(TokenType.IDENTIFIER))
        {
            return ParseAssignStatement();
        }

        throw Error("Unexpected statement");
    }

    private PrintStatement ParsePrintStatement()
    {
        Match(TokenType.COLON);

        List<PrintPart> parts = new() { ParsePrintArgument() };
        while (Match(TokenType.AMPERSAND))
        {
            parts.Add(ParsePrintArgument());
        }

        return new PrintStatement(parts);
    }

    private PrintPart ParsePrintArgument()
    {
        if (Match(TokenType.STRING))
        {
            return new PrintTextPart(PreviousToken().Value);
        }

        if (Match(TokenType.DOLLAR_SIGN))
        {
            return new PrintTextPart("\n");
        }

        if (Match(TokenType.ESCAPE_CODE))
        {
            return new PrintTextPart(PreviousToken().Value);
        }

        if (Match(TokenType.CHARACTER_LITERAL))
        {
            return new PrintTextPart(PreviousToken().Value);
        }

        if (IsExpressionStart())
        {
            return new PrintExpressionPart(ParseExpression());
        }

        throw Error("Expected valid value after PRINT:");
    }

    private ScanStatement ParseScanStatement()
    {
        Match(TokenType.COLON);

        List<string> varNames = new();
        Token name = Expect(TokenType.IDENTIFIER, "Expected variable name after SCAN:");
        varNames.Add(name.Value);

        while (Match(TokenType.COMMA))
        {
            name = Expect(TokenType.IDENTIFIER, "Expected variable name after comma");
            varNames.Add(name.Value);
        }

        return new ScanStatement(varNames);
    }

    private DeclarationStatement ParseDeclareStatement()
    {
        Expect(TokenType.DECLARE, "Expected DECLARE");
        Token typeToken = Advance();

        if (typeToken.Type != TokenType.INT &&
            typeToken.Type != TokenType.CHAR &&
            typeToken.Type != TokenType.BOOL &&
            typeToken.Type != TokenType.FLOAT)
        {
            throw Error("Expected data type (INT, CHAR, BOOL, FLOAT)");
        }

        List<VariableDeclaration> variables = new()
        {
            ParseSingleDeclaration()
        };

        while (Match(TokenType.COMMA))
        {
            variables.Add(ParseSingleDeclaration());
        }

        return new DeclarationStatement(typeToken.Type, variables);
    }

    private VariableDeclaration ParseSingleDeclaration()
    {
        Token name = Expect(TokenType.IDENTIFIER, "Expected variable name");

        if (Match(TokenType.ASSIGN))
        {
            return new VariableDeclaration(name.Value, ParseExpression());
        }

        return new VariableDeclaration(name.Value, null);
    }

    private AssignmentStatement ParseAssignStatement()
    {
        Token firstName = Expect(TokenType.IDENTIFIER, "Expected variable name");
        Expect(TokenType.ASSIGN, "Expected '='");

        List<string> variableNames = new() { firstName.Value };
        while (CheckToken(TokenType.IDENTIFIER) && PeekNext()?.Type == TokenType.ASSIGN)
        {
            Token nextName = Advance();
            variableNames.Add(nextName.Value);
            Advance();
        }

        return new AssignmentStatement(variableNames, ParseExpression());
    }

    private IfStatement ParseIfStatement()
    {
        Expect(TokenType.IF, "Expected IF");

        List<IfBranch> branches = new()
        {
            ParseIfBranch()
        };

        while (Match(TokenType.ELSE_IF))
        {
            branches.Add(ParseIfBranch());
        }

        List<Statement>? elseBody = null;
        if (Match(TokenType.ELSE))
        {
            Expect(TokenType.START_IF, "Expected START IF after ELSE");
            elseBody = ParseBlockUntil(TokenType.END_IF);
            Expect(TokenType.END_IF, "Expected END IF");
        }

        return new IfStatement(branches, elseBody);
    }

    private IfBranch ParseIfBranch()
    {
        Expect(TokenType.PARENTHESIS_OPEN, "Expected '(' after conditional keyword");
        Expression condition = ParseExpression();
        Expect(TokenType.PARENTHESIS_CLOSE, "Expected ')' after condition");
        Expect(TokenType.START_IF, "Expected START IF");
        List<Statement> body = ParseBlockUntil(TokenType.END_IF);
        Expect(TokenType.END_IF, "Expected END IF");
        return new IfBranch(condition, body);
    }

    private ForStatement ParseForStatement()
    {
        Expect(TokenType.FOR, "Expected FOR");
        Expect(TokenType.PARENTHESIS_OPEN, "Expected '(' after FOR");

        Token initVar = Expect(TokenType.IDENTIFIER, "Expected loop variable");
        Expect(TokenType.ASSIGN, "Expected '=' in FOR initialization");
        Expression initializer = ParseExpression();

        Expect(TokenType.COMMA, "Expected ',' after initialization");
        Expression condition = ParseExpression();

        Expect(TokenType.COMMA, "Expected ',' after condition");
        AssignmentStatement update = ParseAssignStatement();

        Expect(TokenType.PARENTHESIS_CLOSE, "Expected ')' after FOR header");
        Expect(TokenType.START_FOR, "Expected START FOR");

        List<Statement> body = ParseBlockUntil(TokenType.END_FOR);
        Expect(TokenType.END_FOR, "Expected END FOR");

        return new ForStatement(initVar.Value, initializer, condition, update, body);
    }

    private RepeatWhenStatement ParseRepeatWhenStatement()
    {
        Expect(TokenType.REPEAT_WHEN, "Expected REPEAT WHEN");
        Expect(TokenType.PARENTHESIS_OPEN, "Expected '(' after REPEAT WHEN");
        Expression condition = ParseExpression();
        Expect(TokenType.PARENTHESIS_CLOSE, "Expected ')' after condition");
        Expect(TokenType.START_REPEAT, "Expected START REPEAT");

        List<Statement> body = ParseBlockUntil(TokenType.END_REPEAT);
        Expect(TokenType.END_REPEAT, "Expected END REPEAT");

        return new RepeatWhenStatement(condition, body);
    }

    private List<Statement> ParseBlockUntil(TokenType endToken)
    {
        List<Statement> statements = new();
        while (!CheckToken(endToken) && !IsAtEnd())
        {
            if (Match(TokenType.DOLLAR_SIGN))
            {
                continue;
            }

            statements.Add(ParseStatement());
        }

        return statements;
    }

    private Expression ParseExpression()
    {
        return ParseOr();
    }

    private Expression ParseOr()
    {
        Expression left = ParseAnd();
        while (Match(TokenType.OR))
        {
            left = new BinaryExpression(left, TokenType.OR, ParseAnd());
        }

        return left;
    }

    private Expression ParseAnd()
    {
        Expression left = ParseNot();
        while (Match(TokenType.AND))
        {
            left = new BinaryExpression(left, TokenType.AND, ParseNot());
        }

        return left;
    }

    private Expression ParseNot()
    {
        if (Match(TokenType.NOT))
        {
            return new UnaryExpression(TokenType.NOT, ParseNot());
        }

        return ParseEquality();
    }

    private Expression ParseEquality()
    {
        Expression left = ParseComparison();
        while (CheckToken(TokenType.EQUALS) || CheckToken(TokenType.NOT_EQUALS))
        {
            Token op = Advance();
            left = new BinaryExpression(left, op.Type, ParseComparison());
        }

        return left;
    }

    private Expression ParseComparison()
    {
        Expression left = ParseAddSub();
        while (CheckToken(TokenType.GREATER_THAN) || CheckToken(TokenType.LESS_THAN) ||
               CheckToken(TokenType.GREATER_THAN_OR_EQUAL) || CheckToken(TokenType.LESS_THAN_OR_EQUAL))
        {
            Token op = Advance();
            left = new BinaryExpression(left, op.Type, ParseAddSub());
        }

        return left;
    }

    private Expression ParseAddSub()
    {
        Expression left = ParseMulDivMod();
        while (CheckToken(TokenType.PLUS) || CheckToken(TokenType.MINUS))
        {
            Token op = Advance();
            left = new BinaryExpression(left, op.Type, ParseMulDivMod());
        }

        return left;
    }

    private Expression ParseMulDivMod()
    {
        Expression left = ParseUnary();
        while (CheckToken(TokenType.MULTIPLY) || CheckToken(TokenType.DIVIDE) || CheckToken(TokenType.MODULO))
        {
            Token op = Advance();
            left = new BinaryExpression(left, op.Type, ParseUnary());
        }

        return left;
    }

    private Expression ParseUnary()
    {
        if (Match(TokenType.MINUS))
        {
            return new UnaryExpression(TokenType.MINUS, ParseUnary());
        }

        if (Match(TokenType.PLUS))
        {
            return new UnaryExpression(TokenType.PLUS, ParseUnary());
        }

        return ParsePrimary();
    }

    private Expression ParsePrimary()
    {
        if (Match(TokenType.NUMBER))
        {
            string numStr = PreviousToken().Value;
            if (numStr.Contains('.'))
            {
                return new LiteralExpression(double.Parse(numStr, CultureInfo.InvariantCulture));
            }

            return new LiteralExpression(int.Parse(numStr, CultureInfo.InvariantCulture));
        }

        if (Match(TokenType.IDENTIFIER))
        {
            return new VariableExpression(PreviousToken().Value);
        }

        if (Match(TokenType.BOOLEAN_TRUE))
        {
            return new LiteralExpression(true);
        }

        if (Match(TokenType.BOOLEAN_FALSE))
        {
            return new LiteralExpression(false);
        }

        if (Match(TokenType.STRING))
        {
            return new LiteralExpression(PreviousToken().Value);
        }

        if (Match(TokenType.CHARACTER_LITERAL))
        {
            return new LiteralExpression(PreviousToken().Value);
        }

        if (Match(TokenType.PARENTHESIS_OPEN))
        {
            Expression result = ParseExpression();
            Expect(TokenType.PARENTHESIS_CLOSE, "Expected ')'");
            return new GroupingExpression(result);
        }

        throw Error($"Expected expression, got {Peek().Type}");
    }

    private bool IsExpressionStart()
    {
        return CheckToken(TokenType.NUMBER) ||
               CheckToken(TokenType.IDENTIFIER) ||
               CheckToken(TokenType.PARENTHESIS_OPEN) ||
               CheckToken(TokenType.MINUS) ||
               CheckToken(TokenType.PLUS) ||
               CheckToken(TokenType.NOT) ||
               CheckToken(TokenType.BOOLEAN_TRUE) ||
               CheckToken(TokenType.BOOLEAN_FALSE) ||
               CheckToken(TokenType.STRING) ||
               CheckToken(TokenType.CHARACTER_LITERAL);
    }

    private Token Expect(TokenType type, string message)
    {
        if (CheckToken(type))
        {
            return Advance();
        }

        throw Error(message);
    }

    private bool Match(TokenType type)
    {
        if (!CheckToken(type))
        {
            return false;
        }

        Advance();
        return true;
    }

    private bool CheckToken(TokenType type)
    {
        if (IsAtEnd())
        {
            return type == TokenType.EOF;
        }

        return Peek().Type == type;
    }

    private Token Advance()
    {
        if (!IsAtEnd())
        {
            _current++;
        }

        return PreviousToken();
    }

    private Token Peek()
    {
        return _tokens[_current];
    }

    private Token? PeekNext()
    {
        if (_current + 1 < _tokens.Count)
        {
            return _tokens[_current + 1];
        }

        return null;
    }

    private Token PreviousToken()
    {
        return _tokens[_current - 1];
    }

    private bool IsAtEnd()
    {
        return _tokens[_current].Type == TokenType.EOF;
    }

    private Exception Error(string message)
    {
        Token token = Peek();
        return new Exception($"{message} at line {token.Line}, column {token.Column}");
    }
}
