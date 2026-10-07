#!/usr/bin/env python3
"""Checks package/source invariants. This does NOT compile C# or simulate hardware."""
from pathlib import Path
import argparse
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--game-project', type=Path)
parser.add_argument('--hardware-project', type=Path)
args = parser.parse_args()

def dependency(override, directory, project):
    if override:
        return override.resolve()
    nested = ROOT / 'dependencies' / directory / project
    return nested if nested.is_file() else ROOT.parent / directory / project

paths = {
    'SecretFlasherManaka.ForEveryThing': dependency(args.game_project, 'SecretFlasherManaka-ForEveryThing', 'SecretFlasherManaka.ForEveryThing.csproj'),
    'BooBoopControl': dependency(args.hardware_project, 'BooBoopControl', 'BooBoopControl.csproj'),
    'SecretFlasherManaka.BooBoopBridge': ROOT / 'SecretFlasherManaka.BooBoopBridge.csproj'
}
checks = 0

def check(condition, description):
    global checks
    if not condition:
        raise AssertionError(description)
    checks += 1
    print('PASS:', description)

def source(repo, name):
    return (paths[repo].parent / 'src' / name).read_text(encoding='utf-8')

try:
    names = ('SecretFlasherManaka.ForEveryThing', 'BooBoopControl', 'SecretFlasherManaka.BooBoopBridge')
    projects = {n: ET.parse(paths[n]).getroot() for n in names}
    for name, project in projects.items():
        check(project.findtext('.//TargetFramework') == 'net6.0', name + ' targets net6.0')
        check(project.findtext('.//PlatformTarget') == 'x64', name + ' targets x64')
        check(project.findtext('.//EnableDefaultCompileItems') == 'false', name + ' disables recursive default source inclusion')
        check([x.get('Include') for x in project.findall('.//Compile')] == ['src/**/*.cs'], name + ' compiles only its own src directory')
        check(not project.findall('.//PackageReference'), name + ' has no new NuGet package dependency')
        check(project.findtext('.//AssemblyVersion') == '1.0.0.0', name + ' keeps API-major assembly identity')
    check(not projects['SecretFlasherManaka.ForEveryThing'].findall('.//ProjectReference'), 'Game repository does not reference hardware/bridge')
    check(not projects['BooBoopControl'].findall('.//Reference') and not projects['BooBoopControl'].findall('.//ProjectReference'), 'Hardware repository has no game/Unity/BepInEx dependency')
    check({x.get('Include') for x in projects['SecretFlasherManaka.BooBoopBridge'].findall('.//ProjectReference')} == {'$(GameSignalsProject)', '$(HardwareControlProject)'}, 'Only the combination repository depends on both sides')
    for name in ('SecretFlasherManaka.ForEveryThing', 'SecretFlasherManaka.BooBoopBridge'):
        refs = projects[name].findall('.//Reference')
        check({x.get('Include') for x in refs} == {'BepInEx.Core','BepInEx.Unity.IL2CPP','0Harmony','Il2CppInterop.Runtime','Il2Cppmscorlib','UnityEngine.CoreModule'}, name + ' retains original core/interop reference set')
        check(all(x.findtext('HintPath') and x.findtext('Private') == 'false' for x in refs), name + ' uses explicit installed HintPaths and does not copy installed dependencies')
        check(any("'%(Reference.HintPath)' != ''" in x.get('Condition','') for x in projects[name].findall('.//Error')), name + ' guards empty framework reference HintPaths')
    game = '\n'.join(p.read_text(encoding='utf-8') for p in (paths['SecretFlasherManaka.ForEveryThing'].parent/'src').glob('*.cs'))
    hardware = '\n'.join(p.read_text(encoding='utf-8') for p in (paths['BooBoopControl'].parent/'src').glob('*.cs'))
    check(not any(x in hardware for x in ['using UnityEngine', 'using BepInEx', 'GameStateSnapshot', 'GameStrength', 'SecretFlasherManaka.ForEveryThing']), 'No game types leak into hardware source')
    check(not any(x in game for x in ['BooBoopController', 'SetVibrationAsync', 'SetStretchAsync', 'Process.Start']), 'Game plugin contains no hardware control')
    hub = source('SecretFlasherManaka.ForEveryThing', 'GameStateHub.cs')
    check('public static event Action<GameStateSnapshot, bool>? Observed;' in hub, 'Freshness observations are public independently of semantic changes')
    check('public static event Action<GameStateSnapshot>? StateChanged;' in hub and 'if (changed)' in hub, 'Semantic event deduplication is retained')
    check('UnityEngine' not in hub and 'Stopwatch.GetTimestamp()' in hub, 'Signal API contains pure CLR snapshots with monotonic timestamps')
    binding = source('SecretFlasherManaka.ForEveryThing', 'GameBinding.cs')
    check('ExposureUnnoticed2.Object3D.AdultGoods.CommonVibratorController' in binding and '"Leader"' in binding and '"VibrationStrength"' in binding, 'Exact Leader/actual-strength binding is retained')
    check('GameStrength.Unknown' in binding and 'UpdateRandomMode already resolves' in binding, 'Unknown/random fail-closed semantics are retained')
    game_plugin = source('SecretFlasherManaka.ForEveryThing', 'Plugin.cs')
    check('_observedFrame == frame' in game_plugin and 'leader == _observedLeader' in game_plugin, 'Only current-frame valid Leader observations publish active state')
    check('OnDisable() => _owner?.SetSourceEnabled(false)' in game_plugin and 'OnEnable() => _owner?.SetSourceEnabled(true)' in game_plugin, 'Source disable and resume boundaries are explicit')
    check('_owner?.BeginShutdown()' in game_plugin and 'Plugin.Current?.' not in game_plugin, 'Game lifecycle callbacks stay bound to their owning plugin instance')
    bridge_plugin = source('SecretFlasherManaka.BooBoopBridge', 'Plugin.cs')
    check('[BepInDependency(SecretFlasherManaka.ForEveryThing.Plugin.Id, ">=1.1.0 <2.0.0")]' in bridge_plugin, 'Bridge requires compatible 1.x signal plugin before loading')
    check('GameStateHub.Observed += ForwardObservation' in bridge_plugin and 'GameStateHub.Observed -= ForwardObservation' in bridge_plugin, 'Bridge subscribes/unsubscribes frame observations')
    check('GameStateReason.ShuttingDown' in bridge_plugin and 'OnDisable() => _owner?.BeginShutdown()' in bridge_plugin, 'Source shutdown and bridge disable terminate the session')
    session = source('SecretFlasherManaka.BooBoopBridge', 'BooBoopGameSession.cs')
    check('GameStrength.Low => 4, GameStrength.High => 8, _ => 0' in session, 'Game-device mapping is vibration Off/Low/High -> 0/4/8')
    check('_control.SetVibrationAsync(desired' in session and '_control.SetStretchAsync(desired' in session, 'Independent game outputs drive vibration and stretch')
    check(session.index('await _control.InitializeAsync') < session.index('await _control.StopAsync') < session.index('_readyAfter = Stopwatch.GetTimestamp()'), 'Initialization -> dual-output stop -> fresh cutoff is ordered')
    check('channel.StopPending = true' in session and '_allStopPending' in session and 'Always re-read both mailboxes' in session, 'Per-channel and global stop barriers survive latest-value coalescing')
    check('TimeSpan.FromMilliseconds(100)' in session and '< 1.0' in session and 'channel.ObservedAt > Math.Max(_readyAfter, channel.FreshAfter)' in session, 'Freshness watchdog and post-connection cutoff remain present')
    check('value != channel.Sent' in session, 'Unchanged output is independently deduplicated')
    piston = source('SecretFlasherManaka.ForEveryThing', 'PistonStateHub.cs')
    piston_binding = source('SecretFlasherManaka.ForEveryThing', 'PistonBinding.cs')
    check('PistonMode.Slow => 4, PistonMode.Medium => 6, PistonMode.Fast => 8, _ => 0' in session, 'Piston Off/Slow/Medium/Fast maps to stretch 0/4/6/8')
    check('private readonly ChannelState _vibration = new(), _stretch = new()' in session, 'Channel state/freshness/sent caches are distinct')
    check('if (allStop) { RequireNewFrames();' in session and 'await sending.ConfigureAwait(false)' in session, 'Successful global stop requires new observations on both channels')
    check('PistonStateHub.Observed += ForwardPistonObservation' in bridge_plugin and 'PistonStateHub.Observed -= ForwardPistonObservation' in bridge_plugin, 'Piston heartbeat subscription is removed during cleanup')
    check('public static event Action<PistonStateSnapshot>? StateChanged;' in piston and 'public static event Action<PistonStateSnapshot, bool>? Observed;' in piston, 'Piston semantic changes and health heartbeats are separate public events')
    check('UnityEngine' not in piston and 'Unknown = -1' in piston, 'Piston API is pure CLR and preserves invalid/unknown semantics')
    check('ExposureUnnoticed2.ObjectUI.InGame.VIbeStatePanel.VibeStatePanelView' in piston_binding and '"currentPistonMode"' in piston_binding, 'Piston binding uses the exact confirmed UI cached member')
    check('Il2CppType.From(_type)' in piston_binding and 'item.Pointer' in piston_binding, 'Piston discovery uses IL2CPP type and an exact cached wrapper')
    check('Time.realtimeSinceStartup + 1f' in piston_binding and 'if (Time.realtimeSinceStartup < _nextFindAt) return false' in piston_binding, 'Invalid piston cache recovery is rate limited to at most once per second')
    check('scene.handle' not in piston_binding and 'if (candidate)' in piston_binding, 'Piston discovery avoids an unverified active-scene-only assumption and rejects ambiguity')
    check('private void PublishGlobalStop' in game_plugin and 'PistonStateHub.Publish(false, PistonMode.Unknown, reason, forceStop: true)' in game_plugin, 'Global lifecycle paths explicitly invalidate both signals')
    epochs = source('BooBoopControl', 'MotionEpochs.cs')
    check('private long _global, _stretch, _vibration' in epochs and 'public void StopAll() => ++_global' in epochs, 'Hardware queue invalidation distinguishes per-channel and global stops')
    controller = source('BooBoopControl', 'BooBoopController.cs')
    check('_motionEpochs.Capture(prefix, level == 0)' in controller and '!_motionEpochs.IsCurrent(ticket)' in controller, 'Controller uses independent queue tickets while retaining one transport lock')
    check('level < 0 || level > 9' in controller, 'Hardware rejects values outside 0..9')
    check('new[] { "aa0100", "bb0100" }' in controller and 'StopModesAsync(stopLimit.Token, reportFailure: false)' in controller, 'Stop and disposal retain both output stop attempts')
    check('private const string SupportedModel = "FN010-RX"' in controller, 'FN010-RX is the only supported model')
    options = source('BooBoopControl', 'BooBoopOptions.cs')
    check('"product_list.json"' in options and '_productCachePath' in controller and '"serialNumber"' in controller and 'No fallback is allowed' in controller, 'UUID comes from validated product metadata without fallback')
    check('No retry or automatic reconnection will occur' in controller, 'Hardware failures do not auto-reconnect or replay motion')
    check((ROOT/'tests/Bridge.MockTests/Bridge.MockTests.csproj').is_file(), 'No-hardware mock project is included')
    mock_project=ET.parse(ROOT/'tests/Bridge.MockTests/Bridge.MockTests.csproj').getroot()
    check(not mock_project.findall('.//Reference') and not mock_project.findall('.//PackageReference'), 'Mock project does not need game assemblies or test packages')
    mock_source='\n'.join(p.read_text(encoding='utf-8') for p in (ROOT/'tests/Bridge.MockTests').glob('*.cs'))
    check('new BooBoopController(' not in mock_source and 'Process.Start' not in mock_source, 'Mocks never instantiate the real controller or start an external process')

    # Naming, dependency-path, and release-integrity checks for the renamed signal plugin.
    signal_name = 'SecretFlasherManaka.ForEveryThing'
    display_name = 'SecretFlasherManaka For EveryThing'
    expected_namespaces = (signal_name, 'BooBoopBridge', 'SecretFlasherManaka.BooBoopBridge')
    for name, namespace in zip(names, expected_namespaces):
        check(projects[name].findtext('.//AssemblyName') == name and projects[name].findtext('.//RootNamespace') == namespace, name + ' has the expected assembly and namespace identity')
    check(projects[signal_name].findtext('.//Product') == display_name and projects[signal_name].findtext('.//Title') == display_name, 'Game assembly product/title use the exact display name')
    check(f'[BepInPlugin(Id, "{display_name}", "1.1.0")]' in game_plugin and f'Log.LogInfo("{display_name} 1.1.0' in game_plugin, 'Plugin and startup log use the exact display name')
    check('public const string Id = "local.seleka.gamesignals";' in game_plugin, 'Game plugin GUID remains the stable technical ID')
    check(all(f'namespace {signal_name};' in p.read_text(encoding='utf-8') for p in (paths[signal_name].parent/'src').glob('*.cs')), 'Every game source uses the renamed namespace')
    check(all(f'using {signal_name};' in text for text in (bridge_plugin, session, mock_source)), 'Bridge and mock consumers import the renamed namespace')
    for key, directory, project in (
        ('GameSignalsProject', 'SecretFlasherManaka-ForEveryThing', 'SecretFlasherManaka.ForEveryThing.csproj'),
        ('HardwareControlProject', 'BooBoopControl', 'BooBoopControl.csproj')
    ):
        values = [node.text for node in projects[names[2]].findall('.//' + key)]
        check(values == [f'$(MSBuildThisFileDirectory)dependencies/{directory}/{project}', f'$(MSBuildThisFileDirectory)../{directory}/{project}'], key + ' supports nested and sibling repositories')
    check({node.get('Link') for node in mock_project.findall('.//Compile')} >= {'Production/GameStateHub.cs', 'Production/PistonStateHub.cs', 'Production/BooBoopGameSession.cs', 'Production/MotionEpochs.cs'}, 'Mocks link production state hubs, session and queue bookkeeping')
    check([node.get('Include') for node in mock_project.findall('.//ProjectReference')] == ['$(HardwareControlProject)'], 'Mock project references only hardware')
    check(all((paths[name].parent/'README.md').is_file() for name in names), 'Each repository has its own README')
    check('HostExecutablePath = Config.Bind' in bridge_plugin and 'ProductCachePath = Config.Bind' in bridge_plugin, 'Bridge exposes installation path settings')
    check('public BooBoopController(string preferredDeviceAddress = "")' in controller, 'Original hardware constructor remains available')
    check(all(not re.search(r'[CD]:\\', p.read_text(encoding='utf-8')) for p in paths['BooBoopControl'].parent.joinpath('src').glob('*.cs')), 'Production hardware has no fixed C/D drive paths')
    print(f'\nPASS: {checks} source invariants. NOT C# compilation, mock execution, or hardware validation.')
except (AssertionError, ET.ParseError, OSError) as error:
    print('FAIL:', error, file=sys.stderr)
    sys.exit(1)
