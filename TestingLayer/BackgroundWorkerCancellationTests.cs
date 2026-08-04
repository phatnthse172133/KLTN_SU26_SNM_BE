using InfrastructureLayer.Backgrounds;
using InfrastructureLayer.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TestingLayer;

public class BackgroundWorkerCancellationTests
{
    [Fact]
    public async Task SubscriptionExpiryWorker_HostShutdownDuringIteration_CompletesWithoutError()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var logger = new RecordingLogger<SubscriptionExpiryWorker>();
        var enteredIteration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new TestSubscriptionExpiryWorker(
            services,
            logger,
            async cancellationToken =>
            {
                enteredIteration.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        using var stoppingSource = new CancellationTokenSource();

        var execution = worker.RunAsync(stoppingSource.Token);
        await enteredIteration.Task.WaitAsync(TimeSpan.FromSeconds(2));
        stoppingSource.Cancel();

        await execution.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task EmailOutboxWorker_HostShutdownDuringIteration_CompletesWithoutError()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var logger = new RecordingLogger<EmailOutboxWorker>();
        var enteredIteration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new TestEmailOutboxWorker(
            services,
            logger,
            async cancellationToken =>
            {
                enteredIteration.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        using var stoppingSource = new CancellationTokenSource();

        var execution = worker.RunAsync(stoppingSource.Token);
        await enteredIteration.Task.WaitAsync(TimeSpan.FromSeconds(2));
        stoppingSource.Cancel();

        await execution.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task OrderCleanupWorker_HostShutdownDuringIteration_CompletesWithoutError()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var logger = new RecordingLogger<OrderCleanupBackgroundService>();
        var enteredIteration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new TestOrderCleanupWorker(
            services,
            logger,
            async cancellationToken =>
            {
                enteredIteration.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        using var stoppingSource = new CancellationTokenSource();

        var execution = worker.RunAsync(stoppingSource.Token);
        await enteredIteration.Task.WaitAsync(TimeSpan.FromSeconds(2));
        stoppingSource.Cancel();

        await execution.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task SubscriptionExpiryWorker_CancellationWithoutHostShutdown_IsLoggedAndRetried()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var logger = new RecordingLogger<SubscriptionExpiryWorker>();
        var secondIteration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempt = 0;
        var worker = new TestSubscriptionExpiryWorker(
            services,
            logger,
            async cancellationToken =>
            {
                if (Interlocked.Increment(ref attempt) == 1)
                    throw new OperationCanceledException("Dependency canceled independently.");

                secondIteration.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        using var stoppingSource = new CancellationTokenSource();

        var execution = worker.RunAsync(stoppingSource.Token);
        await secondIteration.Task.WaitAsync(TimeSpan.FromSeconds(2));
        stoppingSource.Cancel();
        await execution.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error
                && entry.Exception is OperationCanceledException
                && entry.Message.Contains("retry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EmailOutboxWorker_RealFailure_IsLoggedAndRetried()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var logger = new RecordingLogger<EmailOutboxWorker>();
        var secondIteration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempt = 0;
        var worker = new TestEmailOutboxWorker(
            services,
            logger,
            async cancellationToken =>
            {
                if (Interlocked.Increment(ref attempt) == 1)
                    throw new InvalidOperationException("Simulated SMTP failure.");

                secondIteration.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        using var stoppingSource = new CancellationTokenSource();

        var execution = worker.RunAsync(stoppingSource.Token);
        await secondIteration.Task.WaitAsync(TimeSpan.FromSeconds(2));
        stoppingSource.Cancel();
        await execution.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error
                && entry.Exception is InvalidOperationException
                && entry.Message.Contains("retry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OrderCleanupWorker_RealFailure_IsLoggedAndRetried()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var logger = new RecordingLogger<OrderCleanupBackgroundService>();
        var secondIteration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempt = 0;
        var worker = new TestOrderCleanupWorker(
            services,
            logger,
            async cancellationToken =>
            {
                if (Interlocked.Increment(ref attempt) == 1)
                    throw new InvalidOperationException("Simulated database failure.");

                secondIteration.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        using var stoppingSource = new CancellationTokenSource();

        var execution = worker.RunAsync(stoppingSource.Token);
        await secondIteration.Task.WaitAsync(TimeSpan.FromSeconds(2));
        stoppingSource.Cancel();
        await execution.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error
                && entry.Exception is InvalidOperationException
                && entry.Message.Contains("chu kỳ kế tiếp", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TestSubscriptionExpiryWorker : SubscriptionExpiryWorker
    {
        private readonly Func<CancellationToken, Task> _iteration;

        public TestSubscriptionExpiryWorker(
            IServiceProvider serviceProvider,
            ILogger<SubscriptionExpiryWorker> logger,
            Func<CancellationToken, Task> iteration)
            : base(serviceProvider, logger)
        {
            _iteration = iteration;
        }

        protected override TimeSpan Interval => TimeSpan.FromMilliseconds(1);

        protected override Task ExpireSubscriptionsAsync(CancellationToken stoppingToken) =>
            _iteration(stoppingToken);

        public Task RunAsync(CancellationToken stoppingToken) => ExecuteAsync(stoppingToken);
    }

    private sealed class TestEmailOutboxWorker : EmailOutboxWorker
    {
        private readonly Func<CancellationToken, Task> _iteration;

        public TestEmailOutboxWorker(
            IServiceProvider serviceProvider,
            ILogger<EmailOutboxWorker> logger,
            Func<CancellationToken, Task> iteration)
            : base(serviceProvider, logger)
        {
            _iteration = iteration;
        }

        protected override TimeSpan Interval => TimeSpan.FromMilliseconds(1);

        protected override Task ProcessOutboxAsync(CancellationToken stoppingToken) =>
            _iteration(stoppingToken);

        public Task RunAsync(CancellationToken stoppingToken) => ExecuteAsync(stoppingToken);
    }

    private sealed class TestOrderCleanupWorker : OrderCleanupBackgroundService
    {
        private readonly Func<CancellationToken, Task> _iteration;

        public TestOrderCleanupWorker(
            IServiceProvider serviceProvider,
            ILogger<OrderCleanupBackgroundService> logger,
            Func<CancellationToken, Task> iteration)
            : base(serviceProvider, logger)
        {
            _iteration = iteration;
        }

        protected override TimeSpan Period => TimeSpan.FromMilliseconds(1);

        public override Task RunOnceAsync(CancellationToken stoppingToken = default) =>
            _iteration(stoppingToken);

        public Task RunAsync(CancellationToken stoppingToken) => ExecuteAsync(stoppingToken);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly object _sync = new();
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (_sync)
                    return _entries.ToArray();
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_sync)
                _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
