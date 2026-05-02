using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace orphinel.LexorRuntime;

public sealed class LexorExecutionService
{
    private readonly ILogger<LexorExecutionService> _logger;

    public LexorExecutionService(ILogger<LexorExecutionService> logger)
    {
        _logger = logger;
    }

    public async Task<LexorExecutionResponse> ExecuteAsync(LexorExecutionRequest request)
    {
        try
        {
            ScriptProgram program = ParseProgram(request.Source);
            Queue<string> inputs = new(request.Inputs ?? Array.Empty<string>());
            StringBuilder output = new();

            Interpreter interpreter = new(
                text =>
                {
                    output.Append(text);
                    return Task.CompletedTask;
                },
                variableNames =>
                {
                    if (inputs.Count == 0)
                    {
                        throw new Exception(
                            $"SCAN expected input for variable(s): {string.Join(", ", variableNames)}.");
                    }

                    return Task.FromResult(inputs.Dequeue());
                });

            await interpreter.ExecuteAsync(program);

            return new LexorExecutionResponse(
                true,
                output.ToString(),
                null,
                interpreter.Variables);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LEXOR execution failed");

            return new LexorExecutionResponse(
                false,
                string.Empty,
                ex.Message,
                new Dictionary<string, LexorVariableSnapshot>());
        }
    }

    public async Task<IReadOnlyDictionary<string, LexorVariableSnapshot>> ExecuteInteractiveAsync(
        string source,
        Func<string, Task> outputWriter,
        Func<IReadOnlyList<string>, Task<string>> inputReader)
    {
        ScriptProgram program = ParseProgram(source);
        Interpreter interpreter = new(outputWriter, inputReader);
        await interpreter.ExecuteAsync(program);
        return interpreter.Variables;
    }

    private static ScriptProgram ParseProgram(string source)
    {
        Lexor lexor = new(source);
        List<Token> tokens = lexor.Tokenize();
        Parser parser = new(tokens);
        return parser.ParseProgram();
    }
}

public sealed record LexorExecutionRequest(string Source, IReadOnlyList<string>? Inputs);

public sealed record LexorExecutionResponse(
    bool Success,
    string Output,
    string? Error,
    IReadOnlyDictionary<string, LexorVariableSnapshot> Variables);

public sealed record LexorVariableSnapshot(string Type, string Value);
