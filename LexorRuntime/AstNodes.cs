using System.Collections.Generic;

namespace orphinel.LexorRuntime;

internal abstract record Statement;

internal abstract record Expression;

internal sealed record ScriptProgram(
    IReadOnlyList<DeclarationStatement> Declarations,
    IReadOnlyList<Statement> Statements);

internal sealed record DeclarationStatement(
    TokenType DataType,
    IReadOnlyList<VariableDeclaration> Variables) : Statement;

internal sealed record VariableDeclaration(string Name, Expression? Initializer);

internal sealed record PrintStatement(IReadOnlyList<PrintPart> Parts) : Statement;

internal abstract record PrintPart;

internal sealed record PrintTextPart(string Text) : PrintPart;

internal sealed record PrintExpressionPart(Expression Expression) : PrintPart;

internal sealed record ScanStatement(IReadOnlyList<string> VariableNames) : Statement;

internal sealed record AssignmentStatement(
    IReadOnlyList<string> VariableNames,
    Expression Value) : Statement;

internal sealed record IfBranch(Expression Condition, IReadOnlyList<Statement> Body);

internal sealed record IfStatement(
    IReadOnlyList<IfBranch> Branches,
    IReadOnlyList<Statement>? ElseBody) : Statement;

internal sealed record ForStatement(
    string VariableName,
    Expression Initializer,
    Expression Condition,
    AssignmentStatement Update,
    IReadOnlyList<Statement> Body) : Statement;

internal sealed record RepeatWhenStatement(
    Expression Condition,
    IReadOnlyList<Statement> Body) : Statement;

internal sealed record LiteralExpression(object? Value) : Expression;

internal sealed record VariableExpression(string Name) : Expression;

internal sealed record UnaryExpression(TokenType Operator, Expression Operand) : Expression;

internal sealed record BinaryExpression(
    Expression Left,
    TokenType Operator,
    Expression Right) : Expression;

internal sealed record GroupingExpression(Expression Inner) : Expression;
