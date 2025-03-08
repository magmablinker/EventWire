using System.Diagnostics;
using EventWire.Abstractions.Contracts.Handlers;
using EventWire.Abstractions.Contracts.Protocol;
using EventWire.Abstractions.Models;
using EventWire.Core.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventWire.Core.Services;

internal sealed class EnvelopeProcessorService : IEnvelopeProcessorService
{
    private sealed record ProcessingTask(CancellationTokenSource CancellationTokenSource, Task Task);

    private readonly List<ProcessingTask> _processingTasks = [];
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EnvelopeProcessorService> _logger;
    private readonly SemaphoreSlim _tickets = new(1, 1);

    private readonly CancellationTokenSource _cleanupCts = new();
    private readonly Task _cleanupTask;
    private bool _disposed;

    public EnvelopeProcessorService(IServiceProvider serviceProvider, ILogger<EnvelopeProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _cleanupTask = Task.Run(CleanupAsync);
    }

    public async Task EnqueueAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        var cts = new CancellationTokenSource();
        var task = Task.Run(async () => await ProcessAsync(envelope, cts.Token), cts.Token);

        await _tickets.WaitAsync(cancellationToken);
        try
        {
            _processingTasks.Add(new(cts, task));
        }
        finally
        {
            _tickets.Release();
        }
    }

    private async Task CleanupAsync()
    {
        var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await periodicTimer.WaitForNextTickAsync(_cleanupCts.Token))
            {
                await _tickets.WaitAsync(_cleanupCts.Token);
                try
                {
                    _processingTasks.RemoveAll(x => x.Task.IsCompleted);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An exception occurred during cleanup");
                }
                finally
                {
                    _tickets.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("The cleanup task has been cancelled");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "The cleanup task has failed");
        }
    }

    /// <summary>
    /// Processes an incoming message using the appropriate handler
    /// </summary>
    private async Task ProcessAsync(Envelope envelope, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var payloadType = Type.GetType(envelope.Headers[Header.PayloadType]) ??
                              throw new InvalidOperationException($"Unable to create type '{envelope.Headers[Header.PayloadType]}'");

            var handlerType = typeof(IMessageHandler<>).MakeGenericType(payloadType);
            var handlerServiceType = typeof(IMessageHandlerService<,>).MakeGenericType(payloadType, handlerType);
            var handlerService = (IMessageHandlerService)scope.ServiceProvider.GetRequiredService(handlerServiceType);
            await handlerService.HandleAsync(envelope, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
        }
        sw.Stop();

        _logger.LogDebug("Handling took {elapsed}μs",
            Math.Round(sw.Elapsed.TotalMicroseconds, MidpointRounding.ToEven));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _cleanupCts.CancelAsync();
        await _cleanupTask;
        _cleanupCts.Dispose();

        foreach (var processingTask in _processingTasks)
        {
            try
            {
                await processingTask.CancellationTokenSource.CancelAsync();
                // Wait max 1 second for task
                await Task.WhenAny(processingTask.Task, Task.Delay(TimeSpan.FromSeconds(1)));
                processingTask.CancellationTokenSource.Dispose();
                processingTask.Task.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to cancel task");
            }
        }

        _disposed = true;
    }
}
