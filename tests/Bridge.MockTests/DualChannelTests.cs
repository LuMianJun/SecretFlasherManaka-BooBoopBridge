using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BooBoopBridge;
using SecretFlasherManaka.BooBoopBridge;
using SecretFlasherManaka.ForEveryThing;

namespace Bridge.MockTests;

internal static partial class Program
{
    private static PistonStateSnapshot PistonSnapshot(PistonMode mode, bool active = true,
        GameStateReason reason = GameStateReason.Normal) => new(active, mode, reason, 1, Stopwatch.GetTimestamp());

    private static Task PistonHubObservationsAsync()
    {
        PistonStateHub.Publish(false, PistonMode.Off, GameStateReason.SourceUnavailable);
        var observed = new List<(PistonStateSnapshot Snapshot, bool Stop)>();
        var changed = new List<PistonStateSnapshot>();
        Action<PistonStateSnapshot, bool> onObserved = (snapshot, stop) => observed.Add((snapshot, stop));
        Action<PistonStateSnapshot> onChanged = snapshot => changed.Add(snapshot);
        PistonStateHub.Observed += onObserved;
        PistonStateHub.StateChanged += onChanged;
        try
        {
            PistonStateHub.Publish(true, PistonMode.Slow, GameStateReason.Normal);
            PistonStateHub.Publish(true, PistonMode.Slow, GameStateReason.Normal);
            PistonStateHub.Publish(true, PistonMode.Slow, GameStateReason.Normal, forceStop: true);
            Equal(3, observed.Count, "Piston hub publishes unchanged frames and stop barriers");
            Equal(1, changed.Count, "Unchanged piston semantics do not repeat StateChanged");
            Check(observed.All(item => item.Snapshot.Revision == changed[0].Revision), "Unchanged piston frames retain revision.");
            Check(!observed[0].Stop && !observed[1].Stop && observed[2].Stop, "Piston forceStop is preserved.");
            for (int index = 1; index < observed.Count; index++)
                Check(observed[index].Snapshot.ObservedAt >= observed[index - 1].Snapshot.ObservedAt, "Piston timestamps are monotonic.");
            PistonStateHub.Publish(true, PistonMode.Medium, GameStateReason.Normal);
            Equal(2, changed.Count, "Changed piston mode publishes a semantic change");
            Equal(changed[0].Revision + 1, changed[1].Revision, "Piston revision increments once");
            PistonStateHub.Publish(false, PistonMode.Unknown, GameStateReason.SourceDisabled);
            Equal(3, changed.Count, "Piston source loss changes semantic state");
            Equal(observed.Last().Snapshot, PistonStateHub.Current, "Current is the latest piston observation");
        }
        finally
        {
            PistonStateHub.Observed -= onObserved;
            PistonStateHub.StateChanged -= onChanged;
        }
        return Task.CompletedTask;
    }

    private static Task PistonHubSubscriberIsolationAsync()
    {
        PistonStateHub.Publish(false, PistonMode.Off, GameStateReason.SourceUnavailable);
        int healthyObserved = 0, healthyChanged = 0, reported = 0;
        Action<PistonStateSnapshot, bool> badObserved = (_, _) => throw new InvalidOperationException("expected piston observation failure");
        Action<PistonStateSnapshot, bool> goodObserved = (_, _) => healthyObserved++;
        Action<PistonStateSnapshot> badChanged = _ => throw new InvalidOperationException("expected piston change failure");
        Action<PistonStateSnapshot> goodChanged = _ => healthyChanged++;
        Action<Exception>? previousReporter = PistonStateHub.SubscriberError;
        PistonStateHub.SubscriberError = _ => reported++;
        PistonStateHub.Observed += badObserved;
        PistonStateHub.Observed += goodObserved;
        PistonStateHub.StateChanged += badChanged;
        PistonStateHub.StateChanged += goodChanged;
        try
        {
            PistonStateHub.Publish(true, PistonMode.Slow, GameStateReason.Normal);
            Equal(1, healthyObserved, "Healthy piston observation handler runs");
            Equal(1, healthyChanged, "Healthy piston change handler runs");
            Equal(2, reported, "Both failed piston subscribers are reported");
            PistonStateHub.SubscriberError = _ => throw new InvalidOperationException("expected piston reporter failure");
            PistonStateHub.Publish(true, PistonMode.Fast, GameStateReason.Normal);
            Equal(2, healthyObserved, "Throwing reporter does not block piston observations");
            Equal(2, healthyChanged, "Throwing reporter does not block piston changes");
        }
        finally
        {
            PistonStateHub.Observed -= badObserved;
            PistonStateHub.Observed -= goodObserved;
            PistonStateHub.StateChanged -= badChanged;
            PistonStateHub.StateChanged -= goodChanged;
            PistonStateHub.SubscriberError = previousReporter;
        }
        return Task.CompletedTask;
    }

