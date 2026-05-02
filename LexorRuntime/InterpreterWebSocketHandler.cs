using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace orphinel.LexorRuntime;

public sealed class InterpreterWebSocketHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly LexorExecutionService _executionService;
    private readonly ILogger<InterpreterWebSocketHandler> _logger;

    public InterpreterWebSocketHandler(
        LexorExecutionService executionService,
        ILogger<InterpreterWebSocketHandler> logger)
    {
        _executionService = executionService;
        _logger = logger;
    }

    public async Task HandleAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        InteractiveSocketSession session = new(socket, _executionService, _logger);
        await session.RunAsync(cancellationToken);
    }

    private sealed class InteractiveSocketSession
    {
        private readonly WebSocket _socket;
        private readonly LexorExecutionService _executionService;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private TaskCompletionSource<string>? _pendingInput;
        private bool _isRunning;

        public InteractiveSocketSession(
            WebSocket socket,
            LexorExecutionService executionService,
            ILogger logger)
        {
            _socket = socket;
            _executionService = executionService;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await SendAsync(new ServerMessage("connected"), cancellationToken);

            while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                ClientMessage? message = await ReceiveAsync(cancellationToken);
                if (message is null)
                {
                    break;
                }

                switch (message.Type?.ToLowerInvariant())
                {
                    case "run":
                        await StartRunAsync(message.Source, cancellationToken);
                        break;
                    case "input":
                        SubmitInput(message.Value);
                        break;
                    default:
                        await SendAsync(
                            new ServerMessage("error", Error: "Unknown message type."),
                            cancellationToken);
                        break;
                }
            }

            if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
            }
        }

        private async Task StartRunAsync(string? source, CancellationToken cancellationToken)
        {
            if (_isRunning)
            {
                await SendAsync(new ServerMessage("error", Error: "An interpreter session is already running."), cancellationToken);
                return;
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                await SendAsync(new ServerMessage("error", Error: "Source code is required."), cancellationToken);
                return;
            }

            _isRunning = true;
            await SendAsync(new ServerMessage("started"), cancellationToken);

            _ = Task.Run(async () =>
            {
                try
                {
                    IReadOnlyDictionary<string, LexorVariableSnapshot> finalVariables =
                        await _executionService.ExecuteInteractiveAsync(
                        source,
                        async text =>
                        {
                            await SendAsync(new ServerMessage("output", Output: text), cancellationToken);
                        },
                        async variableNames =>
                        {
                            _pendingInput = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                            await SendAsync(
                                new ServerMessage("input_request", VariablesRequested: variableNames),
                                cancellationToken);
                            return await _pendingInput.Task.WaitAsync(cancellationToken);
                        });

                    await SendAsync(
                        new ServerMessage("completed", Success: true, Variables: finalVariables),
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _pendingInput?.TrySetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Interactive interpreter session failed");
                    await SendAsync(
                        new ServerMessage("completed", Success: false, Error: ex.Message),
                        CancellationToken.None);
                }
                finally
                {
                    _pendingInput = null;
                    _isRunning = false;
                }
            }, cancellationToken);
        }

        private void SubmitInput(string? value)
        {
            _pendingInput?.TrySetResult(value ?? string.Empty);
        }

        private async Task<ClientMessage?> ReceiveAsync(CancellationToken cancellationToken)
        {
            ArrayBufferWriter<byte> buffer = new();
            ValueWebSocketReceiveResult result;

            do
            {
                Memory<byte> chunk = buffer.GetMemory(4096);
                result = await _socket.ReceiveAsync(chunk, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                buffer.Advance(result.Count);
            }
            while (!result.EndOfMessage);

            string json = Encoding.UTF8.GetString(buffer.WrittenSpan);
            return JsonSerializer.Deserialize<ClientMessage>(json, JsonOptions);
        }

        private async Task SendAsync(ServerMessage message, CancellationToken cancellationToken)
        {
            if (_socket.State != WebSocketState.Open)
            {
                return;
            }

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                await _socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }

    private sealed record ClientMessage(
        string? Type,
        string? Source,
        string? Value);

    private sealed record ServerMessage(
        string Type,
        bool? Success = null,
        string? Output = null,
        string? Error = null,
        IReadOnlyList<string>? VariablesRequested = null,
        IReadOnlyDictionary<string, LexorVariableSnapshot>? Variables = null);
}
