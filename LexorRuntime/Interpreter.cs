using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace orphinel.LexorRuntime;

internal sealed class Interpreter
{
    private readonly Dictionary<string, (TokenType Type, object Value)> _variables = new();
    private readonly Func<string, Task> _outputWriter;
    private readonly Func<IReadOnlyList<string>, Task<string>> _inputReader;

    public Interpreter(
        Func<string, Task>? outputWriter = null,
        Func<IReadOnlyList<string>, Task<string>>? inputReader = null)
    {
        _outputWriter = outputWriter ?? (_ => Task.CompletedTask);
        _inputReader = inputReader ?? (_ => Task.FromException<string>(new Exception("SCAN input is not available.")));
    }

    public IReadOnlyDictionary<string, LexorVariableSnapshot> Variables =>
        _variables.ToDictionary(
            pair => pair.Key,
            pair => new LexorVariableSnapshot(pair.Value.Type.ToString(), ValueToString(pair.Value.Value)));

    public async Task ExecuteAsync(ScriptProgram program)
    {
        foreach (DeclarationStatement declaration in program.Declarations)
        {
            await ExecuteStatementAsync(declaration);
        }

        foreach (Statement statement in program.Statements)
        {
            await ExecuteStatementAsync(statement);
        }
    }

    public void DeclareVariable(string name, TokenType dataType, object? initialValue)
    {
        if (_variables.ContainsKey(name))
        {
            throw new Exception($"Variable '{name}' is already declared.");
        }

        object defaultVal = dataType switch
        {
            TokenType.INT => 0,
            TokenType.FLOAT => 0.0,
            TokenType.CHAR => "",
            TokenType.BOOL => false,
            _ => throw new Exception($"Unknown data type: {dataType}")
        };

        _variables[name] = (dataType, initialValue != null
            ? CoerceValue(dataType, initialValue)
            : defaultVal);
    }

    public void AssignVariable(string name, object value)
    {
        if (!_variables.ContainsKey(name))
        {
            throw new Exception($"Variable '{name}' is not declared.");
        }

        var (type, _) = _variables[name];
        _variables[name] = (type, CoerceValue(type, value));
    }

    public object GetVariable(string name)
    {
        if (!_variables.ContainsKey(name))
        {
            throw new Exception($"Variable '{name}' is not declared.");
        }

        return _variables[name].Value;
    }

    public string ValueToString(object value)
    {
        if (value is bool b)
        {
            return b ? "TRUE" : "FALSE";
        }

        if (value is int i)
        {
            return i.ToString(CultureInfo.InvariantCulture);
        }

        if (value is float f)
        {
            return f.ToString(CultureInfo.InvariantCulture);
        }

        if (value is double d)
        {
            if (d == Math.Floor(d) && !double.IsInfinity(d))
            {
                return ((int)d).ToString(CultureInfo.InvariantCulture);
            }

            return d.ToString(CultureInfo.InvariantCulture);
        }

        return value?.ToString() ?? string.Empty;
    }

    public async Task ScanAsync(List<string> variableNames)
    {
        string input = await _inputReader(variableNames);
        string[] values = input.Split(',');

        if (values.Length < variableNames.Count)
        {
            throw new Exception($"SCAN expected {variableNames.Count} value(s) but got {values.Length}.");
        }

        for (int i = 0; i < variableNames.Count; i++)
        {
            string varName = variableNames[i];
            string rawVal = values[i].Trim();

            if (!_variables.ContainsKey(varName))
            {
                throw new Exception($"Variable '{varName}' is not declared.");
            }

            var (type, _) = _variables[varName];

            switch (type)
            {
                case TokenType.INT:
                    if (!int.TryParse(rawVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intVal))
                    {
                        throw new Exception($"Cannot parse '{rawVal}' as INT for variable '{varName}'.");
                    }

                    _variables[varName] = (type, intVal);
                    break;

                case TokenType.FLOAT:
                    if (!float.TryParse(rawVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                    {
                        throw new Exception($"Cannot parse '{rawVal}' as FLOAT for variable '{varName}'.");
                    }

                    _variables[varName] = (type, floatVal);
                    break;

                case TokenType.CHAR:
                    if (rawVal.Length != 1)
                    {
                        throw new Exception($"CHAR input for variable '{varName}' must contain exactly one character.");
                    }

                    _variables[varName] = (type, rawVal);
                    break;

                case TokenType.BOOL:
                    bool boolVal = rawVal.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                    _variables[varName] = (type, boolVal);
                    break;
            }
        }
    }

    public bool AreEqual(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (TryGetNumericValue(left, out double leftNumber) && TryGetNumericValue(right, out double rightNumber))
        {
            return leftNumber == rightNumber;
        }

        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool == rightBool;
        }

        if (left is string leftString && right is string rightString)
        {
            return string.Equals(leftString, rightString, StringComparison.Ordinal);
        }

        return Equals(left, right);
    }

    public double ToNumber(object value)
    {
        if (value is int i) return i;
        if (value is float f) return f;
        if (value is double d) return d;
        if (value is bool b) return b ? 1 : 0;
        if (value is string s)
        {
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }

            throw new Exception($"Cannot convert '{s}' to a number.");
        }

        throw new Exception("Cannot convert value to number.");
    }

