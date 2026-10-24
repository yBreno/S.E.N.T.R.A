using Api.Workers;
using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Application.Predictive;
using Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
namespace Tests;

public class PredictiveAutomationTests
{
    [Fact]
    public async Task Automacao_desligada_por_padrao_nao_resolve_analise_nem_cria_timer()
    {
        var clock = new TestClock();
        using var services = new ServiceCollection().BuildServiceProvider();
        using var worker = Worker(services, clock, new());
        await worker.StartAsync(default); await worker.ExecuteTask!;
        Assert.False(new PredictiveAutomationSettings().Enabled); Assert.Equal(60, new PredictiveAutomationSettings().IntervalMinutes);
        Assert.Equal(0, clock.Pending);
    }
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)] [InlineData(14)] [InlineData(1441)]
    public async Task Intervalos_excessivamente_curtos_ou_invalidos_sao_rejeitados(int minutes)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var worker = Worker(services, new(), new() { IntervalMinutes = minutes });
        await worker.StartAsync(default);
        await Assert.ThrowsAsync<OptionsValidationException>(async () => await worker.ExecuteTask!);
    }
    [Fact]
    public async Task Provedor_sem_coordenacao_distribuida_bloqueia_automacao_mesmo_habilitada()
    {
        var clock = new TestClock(); var analysis = new AnalysisStub();
        using var services = Services(analysis, false);
        using var worker = Worker(services, clock, new() { Enabled = true });
        await worker.StartAsync(default); await worker.ExecuteTask!;
        Assert.Equal(0, clock.Pending); Assert.Equal(0, analysis.Calls);
    }
    [Fact]
    public async Task Aguarda_intervalo_completo_recupera_falha_e_cancela_sem_sobrepor_ciclos()
    {
        var clock = new TestClock(); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var analysis = new AnalysisStub { Execute = async (call, ct) => { if (call == 1) throw new InvalidOperationException("falha sintética privada"); await release.Task.WaitAsync(ct); } };
        using var services = Services(analysis, true);
        using var worker = Worker(services, clock, new() { Enabled = true, IntervalMinutes = 15 });
        await worker.StartAsync(default); await Until(() => clock.Pending == 1);
        Assert.Equal(0, analysis.Calls); clock.Advance();
        await Until(() => analysis.Calls == 1 && clock.Pending == 1);
        clock.Advance(); await Until(() => analysis.Calls == 2);
        Assert.Equal(0, clock.Pending); Assert.Equal(1, analysis.MaximumConcurrency);
        await worker.StopAsync(default);
        Assert.True(analysis.Cancelled); Assert.Equal(2, analysis.Calls); Assert.Equal(0, clock.Pending);
    }
    [Fact]
    public async Task Coordenacao_local_serializa_fluxos_concorrentes_e_permite_chamadas_aninhadas()
    {
        using var coordinator = new InProcessPredictiveWriteCoordinator();
        int active = 0, maximum = 0;
        async Task<int> Run() => await coordinator.ExecuteAsync(async () =>
        {
            maximum = Math.Max(maximum, Interlocked.Increment(ref active));
            await Task.Yield();
            var result = await coordinator.ExecuteAsync(() => Task.FromResult(42));
            Interlocked.Decrement(ref active); return result;
        });
        Assert.All(await Task.WhenAll(Run(), Run(), Run()), value => Assert.Equal(42, value));
        Assert.Equal(1, maximum); Assert.False(coordinator.SupportsMultipleInstances);
    }
    [Fact]
    public void Estado_registra_origem_contagens_reais_e_conclusao_automatica_sem_persistencia()
    {
        var state = new PredictiveExecutionState(TimeProvider.System);
        Assert.Equal("NotRun", state.Snapshot().Status);
        state.Start("Automatic"); Assert.Equal("Running", state.Snapshot().Status);
        state.Complete(new(2, 3, [], []));
        var automatic = state.Snapshot(); Assert.Equal(3, automatic.RecommendationsCreated); Assert.Equal(2, automatic.PreventiveAlertsCreated);
        Assert.Equal(DateTimeKind.Utc, automatic.LastAutomaticCompletedAt!.Value.Kind);
        state.Start("Manual"); state.Fail(); var failed = state.Snapshot();
        Assert.Equal("Failed", failed.Status); Assert.Equal("Manual", failed.Origin); Assert.Null(failed.RecommendationsCreated);
        Assert.Equal(automatic.LastAutomaticCompletedAt, failed.LastAutomaticCompletedAt); Assert.Equal("InstanceMemory", failed.Scope);
        Assert.Equal("NotRun", new PredictiveExecutionState(TimeProvider.System).Snapshot().Status);
    }
    private static PredictiveAutomationWorker Worker(ServiceProvider services, TestClock clock, PredictiveAutomationSettings settings) =>
        new(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(settings), clock, NullLogger<PredictiveAutomationWorker>.Instance);
    private static ServiceProvider Services(AnalysisStub analysis, bool distributed) => new ServiceCollection()
        .AddSingleton<IPredictiveAnalysisService>(analysis).AddSingleton<IPredictiveWriteCoordinator>(new CoordinatorStub(distributed)).BuildServiceProvider();
    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(1, timeout.Token);
    }
    private sealed class CoordinatorStub(bool supports) : IPredictiveWriteCoordinator
    {
        public bool SupportsMultipleInstances => supports;
        public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default) => action();
    }
    private sealed class AnalysisStub : IPredictiveAnalysisService
    {
        public int Calls, MaximumConcurrency;
        private int active;
        public bool Cancelled;
        public Func<int, CancellationToken, Task> Execute = (_, _) => Task.CompletedTask;
        public Task<PredictiveAnalysisResultDto> RunAsync(CancellationToken ct = default) => throw new InvalidOperationException("Worker deve distinguir origem automática.");
        public async Task<PredictiveAnalysisResultDto> RunAutomaticAsync(CancellationToken ct = default)
        {
            var call = Interlocked.Increment(ref Calls); MaximumConcurrency = Math.Max(MaximumConcurrency, Interlocked.Increment(ref active));
            try { await Execute(call, ct); return new(0, 0, [], []); }
            catch (OperationCanceledException) { Cancelled = true; throw; }
            finally { Interlocked.Decrement(ref active); }
        }
    }
    private sealed class TestClock : TimeProvider
    {
        private readonly object sync = new();
        private readonly List<TestTimer> timers = [];
        public int Pending { get { lock (sync) return timers.Count; } }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new TestTimer(this, callback, state);
            lock (sync) timers.Add(timer);
            return timer;
        }
        public void Advance()
        {
            TestTimer[] current; lock (sync) { current = timers.ToArray(); timers.Clear(); }
            foreach (var timer in current) timer.Fire();
        }
        private sealed class TestTimer(TestClock owner, TimerCallback callback, object? state) : ITimer
        {
            public void Fire() => callback(state);
            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
            public void Dispose() { lock (owner.sync) owner.timers.Remove(this); }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
