using System;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Attributes;
using BooBoopBridge;
using SecretFlasherManaka.ForEveryThing;
using UnityEngine;

namespace SecretFlasherManaka.BooBoopBridge;

[BepInPlugin(Id, "SecretFlasherManaka Boo Boop Dual Channel Bridge", "1.2.0")]
[BepInDependency(SecretFlasherManaka.ForEveryThing.Plugin.Id, ">=1.1.0 <2.0.0")]
public sealed class Plugin : BasePlugin
{
    public const string Id = "local.seleka.booboopbridge";
    internal static Plugin? Current;
    private BooBoopGameSession? _session;
    private BridgeLifecycle? _lifecycle;
    private Task? _shutdown;
    private bool _closing;

    public override void Load()
    {
        if (Current is not null) throw new InvalidOperationException("This game-device bridge is already active.");
        try
        {
            if (GameStateHub.ApiMajorVersion != 1 || PistonStateHub.ApiMajorVersion != 1 || BooBoopController.ApiMajorVersion != 1)
                throw new InvalidOperationException("This bridge requires game/hardware API major version 1.");
            Current = this;
            string address = Config.Bind("Device", "PreferredAddress", "",
                "Exact FN010-RX address, or empty when discovery has one eligible device. Never choose arbitrarily.").Value;
            var options = new BooBoopOptions
            {
                HostExecutablePath = Config.Bind("Host", "ExecutablePath", "",
                    "Absolute path to the installed Boo Boop.exe; empty uses the system Program Files folder. Environment variables are supported.").Value,
                ProductCachePath = Config.Bind("Host", "ProductCachePath", "",
                    "Absolute path to product_list.json; empty uses the current user's AppData/BooBoop cache. Environment variables are supported.").Value
            };
            _session = new BooBoopGameSession(new BooBoopController(address, options), message => Log.LogInfo(message));
            _lifecycle = AddComponent<BridgeLifecycle>();
            _lifecycle.Bind(this);
            GameStateHub.Observed += ForwardObservation;
            PistonStateHub.Observed += ForwardPistonObservation;
            // Never replay Current: initialization must finish and a new observed frame must arrive.
            if (GameStateHub.Current.Reason == GameStateReason.ShuttingDown)
                throw new InvalidOperationException("The game-signal session is already shutting down.");
            _session.Start();
            Log.LogInfo("Bridge 1.2.0 loaded: actual Leader -> vibration 0/4/8; cached piston mode -> stretch 0/4/6/8. Independent outputs.");
        }
        catch (Exception error)
        {
            Log.LogError("Bridge load failed: " + error.GetType().Name + ". This bridge session is disabled.");
            BeginShutdown();
            throw;
        }
    }

    private void ForwardObservation(GameStateSnapshot snapshot, bool forceStop)
    {
        if (_closing) return;
        _session?.ApplySnapshot(snapshot, forceStop);
        if (snapshot.Reason == GameStateReason.ShuttingDown) BeginShutdown();
    }

    private void ForwardPistonObservation(PistonStateSnapshot snapshot, bool forceStop)
    {
        if (_closing) return;
        _session?.ApplyPistonSnapshot(snapshot, forceStop);
        if (snapshot.Reason == GameStateReason.ShuttingDown) BeginShutdown();
    }

    internal void BeginShutdown()
    {
        if (_closing) return;
        _closing = true;
        GameStateHub.Observed -= ForwardObservation;
        PistonStateHub.Observed -= ForwardPistonObservation;
        Current = null;
        if (_lifecycle) UnityEngine.Object.Destroy(_lifecycle);
        _shutdown = DisposeSessionAsync();
    }

    private async Task DisposeSessionAsync()
    {
        if (_session is null) return;
        try { await _session.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { Log.LogError("Bridge cleanup could not be confirmed: " + error.GetType().Name); }
    }

    public override bool Unload()
    {
        BeginShutdown();
        // Never block Unity while the owned native host and both modes are stopped asynchronously.
        return _shutdown?.IsCompleted ?? true;
    }
}

public sealed class BridgeLifecycle : MonoBehaviour
{
    private Plugin? _owner;
    public BridgeLifecycle(IntPtr pointer) : base(pointer) { }
    [HideFromIl2Cpp]
    internal void Bind(Plugin owner) => _owner = owner;
    // Disabled bridge components end the session permanently; they do not resume/reconnect.
    public void OnDisable() => _owner?.BeginShutdown();
    public void OnApplicationQuit() => _owner?.BeginShutdown();
    public void OnDestroy() => _owner?.BeginShutdown();
}
