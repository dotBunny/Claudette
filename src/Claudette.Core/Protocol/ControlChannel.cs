using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Claudette.Core.Protocol;

/// <summary>A control request that Claude Code answered with an error.</summary>
public sealed class ControlRequestException(string subtype, string error)
    : Exception($"Claude Code rejected the '{subtype}' control request: {error}")
{
    public string Subtype { get; } = subtype;

    public string Error { get; } = error;
}

/// <summary>
/// Sends control requests and matches Claude Code's responses to them by <c>request_id</c> (DESIGN.md §13).
/// Request ids are <c>req_1</c>, <c>req_2</c>, … in send order, which keeps recorded protocol fixtures replayable.
/// </summary>
internal sealed class ControlChannel(Func<string, CancellationToken, ValueTask> sendLine, TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, Pending> _pending = new();
    private int _nextId;
    private Exception? _failure;

    public async Task<JsonObject> RequestAsync(JsonObject request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var subtype = request.GetString("subtype") ?? "?";
        var requestId = $"req_{Interlocked.Increment(ref _nextId)}";
        var pending = new Pending(subtype);
        _pending[requestId] = pending;
        // Started before the request goes out, so the deadline exists by the time anything can see the request (a test
        // that sees it and moves the fake clock on, say).
        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        try
        {
            IOException? unsent = null;
            if (Volatile.Read(ref _failure) is { } failure)
            {
                // Asked after the process has gone: it fails as the requests waiting then did, rather than waiting it out.
                pending.Completion.TrySetException(failure);
            }
            else
            {
                try
                {
                    await sendLine(OutgoingMessages.ControlRequest(requestId, request).ToJsonString(), cancellationToken).ConfigureAwait(false);
                }
                catch (IOException ex)
                {
                    // Its input closed because it's exiting, say having found no sign-in: its exit fails the request
                    // with why it stopped, which the broken pipe doesn't say.
                    unsent = ex;
                }
            }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            try
            {
                return await pending.Completion.Task.WaitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                if (unsent is not null)
                {
                    throw new IOException($"Couldn't send the '{subtype}' control request to Claude Code: {unsent.Message}", unsent);
                }
                throw new TimeoutException($"Claude Code didn't answer the '{subtype}' control request within {timeout}.");
            }
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    /// <summary>Completes the matching request. Returns false if nothing was waiting for this response.</summary>
    public bool TryComplete(ControlResponseMessage response)
    {
        if (!_pending.TryRemove(response.RequestId, out var pending))
        {
            return false;
        }
        if (response.IsSuccess)
        {
            pending.Completion.TrySetResult(response.Response ?? []);
        }
        else
        {
            pending.Completion.TrySetException(new ControlRequestException(pending.Subtype, response.Error ?? "unknown error"));
        }
        return true;
    }

    /// <summary>Fails every outstanding request, and every later one, because the process exited.</summary>
    public void FailAll(Exception exception)
    {
        // Set before the requests are failed, so a request made meanwhile sees one or the other.
        Volatile.Write(ref _failure, exception);
        foreach (var requestId in _pending.Keys)
        {
            if (_pending.TryRemove(requestId, out var pending))
            {
                pending.Completion.TrySetException(exception);
            }
        }
    }

    public ValueTask RespondAsync(string requestId, JsonObject? response, CancellationToken cancellationToken) =>
        sendLine(OutgoingMessages.ControlSuccess(requestId, response).ToJsonString(), cancellationToken);

    public ValueTask RespondErrorAsync(string requestId, string error, CancellationToken cancellationToken) =>
        sendLine(OutgoingMessages.ControlError(requestId, error).ToJsonString(), cancellationToken);

    private sealed class Pending(string subtype)
    {
        public string Subtype { get; } = subtype;

        public TaskCompletionSource<JsonObject> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