    public bool IsTrue(object value)
    {
        if (value is bool b) return b;
        if (value is int i) return i != 0;
        if (value is float f) return f != 0;
        if (value is double d) return d != 0;
        if (value is string s)
        {
            if (s.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return false;
            return !string.IsNullOrEmpty(s);
        }

        return false;
    }

    private object CoerceValue(TokenType targetType, object value)
    {
        return targetType switch
        {
            TokenType.INT => (int)ToNumber(value),
            TokenType.FLOAT => (float)ToNumber(value),
            TokenType.CHAR => CoerceCharValue(value),
            TokenType.BOOL => IsTrue(value),
            _ => value
        };
    }

    private static string CoerceCharValue(object value)
    {
        string charValue = value switch
        {
            string s => s,
            char c => c.ToString(),
            _ => value.ToString() ?? string.Empty
        };

        if (charValue.Length != 1)
        {
            throw new Exception("CHAR value must contain exactly one character.");
        }

        return charValue;
    }

    private async Task ExecuteStatementAsync(Statement statement)
    {
        switch (statement)
        {
            case DeclarationStatement declaration:
                ExecuteDeclaration(declaration);
                break;
            case PrintStatement printStatement:
                await ExecutePrintAsync(printStatement);
                break;
            case ScanStatement scanStatement:
                await ScanAsync(new List<string>(scanStatement.VariableNames));
                break;
            case AssignmentStatement assignmentStatement:
                ExecuteAssignment(assignmentStatement);
                break;
            case IfStatement ifStatement:
                await ExecuteIfAsync(ifStatement);
                break;
            case ForStatement forStatement:
                await ExecuteForAsync(forStatement);
                break;
            case RepeatWhenStatement repeatWhenStatement:
                await ExecuteRepeatWhenAsync(repeatWhenStatement);
                break;
            default:
                throw new Exception($"Unsupported statement type: {statement.GetType().Name}");
        }
    }

    private void ExecuteDeclaration(DeclarationStatement declaration)
    {
        foreach (VariableDeclaration variable in declaration.Variables)
        {
            object? initialValue = variable.Initializer is null
                ? null
                : EvaluateExpression(variable.Initializer);

            DeclareVariable(variable.Name, declaration.DataType, initialValue);
        }
    }

    private async Task ExecutePrintAsync(PrintStatement statement)
    {
        StringBuilder output = new();
        foreach (PrintPart part in statement.Parts)
        {
            switch (part)
            {
                case PrintTextPart textPart:
                    output.Append(textPart.Text);
                    break;
                case PrintExpressionPart expressionPart:
                    output.Append(ValueToString(EvaluateExpression(expressionPart.Expression)));
                    break;
                default:
                    throw new Exception($"Unsupported print part type: {part.GetType().Name}");
            }
        }

        await _outputWriter(output.ToString());
    }

    private void ExecuteAssignment(AssignmentStatement statement)
    {
        object value = EvaluateExpression(statement.Value);
        foreach (string variableName in statement.VariableNames)
        {
            AssignVariable(variableName, value);
        }
    }

    private async Task ExecuteIfAsync(IfStatement statement)
    {
        foreach (IfBranch branch in statement.Branches)
        {
            if (IsTrue(EvaluateExpression(branch.Condition)))
            {
                await ExecuteBlockAsync(branch.Body);
                return;
            }
        }

        if (statement.ElseBody is not null)
        {
            await ExecuteBlockAsync(statement.ElseBody);
        }
    }

    private async Task ExecuteForAsync(ForStatement statement)
    {
        AssignVariable(statement.VariableName, EvaluateExpression(statement.Initializer));

        while (IsTrue(EvaluateExpression(statement.Condition)))
        {
            await ExecuteBlockAsync(statement.Body);
            ExecuteAssignment(statement.Update);
        }
    }

    private async Task ExecuteRepeatWhenAsync(RepeatWhenStatement statement)
    {
        while (IsTrue(EvaluateExpression(statement.Condition)))
        {
            await ExecuteBlockAsync(statement.Body);
        }
    }

    private async Task ExecuteBlockAsync(IReadOnlyList<Statement> statements)
    {
        foreach (Statement statement in statements)
        {
            await ExecuteStatementAsync(statement);
        }
    }

    private object EvaluateExpression(Expression expression)
    {
        return expression switch
        {
            LiteralExpression literal => literal.Value ?? string.Empty,
            VariableExpression variable => GetVariable(variable.Name),
            GroupingExpression grouping => EvaluateExpression(grouping.Inner),
            UnaryExpression unary => EvaluateUnary(unary),
            BinaryExpression binary => EvaluateBinary(binary),
            _ => throw new Exception($"Unsupported expression type: {expression.GetType().Name}")
        };
    }

    private object EvaluateUnary(UnaryExpression expression)
    {
        object operand = EvaluateExpression(expression.Operand);
        return expression.Operator switch
        {
            TokenType.NOT => !IsTrue(operand),
            TokenType.MINUS => -ToNumber(operand),
            TokenType.PLUS => ToNumber(operand),
            _ => throw new Exception($"Unsupported unary operator: {expression.Operator}")
        };
    }

    private object EvaluateBinary(BinaryExpression expression)
    {
        object left = EvaluateExpression(expression.Left);

        if (expression.Operator == TokenType.OR)
        {
            return IsTrue(left) || IsTrue(EvaluateExpression(expression.Right));
        }

        if (expression.Operator == TokenType.AND)
        {
            return IsTrue(left) && IsTrue(EvaluateExpression(expression.Right));
        }

        object right = EvaluateExpression(expression.Right);
        double leftNumber;
        double rightNumber;

        return expression.Operator switch
        {
            TokenType.EQUALS => AreEqual(left, right),
            TokenType.NOT_EQUALS => !AreEqual(left, right),
            TokenType.GREATER_THAN => ToNumber(left) > ToNumber(right),
            TokenType.LESS_THAN => ToNumber(left) < ToNumber(right),
            TokenType.GREATER_THAN_OR_EQUAL => ToNumber(left) >= ToNumber(right),
            TokenType.LESS_THAN_OR_EQUAL => ToNumber(left) <= ToNumber(right),
            TokenType.PLUS => ToNumber(left) + ToNumber(right),
            TokenType.MINUS => ToNumber(left) - ToNumber(right),
            TokenType.MULTIPLY => ToNumber(left) * ToNumber(right),
            TokenType.DIVIDE => (rightNumber = ToNumber(right)) != 0
                ? ToNumber(left) / rightNumber
                : throw new Exception("Division by zero"),
            TokenType.MODULO => (leftNumber = ToNumber(left)) % (rightNumber = ToNumber(right)),
            _ => throw new Exception($"Unsupported binary operator: {expression.Operator}")
        };
    }

    private bool TryGetNumericValue(object value, out double result)
    {
        switch (value)
        {
            case int i:
                result = i;
                return true;
            case float f:
                result = f;
                return true;
            case double d:
                result = d;
                return true;
            case bool b:
                result = b ? 1 : 0;
                return true;
            case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }
}
