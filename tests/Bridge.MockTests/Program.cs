using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BooBoopBridge;
using SecretFlasherManaka.BooBoopBridge;
using SecretFlasherManaka.ForEveryThing;

namespace Bridge.MockTests;

internal static partial class Program
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    private static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("paths: machine and current-user defaults", DefaultInstallationPathsAsync),
            ("paths: custom drives, Unicode, spaces and working directory", CustomInstallationPathsAsync),
            ("paths: environment variables expand independently", EnvironmentInstallationPathsAsync),
            ("paths: invalid and relative paths fail before startup", InvalidInstallationPathsAsync),
            ("paths: normalization and empty cache fallback", NormalizedInstallationPathsAsync),
            ("mapping: vibration and piston modes, unknown/inactive, compatible reason IDs", MappingAsync),
            ("hub: every observation is published; semantic changes are deduplicated", HubObservationsAsync),
            ("hub: throwing subscribers/reporters do not block other subscribers", HubSubscriberIsolationAsync),
            ("startup: baseline stop precedes motion; pre-ready observations are stale", StartupRequiresFreshObservationAsync),
            ("outputs: vibration mapping and duplicate observations remain compatible", VibrationAndDeduplicationAsync),
            ("pause barrier: both channels require frames after global stop completion", PauseBarrierAsync),
            ("Off barrier: immediate High cannot erase the pending stop", OffBarrierAsync),
            ("source-disabled: ordinary source loss stops only its own channel", SourceDisabledBarrierAsync),
            ("mailbox: inflight writes retain only the newest subsequent positive level", LatestValueWinsAsync),
            ("watchdog: one second without vibration observations sends vibration zero", WatchdogAsync),
            ("watchdog: continuously observed High is not duration-limited", ContinuousObservationsAsync),
            ("failure: initialization is not automatically retried", InitializeFailureAsync),
            ("failure: baseline stop failure prevents positive motion and is not retried", StopFailureAsync),
            ("failure: positive write is not automatically retried", VibrationFailureAsync),
            ("failure: readiness loss cleans up without reconnecting", ReadinessLossAsync),
            ("lifecycle: dispose before Start never initializes and cleans up once", DisposeBeforeStartAsync),
            ("lifecycle: disposal during initialization cancels and cleans up once", DisposeDuringInitializationAsync),
            ("lifecycle: concurrent and repeated disposal shares one cleanup", ConcurrentDisposeAsync),
            ("lifecycle: controller diagnostic subscriptions are removed on disposal", SubscriptionCleanupAsync),
            ("diagnostics: a throwing logger cannot prevent cleanup", ThrowingLoggerAsync),
            ("piston hub: all observations publish and semantic changes deduplicate", PistonHubObservationsAsync),
            ("piston hub: throwing subscribers and reporters are isolated", PistonHubSubscriberIsolationAsync),
            ("outputs: simultaneous vibration and piston remain independently deduplicated", BothOutputsAndDeduplicationAsync),
            ("outputs: every piston mode dispatches its mapped level", PistonModesAsync),
            ("outputs: vibration Off preserves piston; piston Off preserves vibration", IndependentOffAsync),
            ("outputs: both Off independently zero both channels", BothOffAsync),
            ("outputs: unknown, invalid, inactive, and unavailable states stop only their channel", InvalidStatesIndependentAsync),
            ("piston barrier: immediate Fast cannot erase an Off stop", PistonOffBarrierAsync),
            ("both barriers: both zero requests survive coalesced positive values", BothOffBarriersAsync),
            ("global barrier: piston-origin stop also requires new frames on both channels", PistonGlobalStopAsync),
            ("mailbox: held vibration rereads newest vibration and piston Off", HeldVibrationRereadsBothAsync),
            ("mailbox: held piston rereads newest piston and vibration Off", HeldPistonRereadsBothAsync),
            ("mailbox: cross-channel updates coalesce before first dispatch", CrossChannelCoalescingAsync),
            ("watchdog: fresh piston cannot keep stale vibration moving", VibrationStaleIndependentAsync),
            ("watchdog: fresh vibration cannot keep stale piston moving", PistonStaleIndependentAsync),
            ("watchdog: repeated independent frames sustain both outputs", BothContinuousObservationsAsync),
            ("startup: each channel independently requires a post-ready timestamp", TimestampFreshnessAsync),
            ("failure: piston write fails once and cleans up", PistonFailureAsync),
            ("failure: queued dual-channel work and concurrent disposal share cleanup", ConcurrentWriteFailureAsync),
            ("hardware queue: vibration zero invalidates only old vibration tickets", VibrationEpochAsync),
            ("hardware queue: stretch zero invalidates only old stretch tickets", StretchEpochAsync),
            ("hardware queue: all-stop invalidates both channels and permits new tickets", GlobalEpochAsync)
        };

        Console.WriteLine("Bridge.MockTests: fake controllers only; no game, device, or native host is started.");
        int failures = 0;
        foreach (var test in tests)
        {
            var elapsed = Stopwatch.StartNew();
            try
            {
                await test.Run().WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
                Console.WriteLine($"PASS {test.Name} ({elapsed.ElapsedMilliseconds} ms)");
            }
            catch (Exception error)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {test.Name}\n{error}");
            }
        }
        Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed; {failures} failed.");
        return failures == 0 ? 0 : 1;
    }

    private static Task MappingAsync()
    {
        Equal(0, BooBoopGameSession.MapVibration(Snapshot(GameStrength.Off)), "Off");
        Equal(4, BooBoopGameSession.MapVibration(Snapshot(GameStrength.Low)), "Low");
        Equal(8, BooBoopGameSession.MapVibration(Snapshot(GameStrength.High)), "High");
        Equal(0, BooBoopGameSession.MapVibration(Snapshot(GameStrength.Unknown)), "Unknown");
        Equal(0, BooBoopGameSession.MapVibration(Snapshot((GameStrength)123)), "Unrecognized enum");
        foreach (GameStrength strength in Enum.GetValues<GameStrength>())
            Equal(0, BooBoopGameSession.MapVibration(Snapshot(strength, active: false)), $"Inactive {strength}");
        Equal(0, BooBoopGameSession.MapStretch(PistonSnapshot(PistonMode.Off)), "Piston Off");
        Equal(4, BooBoopGameSession.MapStretch(PistonSnapshot(PistonMode.Slow)), "Piston Slow");
        Equal(6, BooBoopGameSession.MapStretch(PistonSnapshot(PistonMode.Medium)), "Piston Medium");
        Equal(8, BooBoopGameSession.MapStretch(PistonSnapshot(PistonMode.Fast)), "Piston Fast");
        Equal(0, BooBoopGameSession.MapStretch(PistonSnapshot(PistonMode.Unknown)), "Piston Unknown");
        Equal(0, BooBoopGameSession.MapStretch(PistonSnapshot((PistonMode)123)), "Unrecognized piston enum");
        foreach (PistonMode mode in Enum.GetValues<PistonMode>())
            Equal(0, BooBoopGameSession.MapStretch(PistonSnapshot(mode, active: false)), $"Inactive piston {mode}");
        Equal(0, (int)GameStateReason.Normal, "Normal keeps its existing numeric ID");
        Equal(1, (int)GameStateReason.LeaderUnavailable, "LeaderUnavailable ID");
        Equal(2, (int)GameStateReason.SceneChanged, "SceneChanged ID");
        Equal(3, (int)GameStateReason.Paused, "Paused ID");
        Equal(4, (int)GameStateReason.ShuttingDown, "ShuttingDown ID");
        Equal(5, (int)GameStateReason.SourceDisabled, "SourceDisabled ID");
        Check((int)GameStateReason.SourceUnavailable > 5, "SourceUnavailable is appended.");
        Check((int)GameStateReason.InvalidValue > 5, "InvalidValue is appended.");
        Check(GameStateReason.SourceUnavailable != GameStateReason.InvalidValue, "New reason values are distinct.");
        return Task.CompletedTask;
    }

    private static Task HubObservationsAsync()
    {
        GameStateHub.Publish(false, GameStrength.Off, GameStateReason.LeaderUnavailable);
        var observed = new List<(GameStateSnapshot Snapshot, bool Stop)>();
        var changed = new List<GameStateSnapshot>();
        Action<GameStateSnapshot, bool> onObserved = (snapshot, stop) => observed.Add((snapshot, stop));
        Action<GameStateSnapshot> onChanged = snapshot => changed.Add(snapshot);
        GameStateHub.Observed += onObserved;
        GameStateHub.StateChanged += onChanged;
        try
        {
            GameStateHub.Publish(true, GameStrength.Low, GameStateReason.Normal);
            GameStateHub.Publish(true, GameStrength.Low, GameStateReason.Normal);
            GameStateHub.Publish(true, GameStrength.Low, GameStateReason.Normal);
            GameStateHub.Publish(true, GameStrength.Low, GameStateReason.Normal, forceStop: true);
            Equal(4, observed.Count, "Observed count, including identical frames and forced stop");
            Equal(1, changed.Count, "StateChanged deduplicates identical semantic state");
            Check(observed.All(item => item.Snapshot.Revision == changed[0].Revision), "Repeated frames keep the semantic revision.");
            Check(observed[3].Stop && observed.Take(3).All(item => !item.Stop), "Stop barrier is preserved in Observed.");
            for (int index = 1; index < observed.Count; index++)
                Check(observed[index].Snapshot.ObservedAt >= observed[index - 1].Snapshot.ObservedAt, "Observation timestamps must be monotonic.");
            GameStateHub.Publish(true, GameStrength.High, GameStateReason.Normal);
            Equal(2, changed.Count, "Strength change emits StateChanged");
            Equal(changed[0].Revision + 1, changed[1].Revision, "Semantic revision increments once");
            GameStateHub.Publish(true, GameStrength.High, GameStateReason.Paused, forceStop: true);
            Equal(3, changed.Count, "Reason change emits StateChanged");
            Equal(observed.Last().Snapshot, GameStateHub.Current, "Current stores the latest observation");
        }
        finally
        {
            GameStateHub.Observed -= onObserved;
            GameStateHub.StateChanged -= onChanged;
        }
        return Task.CompletedTask;
    }

    private static Task HubSubscriberIsolationAsync()
    {
        GameStateHub.Publish(false, GameStrength.Off, GameStateReason.LeaderUnavailable);
        int healthyObserved = 0, healthyChanged = 0, reported = 0;
        Action<GameStateSnapshot, bool> badObserved = (_, _) => throw new InvalidOperationException("expected observed failure");
        Action<GameStateSnapshot, bool> goodObserved = (_, _) => healthyObserved++;
        Action<GameStateSnapshot> badChanged = _ => throw new InvalidOperationException("expected changed failure");
        Action<GameStateSnapshot> goodChanged = _ => healthyChanged++;
        Action<Exception>? previousReporter = GameStateHub.SubscriberError;
        GameStateHub.SubscriberError = _ => reported++;
        GameStateHub.Observed += badObserved;
        GameStateHub.Observed += goodObserved;
        GameStateHub.StateChanged += badChanged;
        GameStateHub.StateChanged += goodChanged;
        try
        {
            GameStateHub.Publish(true, GameStrength.Low, GameStateReason.Normal);
            Equal(1, healthyObserved, "Healthy Observed handler still runs");
            Equal(1, healthyChanged, "Healthy StateChanged handler still runs");
            Equal(2, reported, "Each failed subscriber is reported");
            GameStateHub.SubscriberError = _ => throw new InvalidOperationException("expected reporter failure");
            GameStateHub.Publish(true, GameStrength.High, GameStateReason.Normal);
            Equal(2, healthyObserved, "A throwing error reporter does not block the next Observed handler");
            Equal(2, healthyChanged, "A throwing error reporter does not block the next StateChanged handler");
        }
        finally
        {
            GameStateHub.Observed -= badObserved;
            GameStateHub.Observed -= goodObserved;
            GameStateHub.StateChanged -= badChanged;
            GameStateHub.StateChanged -= goodChanged;
            GameStateHub.SubscriberError = previousReporter;
        }
        return Task.CompletedTask;
    }

    private static async Task StartupRequiresFreshObservationAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        var initializing = fake.HoldNext("initialize");
        var baseline = fake.HoldNext("stop");
        var session = new BooBoopGameSession(fake, log.Add);
        try
        {
            session.ApplySnapshot(Snapshot(GameStrength.High));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
            session.Start();
            session.Start();
            await initializing.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            session.ApplySnapshot(Snapshot(GameStrength.Low));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Slow));
            initializing.Release();
            await baseline.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            Equal(0, fake.PositiveCommands().Length, "No motion while baseline stop is pending");
            session.ApplySnapshot(Snapshot(GameStrength.High));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
            baseline.Release();
            await ReadyAsync(log).ConfigureAwait(false);
            await CheckForWindowAsync(() => Equal(0, fake.PositiveCommands().Length, "Pre-ready observations cannot be replayed"), TimeSpan.FromMilliseconds(150)).ConfigureAwait(false);
            session.ApplySnapshot(Snapshot(GameStrength.High));
            await UntilAsync(() => fake.Count("vibration", 8) == 1, "fresh High dispatch").ConfigureAwait(false);
            Equal(0, fake.Count("stretch"), "Fresh vibration does not refresh the piston channel");
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
            await UntilAsync(() => fake.Count("stretch", 8) == 1, "fresh Fast dispatch").ConfigureAwait(false);
            var calls = fake.Commands();
            Equal("initialize", calls[0].Kind, "First call");
            Equal("stop", calls[1].Kind, "Baseline stop is the first output call");
            Equal(1, fake.Count("initialize"), "Repeated Start shares the original initialization");
            Equal(1, fake.MaxConcurrentOutputs, "Startup serializes output calls");
        }
        finally
        {
            initializing.Release();
            baseline.Release();
            await session.DisposeAsync().AsTask().WaitAsync(Deadline).ConfigureAwait(false);
        }
        Equal(1, fake.Count("dispose"), "Startup test cleanup");
    }

    private static async Task VibrationAndDeduplicationAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        session.Start();
        await ReadyAsync(log).ConfigureAwait(false);
        session.ApplySnapshot(Snapshot(GameStrength.Low));
        await UntilAsync(() => fake.Count("vibration", 4) == 1, "Low dispatch").ConfigureAwait(false);
        await CheckForWindowAsync(() =>
        {
            session.ApplySnapshot(Snapshot(GameStrength.Low));
            Equal(1, fake.Count("vibration", 4), "Repeated Low observations must not resend Low");
        }, TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        session.ApplySnapshot(Snapshot(GameStrength.High));
        await UntilAsync(() => fake.Count("vibration", 8) == 1, "High dispatch").ConfigureAwait(false);
        session.ApplySnapshot(Snapshot(GameStrength.Off));
        await UntilAsync(() => fake.Count("vibration", 0) == 1, "Off channel stop").ConfigureAwait(false);
        Equal(2, fake.PositiveCommands().Length, "Exactly Low and High positive commands");
        AssertNoPositiveStretch(fake);
    }

    private static Task PauseBarrierAsync() => GlobalStopRequiresFreshAsync(pistonOrigin: false);
    private static Task OffBarrierAsync() => StopBarrierAsync();
    private static Task SourceDisabledBarrierAsync() => SourceLossIndependentAsync(GameStateReason.SourceDisabled);

    private static async Task StopBarrierAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        session.Start();
        await ReadyAsync(log).ConfigureAwait(false);
        var inflight = fake.HoldNext("vibration", 4);
        try
        {
            session.ApplySnapshot(Snapshot(GameStrength.Low));
            await inflight.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            session.ApplySnapshot(Snapshot(GameStrength.Off));
            session.ApplySnapshot(Snapshot(GameStrength.High));
            inflight.Release();
            await UntilAsync(() => fake.Count("vibration", 8) == 1, "High after the stop barrier").ConfigureAwait(false);
            string[] outputs = fake.Commands().Where(call => call.Kind is "stop" or "vibration" or "stretch")
                .Select(call => call.Kind == "stop" ? "stop" : $"{call.Kind}:{call.Level}").ToArray();
            SequenceEqual(new[] { "stop", "vibration:4", "vibration:0", "vibration:8" }, outputs, "The stop cannot be coalesced away");
            AssertNoPositiveStretch(fake);
        }
        finally { inflight.Release(); }
    }

    private static async Task LatestValueWinsAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        session.Start();
        await ReadyAsync(log).ConfigureAwait(false);
        var inflight = fake.HoldNext("vibration", 4);
        try
        {
            session.ApplySnapshot(Snapshot(GameStrength.Low));
            await inflight.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            for (int index = 0; index < 100; index++)
                session.ApplySnapshot(Snapshot(index % 2 == 0 ? GameStrength.High : GameStrength.Low));
            session.ApplySnapshot(Snapshot(GameStrength.High));
            inflight.Release();
            await UntilAsync(() => fake.Count("vibration", 8) == 1, "coalesced final High").ConfigureAwait(false);
            SequenceEqual(new[] { 4, 8 }, fake.PositiveCommands().Select(call => call.Level!.Value).ToArray(), "Intermediate positive values are coalesced");
            Equal(1, fake.Count("stop"), "Positive-only changes do not add a stop");
            AssertNoPositiveStretch(fake);
        }
        finally { inflight.Release(); }
    }

    private static async Task WatchdogAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        session.Start();
        await ReadyAsync(log).ConfigureAwait(false);
        GameStateSnapshot observation = Snapshot(GameStrength.High);
        session.ApplySnapshot(observation);
        await UntilAsync(() => fake.Count("vibration", 8) == 1, "High before watchdog").ConfigureAwait(false);
        await UntilAsync(() => fake.Count("vibration", 0) == 1, "watchdog channel stop after one second without observations").ConfigureAwait(false);
        Command watchdogStop = fake.Commands().Single(call => call.Kind == "vibration" && call.Level == 0);
        double ageSeconds = (watchdogStop.Timestamp - observation.ObservedAt) / (double)Stopwatch.Frequency;
        Check(ageSeconds >= 1.0, $"Watchdog stopped too early, at observation age {ageSeconds:F6} seconds.");
        Equal(1, fake.Count("vibration", 8), "Watchdog must not replay motion");
        Equal(1, fake.Count("stop"), "Channel watchdog does not globally stop");
    }

    private static async Task ContinuousObservationsAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        session.Start();
        await ReadyAsync(log).ConfigureAwait(false);
        session.ApplySnapshot(Snapshot(GameStrength.High));
        await UntilAsync(() => fake.Count("vibration", 8) == 1, "initial High for heartbeat test").ConfigureAwait(false);
        await CheckForWindowAsync(() =>
        {
            session.ApplySnapshot(Snapshot(GameStrength.High));
            Equal(0, fake.Count("vibration", 0), "Fresh identical observations keep the watchdog alive");
            Equal(1, fake.Count("vibration", 8), "Heartbeat observations do not resend High");
        }, TimeSpan.FromMilliseconds(1250)).ConfigureAwait(false);
        session.ApplySnapshot(Snapshot(GameStrength.Off));
        await UntilAsync(() => fake.Count("vibration", 0) == 1, "explicit Off after continuously observed High").ConfigureAwait(false);
    }

    private static Task InitializeFailureAsync() => FailureAsync("initialize");
    private static Task StopFailureAsync() => FailureAsync("stop");
    private static Task VibrationFailureAsync() => FailureAsync("vibration");

    private static async Task FailureAsync(string failingOperation)
    {
        var fake = new FakeController();
        var log = new LogProbe();
        fake.FailNext(failingOperation, new InvalidOperationException("expected mock failure"));
        await using var session = new BooBoopGameSession(fake, log.Add);
        session.Start();
        if (failingOperation is "vibration" or "stretch")
        {
            await ReadyAsync(log).ConfigureAwait(false);
            if (failingOperation == "vibration") session.ApplySnapshot(Snapshot(GameStrength.High));
            else session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
        }
        await session.Completion.WaitAsync(Deadline).ConfigureAwait(false);
        Equal(1, fake.Count(failingOperation), "Failed operation is attempted once");
        Equal(1, fake.Count("initialize"), "No automatic reconnect");
        Equal(1, fake.Count("dispose"), "Failure cleans up once");
        AssertNoSubscriptions(fake);
        Equal(failingOperation is "vibration" or "stretch" ? 1 : 0, fake.PositiveCommands().Length, "Failed startup cannot dispatch motion");
        Check(log.Contains("No automatic retry"), "The diagnostic should disclose no automatic retry.");
        int commandCount = fake.Commands().Length;
        session.ApplySnapshot(Snapshot(GameStrength.High));
        session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
        Throws<ObjectDisposedException>(session.Start, "A failed session cannot restart itself");
        await session.DisposeAsync().ConfigureAwait(false);
        Equal(commandCount, fake.Commands().Length, "Later observations/disposal cannot replay or reclean a failed session");
    }

    private static async Task ReadinessLossAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        session.Start();
        await ReadyAsync(log).ConfigureAwait(false);
        session.ApplySnapshot(Snapshot(GameStrength.High));
        await UntilAsync(() => fake.Count("vibration", 8) == 1, "High before readiness loss").ConfigureAwait(false);
        fake.LoseReadiness();
        await session.Completion.WaitAsync(Deadline).ConfigureAwait(false);
        Equal(1, fake.Count("initialize"), "Readiness loss does not reconnect");
        Equal(1, fake.Count("dispose"), "Readiness loss cleans up once");
        Equal(1, fake.PositiveCommands().Length, "Readiness loss cannot replay motion");
        AssertNoSubscriptions(fake);
        int commandCount = fake.Commands().Length;
        session.ApplySnapshot(Snapshot(GameStrength.High));
        Throws<ObjectDisposedException>(session.Start, "A session that lost readiness cannot restart itself");
        await session.DisposeAsync().ConfigureAwait(false);
        Equal(commandCount, fake.Commands().Length, "Later observations do not restart a lost session");
    }

    private static async Task DisposeBeforeStartAsync()
    {
        var fake = new FakeController();
        var cleanup = fake.HoldNext("dispose");
        var session = new BooBoopGameSession(fake);
        try
        {
            session.ApplySnapshot(Snapshot(GameStrength.High));
            Task first = session.DisposeAsync().AsTask();
            await cleanup.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            Task second = session.DisposeAsync().AsTask();
            Check(ReferenceEquals(first, second), "Repeated DisposeAsync shares the same task.");
            Throws<ObjectDisposedException>(session.Start, "Start after disposal");
            Equal(0, fake.Count("initialize"), "Dispose before Start must not initialize");
            Equal(0, fake.PositiveCommands().Length, "Dispose before Start must not send motion");
            Equal(1, fake.Count("dispose"), "One cleanup invocation");
            Check(!first.IsCompleted, "Dispose awaits actual cleanup completion.");
            cleanup.Release();
            await Task.WhenAll(first, second).WaitAsync(Deadline).ConfigureAwait(false);
            await session.DisposeAsync().ConfigureAwait(false);
            Equal(1, fake.Count("dispose"), "Completed disposal remains idempotent");
            AssertNoSubscriptions(fake);
        }
        finally
        {
            cleanup.Release();
            await session.DisposeAsync().AsTask().WaitAsync(Deadline).ConfigureAwait(false);
        }
    }

    private static async Task DisposeDuringInitializationAsync()
    {
        var fake = new FakeController();
        var initializing = fake.HoldNext("initialize");
        var session = new BooBoopGameSession(fake);
        try
        {
            session.Start();
            await initializing.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            await session.DisposeAsync().AsTask().WaitAsync(Deadline).ConfigureAwait(false);
            Equal(1, fake.Count("initialize"), "Initialization started once");
            Equal(0, fake.Count("stop"), "Canceled initialization never reaches baseline stop");
            Equal(0, fake.PositiveCommands().Length, "Canceled initialization sends no motion");
            Equal(1, fake.Count("dispose"), "Canceled initialization cleans up once");
        }
        finally
        {
            initializing.Release();
            await session.DisposeAsync().AsTask().WaitAsync(Deadline).ConfigureAwait(false);
        }
    }

    private static async Task ConcurrentDisposeAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        var session = new BooBoopGameSession(fake, log.Add);
        var cleanup = fake.HoldNext("dispose");
        var inflight = fake.HoldNext("vibration", 8);
        try
        {
            session.Start();
            await ReadyAsync(log).ConfigureAwait(false);
            session.ApplySnapshot(Snapshot(GameStrength.High));
            await inflight.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            Task[] disposals = Enumerable.Range(0, 24)
                .Select(_ => Task.Run(async () => await session.DisposeAsync().ConfigureAwait(false)))
                .ToArray();
            await cleanup.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            Equal(1, fake.Count("dispose"), "All concurrent callers share a single cleanup");
            Check(disposals.All(task => !task.IsCompleted), "No caller completes before cleanup finishes.");
            cleanup.Release();
            await Task.WhenAll(disposals).WaitAsync(Deadline).ConfigureAwait(false);
            await session.DisposeAsync().ConfigureAwait(false);
            await session.DisposeAsync().ConfigureAwait(false);
            Equal(1, fake.Count("dispose"), "Repeated post-completion disposal does not repeat cleanup");
            Equal(1, fake.Count("initialize"), "Disposal never starts a replacement host");
            Equal(1, fake.PositiveCommands().Length, "Only the original inflight positive command was attempted");
            AssertNoSubscriptions(fake);
        }
        finally
        {
            cleanup.Release();
            inflight.Release();
            await session.DisposeAsync().AsTask().WaitAsync(Deadline).ConfigureAwait(false);
        }
    }

    private static async Task SubscriptionCleanupAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        var session = new BooBoopGameSession(fake, log.Add);
        try
        {
            Equal(1, fake.DiagnosticSubscriberCount, "Session subscribes to controller diagnostics");
            Equal(1, fake.ErrorSubscriberCount, "Session subscribes to controller errors");
            session.Start();
            await ReadyAsync(log).ConfigureAwait(false);
            fake.EmitDiagnostic("subscribed diagnostic probe");
            fake.EmitError("subscribed error probe");
            Check(log.Contains("subscribed diagnostic probe"), "Diagnostics reach the live session.");
            Check(log.Contains("subscribed error probe"), "Errors reach the live session.");
            await session.DisposeAsync().AsTask().WaitAsync(Deadline).ConfigureAwait(false);
            AssertNoSubscriptions(fake);
            int linesAfterDisposal = log.Count;
            fake.EmitDiagnostic("detached diagnostic probe");
            fake.EmitError("detached error probe");
            Equal(linesAfterDisposal, log.Count, "Disposed sessions receive no controller callbacks");
        }
        finally { await session.DisposeAsync().AsTask().WaitAsync(Deadline).ConfigureAwait(false); }
    }

    private static async Task ThrowingLoggerAsync()
    {
        var fake = new FakeController();
        var session = new BooBoopGameSession(fake, _ => throw new InvalidOperationException("expected logger failure"));
        try
        {
            session.Start();
            await UntilAsync(() => fake.Count("stop") >= 1, "baseline with throwing logger").ConfigureAwait(false);
            // Disposal itself must remain safe even when every diagnostic callback throws.
            await session.DisposeAsync().AsTask().WaitAsync(Deadline).ConfigureAwait(false);
            Equal(1, fake.Count("dispose"), "Throwing logger does not prevent cleanup");
        }
        finally { await session.DisposeAsync().AsTask().WaitAsync(Deadline).ConfigureAwait(false); }
    }

    private static GameStateSnapshot Snapshot(GameStrength strength, bool active = true,
        GameStateReason reason = GameStateReason.Normal) => new(active, strength, reason, 1, Stopwatch.GetTimestamp());

    private static Task ReadyAsync(LogProbe log) => UntilAsync(() => log.Contains("Connected; waiting for a fresh"), "initialization and baseline stop completion");

    private static async Task UntilAsync(Func<bool> predicate, string description)
    {
        var elapsed = Stopwatch.StartNew();
        while (!predicate())
        {
            if (elapsed.Elapsed >= Deadline)
                throw new TimeoutException($"Timed out waiting for {description}.");
            await Task.Delay(5).ConfigureAwait(false);
        }
    }

    // These are intentional absence/freshness observation windows, not synchronization sleeps.
    // Every tick checks the invariant and (for heartbeat tests) publishes another observation.
    private static async Task CheckForWindowAsync(Action check, TimeSpan duration)
    {
        var elapsed = Stopwatch.StartNew();
        do
        {
            check();
            await Task.Delay(10).ConfigureAwait(false);
        } while (elapsed.Elapsed < duration);
        check();
    }

    private static void AssertNoPositiveStretch(FakeController fake) =>
        Check(!fake.Commands().Any(call => call.Kind == "stretch" && call.Level > 0), "Vibration-only observations must not send positive stretch.");

    private static void AssertNoSubscriptions(FakeController fake)
    {
        Equal(0, fake.DiagnosticSubscriberCount, "Cleanup removes the diagnostic subscription");
        Equal(0, fake.ErrorSubscriberCount, "Cleanup removes the error subscription");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{description}: expected {expected}, actual {actual}.");
    }

    private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string description)
    {
        T[] expectedItems = expected.ToArray(), actualItems = actual.ToArray();
        if (!expectedItems.SequenceEqual(actualItems))
            throw new InvalidOperationException($"{description}: expected [{string.Join(", ", expectedItems)}], actual [{string.Join(", ", actualItems)}].");
    }

    private static void Throws<T>(Action action, string description) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"{description}: expected {typeof(T).Name}.");
    }
}

