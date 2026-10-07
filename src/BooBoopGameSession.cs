using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BooBoopBridge;
using SecretFlasherManaka.ForEveryThing;

namespace SecretFlasherManaka.BooBoopBridge;

// One bounded mailbox per output and one awaited transport worker. No Unity objects cross threads.
public sealed class BooBoopGameSession : IAsyncDisposable
{
    private sealed class ChannelState
    {
        public int Latest, Sent;
        public long ObservedAt, FreshAfter;
        public bool StopPending, NeedsFresh = true;
        public string LastDiagnostic = "";
    }
    private readonly object _gate = new();
    private readonly IBooBoopController _control;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _ending = new();
    private readonly SemaphoreSlim _changed = new(0, 1);
    private readonly ChannelState _vibration = new(), _stretch = new();
    private long _readyAfter;
    private bool _allStopPending, _allStopInFlight, _closed;
    private Task? _run;
    private int _nextChannel;

    public BooBoopGameSession(IBooBoopController control, Action<string>? log = null)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _log = log ?? (_ => { });
        _control.Diagnostic += ControlDiagnostic;
        _control.Error += ControlError;
    }

    public static int MapVibration(GameStateSnapshot snapshot) => snapshot.Active
        ? snapshot.Strength switch { GameStrength.Low => 4, GameStrength.High => 8, _ => 0 } : 0;
    public static int MapStretch(PistonStateSnapshot snapshot) => snapshot.Active
        ? snapshot.Mode switch { PistonMode.Slow => 4, PistonMode.Medium => 6, PistonMode.Fast => 8, _ => 0 } : 0;
    public Task Completion { get { lock (_gate) return _run ?? Task.CompletedTask; } }

    public void Start()
    {
        lock (_gate)
        {
            if (_closed) throw new ObjectDisposedException(nameof(BooBoopGameSession));
            _run ??= Task.Run(RunAsync);
        }
    }

    public void ApplySnapshot(GameStateSnapshot snapshot, bool forceStop = false) =>
        Publish(_vibration, MapVibration(snapshot), snapshot.ObservedAt, forceStop,
            $"vibration: active={snapshot.Active}, strength={snapshot.Strength}, reason={snapshot.Reason}");

    public void ApplyPistonSnapshot(PistonStateSnapshot snapshot, bool forceStop = false) =>
        Publish(_stretch, MapStretch(snapshot), snapshot.ObservedAt, forceStop,
            $"stretch: active={snapshot.Active}, piston={snapshot.Mode}, reason={snapshot.Reason}");

    private void Publish(ChannelState channel, int level, long observedAt, bool forceStop, string diagnostic)
    {
        bool report;
        lock (_gate)
        {
            if (_closed) return;
            // Ignore out-of-order frames, but never discard an explicit lifecycle stop barrier.
            if (observedAt < channel.ObservedAt && !forceStop) return;
            bool changed = level != channel.Latest;
            channel.Latest = level;
            channel.ObservedAt = observedAt;
            if (level == 0 && changed) channel.StopPending = true;
            if (_readyAfter != 0 && observedAt > Math.Max(_readyAfter, channel.FreshAfter))
            {
                changed |= channel.NeedsFresh;
                channel.NeedsFresh = false;
            }
            if (forceStop)
            {
                if (!_allStopInFlight) _allStopPending = true;
                _vibration.NeedsFresh = _stretch.NeedsFresh = true;
            }
            report = channel.LastDiagnostic != diagnostic || forceStop;
            channel.LastDiagnostic = diagnostic;
            if (changed || forceStop) Wake();
        }
        if (report) Log("[Bridge] " + diagnostic + $", mapped={level}, globalStopBarrier={forceStop}");
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (!_closed)
            {
                _closed = true;
                _ending.Cancel();
                Wake();
            }
            _run ??= Task.Run(DisposeControlAsync);
            return new ValueTask(_run);
        }
    }

    private void Wake() { if (_changed.CurrentCount == 0) _changed.Release(); }
    private bool Fresh(ChannelState channel, long now) => !channel.NeedsFresh &&
        channel.ObservedAt > Math.Max(_readyAfter, channel.FreshAfter) && channel.ObservedAt <= now &&
        (now - channel.ObservedAt) / (double)Stopwatch.Frequency < 1.0;

    // Must run under the mailbox lock, and only after a successful global stop.
    private void RequireNewFrames()
    {
        long cutoff = Stopwatch.GetTimestamp();
        foreach (ChannelState channel in new[] { _vibration, _stretch })
        {
            channel.Sent = 0;
            channel.StopPending = false;
            channel.FreshAfter = cutoff;
            channel.NeedsFresh = true;
        }
    }

    private async Task RunAsync()
    {
        try
        {
            await _control.InitializeAsync(_ending.Token).ConfigureAwait(false);
            await _control.StopAsync(_ending.Token).ConfigureAwait(false);
            lock (_gate)
            {
                _ending.Token.ThrowIfCancellationRequested();
                _readyAfter = Stopwatch.GetTimestamp();
                RequireNewFrames();
                _allStopPending = false;
            }
            Log("Connected; waiting for a fresh observation on each channel. Vibration=0/4/8; piston stretch=0/4/6/8.");
            while (true)
            {
                _ending.Token.ThrowIfCancellationRequested();
                if (!_control.IsReady) throw new InvalidOperationException("The control session is no longer ready.");
                ChannelState? selected = null;
                int desired = 0;
                bool allStop;
                Task? sending = null;
                lock (_gate)
                {
                    _ending.Token.ThrowIfCancellationRequested();
                    allStop = _allStopPending;
                    if (allStop)
                    {
                        _allStopPending = false;
                        _allStopInFlight = true;
                        sending = _control.StopAsync(_ending.Token);
                    }
                    else
                    {
                        long now = Stopwatch.GetTimestamp();
                        // Stops get priority across channels; positive writes alternate to avoid starvation.
                        foreach (ChannelState channel in new[] { _vibration, _stretch })
                        {
                            if (channel.StopPending || (!Fresh(channel, now) && channel.Sent != 0))
                            { selected = channel; break; }
                        }
                        if (selected is null)
                            for (int offset = 0; offset < 2; offset++)
                            {
                                int index = (_nextChannel + offset) % 2;
                                ChannelState channel = index == 0 ? _vibration : _stretch;
                                int value = Fresh(channel, now) ? channel.Latest : 0;
                                if (value != channel.Sent) { selected = channel; desired = value; break; }
                            }
                        if (selected is not null)
                        {
                            selected.StopPending = false;
                            _nextChannel = selected == _vibration ? 1 : 0;
                            // Starting while locked linearizes this operation before later observations.
                            sending = selected == _vibration
                                ? _control.SetVibrationAsync(desired, _ending.Token)
                                : _control.SetStretchAsync(desired, _ending.Token);
                        }
                    }
                }
                if (sending is not null)
                {
                    string mode = allStop ? "all outputs" : selected == _vibration ? "vibration" : "stretch";
                    try
                    {
                        await sending.ConfigureAwait(false);
                        lock (_gate)
                        {
                            if (allStop) { RequireNewFrames(); _allStopInFlight = false; }
                            else selected!.Sent = desired;
                        }
                        if (allStop) Log("All-stop completed; waiting for fresh observations.");
                        Log($"[Bridge] {mode}={desired} written to native host; physical execution unconfirmed.");
                    }
                    catch (Exception error)
                    {
                        Log($"[Bridge] {mode} write failed or canceled: {error.GetType().Name}; execution is unknown.");
                        throw;
                    }
                    continue; // Always re-read both mailboxes after an awaited operation.
                }
                await _changed.WaitAsync(TimeSpan.FromMilliseconds(100), _ending.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_ending.IsCancellationRequested) { }
        catch (Exception error)
        { Log("Game bridge stopped: " + error.GetType().Name + ". No automatic retry or motion replay."); }
        finally
        {
            lock (_gate) _closed = true;
            await DisposeControlAsync().ConfigureAwait(false);
        }
    }

    private async Task DisposeControlAsync()
    {
        try { await _control.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { Log("Cleanup could not be confirmed: " + error.GetType().Name + ". Use physical stop if needed."); }
        finally
        {
            _control.Diagnostic -= ControlDiagnostic;
            _control.Error -= ControlError;
        }
        Log("Bridge cleanup finished; software requests cannot prove physical stopping.");
    }
    private void ControlDiagnostic(string message) => Log("[Hardware] " + message);
    private void ControlError(string message) => Log("[Hardware] " + message);
    private void Log(string message) { try { _log(message); } catch { } }
}