    private static async Task StartBothAsync(BooBoopGameSession session, FakeController fake, LogProbe log)
    {
        session.Start();
        await ReadyAsync(log).ConfigureAwait(false);
        session.ApplySnapshot(Snapshot(GameStrength.Low));
        session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Slow));
        await UntilAsync(() => fake.Count("vibration", 4) == 1 && fake.Count("stretch", 4) == 1,
            "independent initial Low and Slow").ConfigureAwait(false);
    }

    private static async Task BothOutputsAndDeduplicationAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        await StartBothAsync(session, fake, log).ConfigureAwait(false);
        await CheckForWindowAsync(() =>
        {
            session.ApplySnapshot(Snapshot(GameStrength.Low));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Slow));
            Equal(1, fake.Count("vibration", 4), "Stable vibration is not resent");
            Equal(1, fake.Count("stretch", 4), "Stable piston is not resent");
        }, TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Medium));
        await UntilAsync(() => fake.Count("stretch", 6) == 1, "independent Medium change").ConfigureAwait(false);
        Equal(1, fake.Count("vibration"), "Piston change does not reissue vibration");
        session.ApplySnapshot(Snapshot(GameStrength.High));
        await UntilAsync(() => fake.Count("vibration", 8) == 1, "independent High change").ConfigureAwait(false);
        Equal(2, fake.Count("stretch"), "Vibration change does not reissue piston");
        Equal(1, fake.Count("stop"), "No global stop for positive independent updates");
        Equal(1, fake.MaxConcurrentOutputs, "Output operations are always awaited serially");
    }

    private static async Task PistonModesAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        session.Start();
        await ReadyAsync(log).ConfigureAwait(false);
        foreach (var mapping in new[] { (PistonMode.Slow, 4), (PistonMode.Medium, 6), (PistonMode.Fast, 8), (PistonMode.Off, 0) })
        {
            session.ApplyPistonSnapshot(PistonSnapshot(mapping.Item1));
            await UntilAsync(() => fake.Count("stretch", mapping.Item2) == 1, $"piston {mapping.Item1}").ConfigureAwait(false);
        }
        SequenceEqual(new[] { 4, 6, 8, 0 }, Levels(fake, "stretch"), "Piston mode dispatch values");
        Equal(0, fake.Count("vibration"), "Piston-only observations cannot move vibration");
        Equal(1, fake.Count("stop"), "Piston Off uses a channel stop");
    }

    private static async Task IndependentOffAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        await StartBothAsync(session, fake, log).ConfigureAwait(false);
        session.ApplySnapshot(Snapshot(GameStrength.Off));
        await UntilAsync(() => fake.Count("vibration", 0) == 1, "vibration Off").ConfigureAwait(false);
        SequenceEqual(new[] { 4 }, Levels(fake, "stretch"), "Vibration Off leaves piston unchanged");
        session.ApplySnapshot(Snapshot(GameStrength.High));
        await UntilAsync(() => fake.Count("vibration", 8) == 1, "vibration restarts independently").ConfigureAwait(false);
        session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Off));
        await UntilAsync(() => fake.Count("stretch", 0) == 1, "piston Off").ConfigureAwait(false);
        SequenceEqual(new[] { 4, 0, 8 }, Levels(fake, "vibration"), "Piston Off leaves vibration unchanged");
        Equal(1, fake.Count("stop"), "Neither channel Off calls global stop");
    }

    private static async Task BothOffAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        await StartBothAsync(session, fake, log).ConfigureAwait(false);
        session.ApplySnapshot(Snapshot(GameStrength.Off));
        session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Off));
        await UntilAsync(() => fake.Count("vibration", 0) == 1 && fake.Count("stretch", 0) == 1, "both independent Off requests").ConfigureAwait(false);
        await CheckForWindowAsync(() =>
        {
            session.ApplySnapshot(Snapshot(GameStrength.Off));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Off));
            Equal(1, fake.Count("vibration", 0), "Stable vibration Off deduplicates");
            Equal(1, fake.Count("stretch", 0), "Stable piston Off deduplicates");
            Equal(1, fake.Count("stop"), "Both ordinary Off requests do not become a global lifecycle stop");
        }, TimeSpan.FromMilliseconds(150)).ConfigureAwait(false);
    }

    private static async Task SourceLossIndependentAsync(GameStateReason reason)
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        await StartBothAsync(session, fake, log).ConfigureAwait(false);
        session.ApplySnapshot(Snapshot(GameStrength.Unknown, active: false, reason: reason));
        await UntilAsync(() => fake.Count("vibration", 0) == 1, $"vibration {reason}").ConfigureAwait(false);
        SequenceEqual(new[] { 4 }, Levels(fake, "stretch"), "Vibration source loss leaves piston alone");
        session.ApplySnapshot(Snapshot(GameStrength.Low));
        await UntilAsync(() => fake.Count("vibration", 4) == 2, "fresh vibration source recovery").ConfigureAwait(false);
        session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Unknown, active: false, reason: reason));
        await UntilAsync(() => fake.Count("stretch", 0) == 1, $"piston {reason}").ConfigureAwait(false);
        SequenceEqual(new[] { 4, 0, 4 }, Levels(fake, "vibration"), "Piston source loss leaves vibration alone");
        Equal(1, fake.Count("stop"), "Source loss without forceStop is channel-specific");
    }

    private static async Task InvalidStatesIndependentAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        await StartBothAsync(session, fake, log).ConfigureAwait(false);
        int zeroCount = 0, positiveCount = 1;
        foreach (var invalid in new[] { (GameStrength.Unknown, true), ((GameStrength)123, true), (GameStrength.High, false) })
        {
            session.ApplySnapshot(Snapshot(invalid.Item1, invalid.Item2, GameStateReason.InvalidValue));
            zeroCount++;
            await UntilAsync(() => fake.Count("vibration", 0) == zeroCount, "invalid vibration channel zero").ConfigureAwait(false);
            SequenceEqual(new[] { 4 }, Levels(fake, "stretch"), "Invalid vibration does not alter piston");
            session.ApplySnapshot(Snapshot(GameStrength.Low));
            positiveCount++;
            await UntilAsync(() => fake.Count("vibration", 4) == positiveCount, "vibration recovery").ConfigureAwait(false);
        }
        int vibrationWrites = fake.Count("vibration");
        zeroCount = 0;
        positiveCount = 1;
        foreach (var invalid in new[] { (PistonMode.Unknown, true), ((PistonMode)123, true), (PistonMode.Fast, false) })
        {
            session.ApplyPistonSnapshot(PistonSnapshot(invalid.Item1, invalid.Item2, GameStateReason.InvalidValue));
            zeroCount++;
            await UntilAsync(() => fake.Count("stretch", 0) == zeroCount, "invalid piston channel zero").ConfigureAwait(false);
            Equal(vibrationWrites, fake.Count("vibration"), "Invalid piston does not alter vibration");
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Slow));
            positiveCount++;
            await UntilAsync(() => fake.Count("stretch", 4) == positiveCount, "piston recovery").ConfigureAwait(false);
        }
        Equal(1, fake.Count("stop"), "Invalid channel values are not global lifecycle barriers");
        await SourceLossIndependentAsync(GameStateReason.SourceUnavailable).ConfigureAwait(false);
    }

    private static async Task PistonOffBarrierAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        session.Start();
        await ReadyAsync(log).ConfigureAwait(false);
        var inflight = fake.HoldNext("stretch", 4);
        try
        {
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Slow));
            await inflight.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Off));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
            inflight.Release();
            await UntilAsync(() => fake.Count("stretch", 8) == 1, "Fast after piston Off barrier").ConfigureAwait(false);
            SequenceEqual(new[] { 4, 0, 8 }, Levels(fake, "stretch"), "Piston Off cannot be coalesced away");
            Equal(1, fake.Count("stop"), "Piston barrier does not globally stop");
            Equal(0, fake.Count("vibration"), "Piston barrier does not touch vibration");
        }
        finally { inflight.Release(); }
    }

    private static async Task BothOffBarriersAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        await StartBothAsync(session, fake, log).ConfigureAwait(false);
        var inflight = fake.HoldNext("vibration", 8);
        try
        {
            session.ApplySnapshot(Snapshot(GameStrength.High));
            await inflight.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            session.ApplySnapshot(Snapshot(GameStrength.Off));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Off));
            session.ApplySnapshot(Snapshot(GameStrength.Low));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
            inflight.Release();
            await UntilAsync(() => fake.Count("vibration", 4) == 2 && fake.Count("stretch", 8) == 1, "both channels resume after their barriers").ConfigureAwait(false);
            SequenceEqual(new[] { 4, 8, 0, 4 }, Levels(fake, "vibration"), "Vibration zero survives positive coalescing");
            SequenceEqual(new[] { 4, 0, 8 }, Levels(fake, "stretch"), "Piston zero survives positive coalescing");
            var tail = fake.Commands().SkipWhile(call => !(call.Kind == "vibration" && call.Level == 8)).Skip(1).ToArray();
            Equal(2, tail.TakeWhile(call => call.Level == 0).Count(), "All pending channel stops precede any new positive command");
            Equal(1, fake.Count("stop"), "Both channel barriers remain independent");
            Equal(1, fake.MaxConcurrentOutputs, "No overlapping output calls");
        }
        finally { inflight.Release(); }
    }

    private static Task PistonGlobalStopAsync() => GlobalStopRequiresFreshAsync(pistonOrigin: true);

    private static async Task GlobalStopRequiresFreshAsync(bool pistonOrigin)
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        await StartBothAsync(session, fake, log).ConfigureAwait(false);
        var inflight = fake.HoldNext("vibration", 8);
        var globalStop = fake.HoldNext("stop");
        try
        {
            session.ApplySnapshot(Snapshot(GameStrength.High));
            await inflight.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            if (pistonOrigin)
                session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Off, false, GameStateReason.Paused), forceStop: true);
            else
                session.ApplySnapshot(Snapshot(GameStrength.Off, false, GameStateReason.Paused), forceStop: true);
            // Neither an immediate positive nor a frame while StopAsync is pending may erase the global barrier.
            session.ApplySnapshot(Snapshot(GameStrength.High));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
            inflight.Release();
            await globalStop.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            Equal(3, fake.PositiveCommands().Length, "Global stop precedes queued positive values");
            session.ApplySnapshot(Snapshot(GameStrength.High));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
            globalStop.Release();
            await UntilAsync(() => log.Contains("All-stop completed; waiting for fresh observations"), "global stop and both freshness cutoffs").ConfigureAwait(false);
            await CheckForWindowAsync(() => Equal(3, fake.PositiveCommands().Length,
                "Global stop cannot replay either channel's pre-completion frames"), TimeSpan.FromMilliseconds(150)).ConfigureAwait(false);
            session.ApplySnapshot(Snapshot(GameStrength.High));
            await UntilAsync(() => fake.Count("vibration", 8) == 2, "post-stop vibration observation").ConfigureAwait(false);
            Equal(0, fake.Count("stretch", 8), "A fresh vibration frame does not rearm piston");
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
            await UntilAsync(() => fake.Count("stretch", 8) == 1, "post-stop piston observation").ConfigureAwait(false);
            Equal(2, fake.Count("stop"), "Exactly baseline and explicit global stop");
            Equal(1, fake.MaxConcurrentOutputs, "Global stop is serialized with both writes");
        }
        finally
        {
            inflight.Release();
            globalStop.Release();
        }
    }

    private static Task HeldVibrationRereadsBothAsync() => HeldWriteRereadsBothAsync(holdPiston: false);
    private static Task HeldPistonRereadsBothAsync() => HeldWriteRereadsBothAsync(holdPiston: true);

    private static async Task HeldWriteRereadsBothAsync(bool holdPiston)
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        await StartBothAsync(session, fake, log).ConfigureAwait(false);
        string heldKind = holdPiston ? "stretch" : "vibration";
        string otherKind = holdPiston ? "vibration" : "stretch";
        int nextLevel = holdPiston ? 6 : 4;
        var inflight = fake.HoldNext(heldKind, 8);
        try
        {
            if (holdPiston) session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
            else session.ApplySnapshot(Snapshot(GameStrength.High));
            await inflight.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            int pendingCount = fake.Commands().Length;
            if (holdPiston)
            {
                session.ApplySnapshot(Snapshot(GameStrength.Off));
                session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Medium));
            }
            else
            {
                session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Off));
                session.ApplySnapshot(Snapshot(GameStrength.Low));
            }
            await CheckForWindowAsync(() => Equal(pendingCount, fake.Commands().Length,
                "Worker awaits the current output before dispatching either channel"), TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
            inflight.Release();
            await UntilAsync(() => fake.Count(otherKind, 0) == 1 && fake.Count(heldKind, nextLevel) == (holdPiston ? 1 : 2),
                "fresh mailbox values after held write").ConfigureAwait(false);
            var tail = fake.Commands().Skip(pendingCount).Select(FormatCommand).ToArray();
            SequenceEqual(new[] { $"{otherKind}:0", $"{heldKind}:{nextLevel}" }, tail,
                "Pending other-channel stop takes priority over refreshed positive value");
            Equal(1, fake.Count("stop"), "Crossed channel stop is not global");
            Equal(1, fake.MaxConcurrentOutputs, "Held output never overlaps another output");
        }
        finally { inflight.Release(); }
    }

    private static async Task CrossChannelCoalescingAsync()
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
            {
                session.ApplyPistonSnapshot(PistonSnapshot(index % 2 == 0 ? PistonMode.Slow : PistonMode.Fast));
                session.ApplySnapshot(Snapshot(index % 2 == 0 ? GameStrength.Low : GameStrength.High));
            }
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Medium));
            session.ApplySnapshot(Snapshot(GameStrength.High));
            Equal(0, fake.Count("stretch"), "Other channel is not dispatched during held write");
            inflight.Release();
            await UntilAsync(() => fake.Count("stretch", 6) == 1 && fake.Count("vibration", 8) == 1, "newest values on both channels").ConfigureAwait(false);
            SequenceEqual(new[] { 4, 8 }, Levels(fake, "vibration"), "Vibration retains newest positive only");
            SequenceEqual(new[] { 6 }, Levels(fake, "stretch"), "Piston rereads mailbox before first dispatch");
            Equal(1, fake.MaxConcurrentOutputs, "Cross-channel dispatches are serialized");
        }
        finally { inflight.Release(); }
    }

    private static Task VibrationStaleIndependentAsync() => IndependentWatchdogAsync(stalePiston: false);
    private static Task PistonStaleIndependentAsync() => IndependentWatchdogAsync(stalePiston: true);

    private static async Task IndependentWatchdogAsync(bool stalePiston)
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        await StartBothAsync(session, fake, log).ConfigureAwait(false);
        long lastObservation;
        if (stalePiston)
        {
            var observation = PistonSnapshot(PistonMode.Slow);
            lastObservation = observation.ObservedAt;
            session.ApplyPistonSnapshot(observation);
        }
        else
        {
            var observation = Snapshot(GameStrength.Low);
            lastObservation = observation.ObservedAt;
            session.ApplySnapshot(observation);
        }
        string staleKind = stalePiston ? "stretch" : "vibration";
        string freshKind = stalePiston ? "vibration" : "stretch";
        await CheckForWindowAsync(() =>
        {
            if (stalePiston) session.ApplySnapshot(Snapshot(GameStrength.Low));
            else session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Slow));
            Equal(0, fake.Count(freshKind, 0), "Other channel's independent heartbeat prevents its own stop");
            Equal(1, fake.Count("stop"), "Per-channel staleness must not issue global stop");
        }, TimeSpan.FromMilliseconds(1300)).ConfigureAwait(false);
        await UntilAsync(() => fake.Count(staleKind, 0) == 1, "stale channel zero").ConfigureAwait(false);
        var stopped = fake.Commands().Single(call => call.Kind == staleKind && call.Level == 0);
        Check((stopped.Timestamp - lastObservation) / (double)Stopwatch.Frequency >= 1.0, "Independent watchdog does not stop early.");
        SequenceEqual(new[] { 4 }, Levels(fake, freshKind), "Fresh other channel is neither stopped nor resent");
        Equal(1, fake.Count(staleKind, 4), "Watchdog does not replay stale output");
        if (stalePiston) session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Slow));
        else session.ApplySnapshot(Snapshot(GameStrength.Low));
        await UntilAsync(() => fake.Count(staleKind, 4) == 2, "fresh observation resumes only stale channel").ConfigureAwait(false);
    }

    private static async Task BothContinuousObservationsAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        await using var session = new BooBoopGameSession(fake, log.Add);
        await StartBothAsync(session, fake, log).ConfigureAwait(false);
        await CheckForWindowAsync(() =>
        {
            session.ApplySnapshot(Snapshot(GameStrength.Low));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Slow));
            Equal(2, fake.PositiveCommands().Length, "Unchanged independent frames sustain both outputs without resending");
            Equal(0, fake.Count("vibration", 0), "Fresh vibration never expires");
            Equal(0, fake.Count("stretch", 0), "Fresh piston never expires");
            Equal(1, fake.Count("stop"), "Fresh independent frames never globally stop");
        }, TimeSpan.FromMilliseconds(1250)).ConfigureAwait(false);
    }

    private static async Task TimestampFreshnessAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        var beforeReadyVibration = Snapshot(GameStrength.High);
        var beforeReadyPiston = PistonSnapshot(PistonMode.Fast);
        await using var session = new BooBoopGameSession(fake, log.Add);
        session.Start();
        await ReadyAsync(log).ConfigureAwait(false);
        session.ApplySnapshot(beforeReadyVibration);
        session.ApplyPistonSnapshot(beforeReadyPiston);
        await CheckForWindowAsync(() => Equal(0, fake.PositiveCommands().Length,
            "Delivering old timestamps after ready cannot make them fresh"), TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
        await UntilAsync(() => fake.Count("stretch", 8) == 1, "fresh piston alone").ConfigureAwait(false);
        Equal(0, fake.Count("vibration", 8), "Fresh piston cannot refresh old vibration timestamp");
        session.ApplySnapshot(Snapshot(GameStrength.High));
        await UntilAsync(() => fake.Count("vibration", 8) == 1, "fresh vibration independently").ConfigureAwait(false);
        Equal(1, fake.MaxConcurrentOutputs, "Ready channel writes are serialized");
    }

    private static Task PistonFailureAsync() => FailureAsync("stretch");

    private static async Task ConcurrentWriteFailureAsync()
    {
        var fake = new FakeController();
        var log = new LogProbe();
        var session = new BooBoopGameSession(fake, log.Add);
        var inflight = fake.HoldNext("stretch", 8);
        var cleanup = fake.HoldNext("dispose");
        try
        {
            await StartBothAsync(session, fake, log).ConfigureAwait(false);
            fake.FailNext("stretch", new InvalidOperationException("expected held piston write failure"));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
            await inflight.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            session.ApplySnapshot(Snapshot(GameStrength.High));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Medium));
            inflight.Release();
            await cleanup.Entered.WaitAsync(Deadline).ConfigureAwait(false);
            Task[] disposals = Enumerable.Range(0, 24).Select(_ => Task.Run(async () => await session.DisposeAsync().ConfigureAwait(false))).ToArray();
            Equal(1, fake.Count("dispose"), "Write failure starts exactly one cleanup");
            Check(disposals.All(task => !task.IsCompleted), "Concurrent disposal waits for failed-write cleanup.");
            session.ApplySnapshot(Snapshot(GameStrength.High));
            session.ApplyPistonSnapshot(PistonSnapshot(PistonMode.Fast));
            cleanup.Release();
            await Task.WhenAll(disposals).WaitAsync(Deadline).ConfigureAwait(false);
            await session.Completion.WaitAsync(Deadline).ConfigureAwait(false);
            Equal(0, fake.Count("vibration", 8), "Queued vibration is not sent after piston failure");
            Equal(0, fake.Count("stretch", 6), "Queued piston is not retried after failure");
            Equal(1, fake.Count("stretch", 8), "Failed write is attempted only once");
            Equal(1, fake.Count("initialize"), "Concurrent cleanup does not reconnect");
            Equal(1, fake.Count("dispose"), "Concurrent failure and disposal share cleanup");
            Equal(1, fake.MaxConcurrentOutputs, "Failure path never overlaps output calls");
            AssertNoSubscriptions(fake);
        }
        finally
        {
            inflight.Release();
            cleanup.Release();
            await session.DisposeAsync().AsTask().WaitAsync(Deadline).ConfigureAwait(false);
        }
    }

    private static Task VibrationEpochAsync() => IndependentEpochAsync(stopPiston: false);
    private static Task StretchEpochAsync() => IndependentEpochAsync(stopPiston: true);

    private static Task IndependentEpochAsync(bool stopPiston)
    {
        var epochs = new MotionEpochs();
        string stoppedMode = stopPiston ? "aa01" : "bb01";
        string otherMode = stopPiston ? "bb01" : "aa01";
        MotionTicket oldStopped = epochs.Capture(stoppedMode, stop: false);
        MotionTicket oldOther = epochs.Capture(otherMode, stop: false);
        Check(epochs.IsCurrent(oldStopped) && epochs.IsCurrent(oldOther), "Initial queue tickets are current.");
        MotionTicket stopTicket = epochs.Capture(stoppedMode, stop: true);
        Check(!epochs.IsCurrent(oldStopped), "Channel zero invalidates older writes on its own channel.");
        Check(epochs.IsCurrent(oldOther), "Channel zero preserves already queued other-channel writes.");
        Check(epochs.IsCurrent(stopTicket), "The newly captured zero ticket remains current.");
        MotionTicket newPositive = epochs.Capture(stoppedMode, stop: false);
        Check(epochs.IsCurrent(newPositive), "A new positive command after channel zero is permitted.");
        Check(epochs.IsCurrent(stopTicket), "A later positive command cannot invalidate its preceding stop.");
        Check(epochs.IsCurrent(oldOther), "Recovery still does not invalidate the other channel.");
        Throws<ArgumentOutOfRangeException>(() => epochs.Capture("unrecognized", stop: false), "Unknown native mode is rejected");
        return Task.CompletedTask;
    }

    private static Task GlobalEpochAsync()
    {
        var epochs = new MotionEpochs();
        MotionTicket oldVibration = epochs.Capture("bb01", stop: false);
        MotionTicket oldStretch = epochs.Capture("aa01", stop: false);
        epochs.StopAll();
        Check(!epochs.IsCurrent(oldVibration), "Global stop invalidates queued vibration.");
        Check(!epochs.IsCurrent(oldStretch), "Global stop invalidates queued stretch.");
        MotionTicket newVibration = epochs.Capture("bb01", stop: false);
        MotionTicket newStretch = epochs.Capture("aa01", stop: false);
        Check(epochs.IsCurrent(newVibration) && epochs.IsCurrent(newStretch), "New post-stop tickets on both channels are accepted.");
        epochs.StopAll();
        Check(!epochs.IsCurrent(newVibration) && !epochs.IsCurrent(newStretch), "A second global stop invalidates both new tickets.");
        return Task.CompletedTask;
    }

    private static int[] Levels(FakeController fake, string kind) => fake.Commands()
        .Where(call => call.Kind == kind).Select(call => call.Level!.Value).ToArray();

    private static string FormatCommand(Command command) => command.Level.HasValue ? $"{command.Kind}:{command.Level.Value}" : command.Kind;
}