internal sealed class LogProbe
{
    private readonly object _gate = new();
    private readonly List<string> _lines = new();
    public int Count { get { lock (_gate) return _lines.Count; } }
    public void Add(string line) { lock (_gate) _lines.Add(line); }
    public bool Contains(string text) { lock (_gate) return _lines.Any(line => line.Contains(text, StringComparison.Ordinal)); }
}

internal readonly record struct Command(string Kind, int? Level, long Timestamp);

internal sealed class OperationGate
{
    private readonly TaskCompletionSource<Command> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<Command> Entered => _entered.Task;
    public void Release() => _release.TrySetResult(true);
    public Task WaitAsync(Command command, CancellationToken cancellationToken)
    {
        _entered.TrySetResult(command);
        return _release.Task.WaitAsync(cancellationToken);
    }
}

// The only IBooBoopController implementation instantiated by this executable.
// Stop/dispose calls are boundary observations, not claims of physical device safety.
internal sealed class FakeController : IBooBoopController
{
    private readonly object _gate = new();
    private readonly List<Command> _commands = new();
    private readonly List<(string Kind, int? Level, OperationGate Gate)> _holds = new();
    private readonly List<(string Kind, Exception Error)> _failures = new();
    private bool _ready;
    private int _activeOutputs, _maxConcurrentOutputs;
    public int MaxConcurrentOutputs { get { lock (_gate) return _maxConcurrentOutputs; } }

