using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting;
using TrailTrainer.Developer.Core;
using TrailTrainer.Developer.Tasks;

namespace TrailTrainer.Developer.Tests;

public sealed class HostedAutomaticResumeServiceTests
{
    [Fact]
    public void RequestProvider_CoreAbstractionIsSynchronous()
    {
        var method = typeof(IAutomaticResumeWorkerRequestProvider).GetMethod(
            nameof(IAutomaticResumeWorkerRequestProvider.GetRequest));

        Assert.NotNull(method);
        Assert.Empty(method.GetParameters());
        Assert.Equal(typeof(AutomaticResumeWorkerRequest), method.ReturnType);
    }

    [Fact]
    public async Task StartAsync_GetsRequestAndInvokesWorkerExactlyOnceWithExactValues()
    {
        using var source = new CancellationTokenSource();
        var request = WorkerRequest();
        var provider = new FakeRequestProvider { Request = request };
        var workerResult = WorkerResult();
        var worker = new FakeWorker { Result = workerResult };

        await CreateService(worker, provider).StartAsync(source.Token);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, worker.CallCount);
        Assert.Same(request, worker.Request);
        Assert.Equal(source.Token, worker.Token);
        Assert.Same(workerResult, worker.ReturnedResult);
    }

    [Fact]
    public async Task StartAsync_ExplicitRepeatedHostInvocationDelegatesOncePerCall()
    {
        var provider = new FakeRequestProvider { Request = WorkerRequest() };
        var worker = new FakeWorker { Result = WorkerResult() };
        var service = CreateService(worker, provider);

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        Assert.Equal(2, provider.CallCount);
        Assert.Equal(2, worker.CallCount);
    }

    [Fact]
    public async Task StartAsync_RejectsNullProviderResultAndPreventsWorker()
    {
        var provider = new FakeRequestProvider { Request = null };
        var worker = new FakeWorker { Result = WorkerResult() };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(worker, provider).StartAsync(CancellationToken.None));

        Assert.Contains("returned null", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(0, worker.CallCount);
    }

    [Fact]
    public async Task StartAsync_ProviderExceptionPropagatesUnchangedAndPreventsWorker()
    {
        var expected = new InvalidDataException("provider failed");
        var provider = new FakeRequestProvider { Exception = expected };
        var worker = new FakeWorker { Result = WorkerResult() };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateService(worker, provider).StartAsync(CancellationToken.None));

        Assert.Same(expected, exception);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(0, worker.CallCount);
    }

    [Fact]
    public async Task StartAsync_AwaitsWorkerCompletionWithoutDetachedWorkOrSecondInvocation()
    {
        var completion = new TaskCompletionSource<AutomaticResumeWorkerResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new FakeRequestProvider { Request = WorkerRequest() };
        var worker = new FakeWorker { PendingResult = completion.Task };
        var service = CreateService(worker, provider);

        var start = service.StartAsync(CancellationToken.None);

        Assert.False(start.IsCompleted);
        Assert.Equal(1, worker.CallCount);
        completion.SetResult(WorkerResult());
        await start;
        Assert.Equal(1, worker.CallCount);
    }

    [Fact]
    public async Task StartAsync_WorkerExceptionPropagatesUnchangedWithoutRetry()
    {
        var expected = new IOException("worker failed");
        var provider = new FakeRequestProvider { Request = WorkerRequest() };
        var worker = new FakeWorker { Exception = expected };

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            CreateService(worker, provider).StartAsync(CancellationToken.None));

        Assert.Same(expected, exception);
        Assert.Equal(1, worker.CallCount);
    }

    [Fact]
    public async Task StartAsync_WorkerCancellationPropagatesUnchanged()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var provider = new FakeRequestProvider { Request = WorkerRequest() };
        var worker = new FakeWorker { HonorCancellation = true };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(worker, provider).StartAsync(source.Token));

        Assert.Equal(1, worker.CallCount);
        Assert.Equal(source.Token, worker.Token);
    }

    [Fact]
    public async Task StopAsync_CompletesWithoutProviderOrWorkerInvocation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var provider = new FakeRequestProvider { Request = WorkerRequest() };
        var worker = new FakeWorker { Result = WorkerResult() };

        await CreateService(worker, provider).StopAsync(source.Token);

        Assert.Equal(0, provider.CallCount);
        Assert.Equal(0, worker.CallCount);
    }

    [Fact]
    public void HostedAdapter_ImplementsIHostedServiceWithExactlyRequiredDependencies()
    {
        var serviceType = typeof(HostedAutomaticResumeService);
        var parameters = Assert.Single(serviceType.GetConstructors()).GetParameters();

        Assert.Contains(typeof(IHostedService), serviceType.GetInterfaces());
        Assert.Equal(
            [
                typeof(IAutomaticResumeWorker),
                typeof(IAutomaticResumeWorkerRequestProvider),
                typeof(IInitialDeveloperTaskIntake),
                typeof(IInitialDeveloperTaskIntakeRequestProvider),
                typeof(Microsoft.Extensions.Logging.ILogger<HostedAutomaticResumeService>)
            ],
            parameters.Select(parameter => parameter.ParameterType));
        Assert.False(typeof(BackgroundService).IsAssignableFrom(serviceType));
    }

    private static HostedAutomaticResumeService CreateService(
        IAutomaticResumeWorker worker,
        IAutomaticResumeWorkerRequestProvider provider) =>
        new(worker, provider, new NoOpIntake(), new DisabledIntakeRequestProvider());

    private sealed class NoOpIntake : IInitialDeveloperTaskIntake
    {
        public Task<InitialDeveloperTaskIntakeResult> ExecuteAsync(
            InitialDeveloperTaskIntakeRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new InitialDeveloperTaskIntakeResult(
                InitialDeveloperTaskIntakeState.Disabled));
    }

    private sealed class DisabledIntakeRequestProvider : IInitialDeveloperTaskIntakeRequestProvider
    {
        public InitialDeveloperTaskIntakeRequest GetRequest() => new(
            false,
            string.Empty,
            string.Empty,
            string.Empty,
            "main",
            "origin",
            PullRequestMergeMethod.Squash,
            null,
            null,
            false);
    }

    private static AutomaticResumeWorkerRequest WorkerRequest()
    {
        var runRequest = new AutomaticResumeRunRequest(
            new AutomaticResumeBatchRunRequest(
                new AutomaticResumeBatchStepRequest(
                    PullRequestMergeMethod.Squash,
                    "title",
                    "message",
                    true),
                2),
            2);
        return new AutomaticResumeWorkerRequest(
            new RepeatedDelayedAutomaticResumeRequest(
                runRequest,
                TimeSpan.FromMinutes(5),
                3));
    }

    private static AutomaticResumeWorkerResult WorkerResult() => new(
        (RepeatedDelayedAutomaticResumeResult)RuntimeHelpers.GetUninitializedObject(
            typeof(RepeatedDelayedAutomaticResumeResult)));

    private sealed class FakeRequestProvider : IAutomaticResumeWorkerRequestProvider
    {
        public AutomaticResumeWorkerRequest? Request { get; init; }
        public Exception? Exception { get; init; }
        public int CallCount { get; private set; }

        public AutomaticResumeWorkerRequest GetRequest()
        {
            CallCount++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Request!;
        }
    }

    private sealed class FakeWorker : IAutomaticResumeWorker
    {
        public AutomaticResumeWorkerResult? Result { get; init; }
        public Task<AutomaticResumeWorkerResult>? PendingResult { get; init; }
        public Exception? Exception { get; init; }
        public bool HonorCancellation { get; init; }
        public int CallCount { get; private set; }
        public AutomaticResumeWorkerRequest? Request { get; private set; }
        public CancellationToken Token { get; private set; }
        public AutomaticResumeWorkerResult? ReturnedResult { get; private set; }

        public Task<AutomaticResumeWorkerResult> RunAsync(
            AutomaticResumeWorkerRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Request = request;
            Token = cancellationToken;
            if (HonorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (Exception is not null)
            {
                return Task.FromException<AutomaticResumeWorkerResult>(Exception);
            }

            if (PendingResult is not null)
            {
                return PendingResult;
            }

            ReturnedResult = Result;
            return Task.FromResult(Result!);
        }
    }
}