    public bool IsReady { get { lock (_gate) return _ready; } }
    public event Action<string>? Diagnostic;
    public event Action<string>? Error;
    public int DiagnosticSubscriberCount => Diagnostic?.GetInvocationList().Length ?? 0;
    public int ErrorSubscriberCount => Error?.GetInvocationList().Length ?? 0;
    public void EmitDiagnostic(string message) => Diagnostic?.Invoke(message);
    public void EmitError(string message) => Error?.Invoke(message);
    public void LoseReadiness()
    {
        lock (_gate) _ready = false;
        EmitError("mock readiness lost");
    }

    public OperationGate HoldNext(string kind, int? level = null)
    {
        var hold = new OperationGate();
        lock (_gate) _holds.Add((kind, level, hold));
        return hold;
    }

    public void FailNext(string kind, Exception error) { lock (_gate) _failures.Add((kind, error)); }
    public Command[] Commands() { lock (_gate) return _commands.ToArray(); }
    public int Count(string kind, int? level = null) => Commands().Count(call => call.Kind == kind && (!level.HasValue || call.Level == level));
    public Command[] PositiveCommands() => Commands().Where(call => (call.Kind is "vibration" or "stretch") && call.Level > 0).ToArray();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RecordAsync("initialize", null, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) _ready = true;
        Diagnostic?.Invoke("mock ready");
    }

    public Task SetStretchAsync(int level, CancellationToken cancellationToken = default) => RecordAsync("stretch", level, cancellationToken);
    public Task SetVibrationAsync(int level, CancellationToken cancellationToken = default) => RecordAsync("vibration", level, cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken = default) => RecordAsync("stop", null, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        lock (_gate) _ready = false;
        // Cleanup has its own gate and is deliberately independent of the canceled run token.
        await RecordAsync("dispose", null, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task RecordAsync(string kind, int? level, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var command = new Command(kind, level, Stopwatch.GetTimestamp());
        OperationGate? hold = null;
        Exception? failure = null;
        lock (_gate)
        {
            _commands.Add(command);
            if (kind is "stop" or "vibration" or "stretch")
            {
                _activeOutputs++;
                _maxConcurrentOutputs = Math.Max(_maxConcurrentOutputs, _activeOutputs);
            }
            int holdIndex = _holds.FindIndex(item => item.Kind == kind && (!item.Level.HasValue || item.Level == level));
            if (holdIndex >= 0)
            {
                hold = _holds[holdIndex].Gate;
                _holds.RemoveAt(holdIndex);
            }
            int failureIndex = _failures.FindIndex(item => item.Kind == kind);
            if (failureIndex >= 0)
            {
                failure = _failures[failureIndex].Error;
                _failures.RemoveAt(failureIndex);
            }
        }
        try
        {
            if (hold is not null) await hold.WaitAsync(command, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (failure is not null)
            {
                Error?.Invoke($"mock {kind} failure");
                throw failure;
            }
        }
        finally
        {
            if (kind is "stop" or "vibration" or "stretch")
                lock (_gate) _activeOutputs--;
        }
    }
}
