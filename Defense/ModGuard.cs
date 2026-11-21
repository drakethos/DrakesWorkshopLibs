using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DrakeModsLibs.Defense;

/// <summary>
/// Defensive Harmony host shared by the Drake mods (Libs, RenameIt, LockSmith, ServerTeams).
/// <para>
/// Problem it solves: players run these mods next to dozens of unknown mods and server-side mods, and the only
/// feedback is "it throws errors / conflicts". This guard makes our mod (a) survive other mods and (b) say
/// plainly <b>whose</b> error it is.
/// </para>
/// <list type="number">
/// <item><b>Isolated patching.</b> Each patch class is applied on its own. One that cannot be applied
/// (a vanilla/other-mod signature drifted, another mod's transpiler broke the IL) is skipped and named;
/// every other patch still loads. A plain <c>PatchAll</c> aborts at the first failure and leaves the mod half-patched.</item>
/// <item><b>Attributed runtime errors.</b> A Harmony finalizer rides on every method we patch. When an exception
/// passes through, the stack is walked to find the mod that threw it. If it was us the log says so (and the
/// error is contained when that is safe); if it was another mod the log says that instead, and the exception
/// is left untouched. Each distinct error is logged once, with a count.</item>
/// <item><b>Overlap report.</b> After every plugin has loaded, the methods we patch that other mods also patch are
/// listed (with who, and the risky kinds: transpilers and bool prefixes that can skip the original).
/// Written to <c>BepInEx/DrakeMods-compat-report.txt</c> — the file to ask users for.</item>
/// </list>
/// <para>Set the environment variable <c>DRAKEMODS_NO_GUARD=1</c> to disable the finalizers (patching is still isolated).</para>
/// </summary>
public sealed class ModGuard
{
    public const string ReportFileName = "DrakeMods-compat-report.txt";

    // ---- shared (per-assembly) state ---------------------------------------------------------------------------

    static readonly object Gate = new object();
    static readonly List<ModGuard> Guards = new List<ModGuard>();
    static readonly Dictionary<string, int> ErrorCounts = new Dictionary<string, int>();
    static readonly object FileGate = new object();
    static Dictionary<Assembly, string>? _pluginByAssembly;
    static int _pluginCountAtBuild = -1;
    static bool _runnerSpawned;
    const long ReportFileCapBytes = 256 * 1024;

    static readonly MethodInfo FinalizerMethod =
        typeof(ModGuard).GetMethod(nameof(Finalizer), BindingFlags.Static | BindingFlags.NonPublic)!;

    // ---- per-mod state ------------------------------------------------------------------------------------------

    readonly Harmony _harmony;
    readonly ManualLogSource _log;
    readonly HashSet<MethodBase> _armed = new HashSet<MethodBase>();
    readonly List<string> _failedClasses = new List<string>();
    int _appliedClasses;

    public string Name { get; }
    public string Guid { get; }
    public string Version { get; }
    public Assembly Assembly { get; }

    /// <summary>
    /// When true (default), an exception thrown by <i>our own</i> postfix is contained so it cannot break the game
    /// method it decorates. Exceptions from our prefixes are never contained (that would skip the game method).
    /// </summary>
    public bool ContainOwnPostfixErrors { get; set; } = true;

    ModGuard(string name, string guid, string version, Assembly assembly, Harmony harmony, ManualLogSource log)
    {
        Name = name;
        Guid = guid;
        Version = version;
        Assembly = assembly;
        _harmony = harmony;
        _log = log;
    }

    /// <summary>Create (and register) the guard for one plugin. Call from <c>Awake</c>, before patching.</summary>
    public static ModGuard Create(string name, string guid, string version, Assembly assembly, Harmony harmony, ManualLogSource log)
    {
        var guard = new ModGuard(name, guid, version, assembly, harmony, log);
        lock (Gate)
            Guards.Add(guard);
        SpawnRunner();
        return guard;
    }

    // ---- 1. isolated patching -----------------------------------------------------------------------------------

    /// <summary>
    /// Drop-in for <c>harmony.PatchAll(assembly)</c>: every <c>[HarmonyPatch]</c> class is applied independently.
    /// Returns the number of classes that could not be applied.
    /// </summary>
    public int PatchAll(Assembly? assembly = null)
    {
        foreach (var type in GetTypesSafe(assembly ?? Assembly))
        {
            if (!IsPatchClass(type))
                continue;
            PatchClass(type);
        }

        Arm();
        return _failedClasses.Count;
    }

    /// <summary>Drop-in for <c>harmony.PatchAll(typeof(X))</c> (plus nested patch classes are NOT included, same as Harmony).</summary>
    public bool PatchClass(Type type)
    {
        try
        {
            _harmony.CreateClassProcessor(type).Patch();
            _appliedClasses++;
            return true;
        }
        catch (Exception ex)
        {
            var target = TargetTypeOf(type);
            var others = target == null ? new List<string>() : OtherModsPatchingType(target);
            _failedClasses.Add(type.FullName ?? type.Name);

            var sb = new StringBuilder();
            sb.Append('[').Append(Name).Append("] Patch class ").Append(type.Name)
              .Append(" could NOT be applied and was skipped; the rest of ").Append(Name).Append(" still loads. ");
            sb.Append("Reason: ").Append(InnermostMessage(ex)).Append('.');
            if (target != null)
                sb.Append(" It targets ").Append(target.FullName).Append('.');

            var culprit = Blame(ex);
            if (culprit.IsMod && culprit.Assembly != Assembly)
                sb.Append(" The failure came from mod '").Append(culprit.Label).Append("', not ").Append(Name).Append('.');
            else if (others.Count > 0)
                sb.Append(" Other mods patching ").Append(target!.Name).Append(": ").Append(string.Join(", ", others))
                  .Append(" — one of them (or a game update) probably changed what this patch expects.");
            else
                sb.Append(" No other mod patches that type, so the game/target signature probably changed (game update?).");

            _log.LogWarning(sb.ToString());
            AppendReport("PATCH-SKIPPED " + sb);
            return false;
        }
    }

    static bool IsPatchClass(Type t)
    {
        try
        {
            return t.IsClass && t.IsDefined(typeof(HarmonyPatch), false);
        }
        catch
        {
            return false;
        }
    }

    static IEnumerable<Type> GetTypesSafe(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    static Type? TargetTypeOf(Type patchClass)
    {
        try
        {
            for (var t = patchClass; t != null; t = t.DeclaringType)
            {
                foreach (var attr in t.GetCustomAttributes(typeof(HarmonyPatch), false).OfType<HarmonyPatch>())
                {
                    if (attr.info?.declaringType != null)
                        return attr.info.declaringType;
                }
            }
        }
        catch
        {
            // best effort only
        }

        return null;
    }

    List<string> OtherModsPatchingType(Type target)
    {
        var labels = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var m in Harmony.GetAllPatchedMethods())
            {
                if (m.DeclaringType != target)
                    continue;
                foreach (var (label, _, _, ours) in EnumeratePatches(m))
                {
                    if (!ours)
                        labels.Add(label);
                }
            }
        }
        catch
        {
            // best effort only
        }

        return labels.ToList();
    }

    // ---- 2. attributed runtime errors ---------------------------------------------------------------------------

    /// <summary>
    /// Attach the attributing finalizer to every method this mod has patched. Safe to call repeatedly
    /// (call it again after late/soft patches such as a CompatHost initialising).
    /// </summary>
    public void Arm()
    {
        if (Environment.GetEnvironmentVariable("DRAKEMODS_NO_GUARD") == "1")
            return;

        try
        {
            foreach (var method in Harmony.GetAllPatchedMethods().ToList())
            {
                if (method == null || _armed.Contains(method))
                    continue;
                if (method.IsAbstract || method.ContainsGenericParameters)
                    continue;

                var info = Harmony.GetPatchInfo(method);
                if (info == null || !info.Owners.Contains(_harmony.Id))
                    continue;
                if (info.Finalizers.Any(p => p.owner == _harmony.Id))
                {
                    _armed.Add(method);
                    continue;
                }

                try
                {
                    _harmony.Patch(method, finalizer: new HarmonyMethod(FinalizerMethod) { priority = Priority.Last });
                    _armed.Add(method);
                }
                catch (Exception ex)
                {
                    // Never let the safety net be the thing that breaks the mod.
                    _log.LogDebug($"[{Name}] Error guard not attached to {Describe(method)}: {InnermostMessage(ex)}");
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug($"[{Name}] Error guard Arm() failed: {InnermostMessage(ex)}");
        }
    }

    /// <summary>
    /// Run <paramref name="action"/>; if it throws, log an attributed warning and carry on.
    /// Use for optional setup steps (soft-dependency hooks, registrations) in <c>Awake</c>.
    /// </summary>
    public bool Try(string what, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            var culprit = Blame(ex);
            var who = culprit.IsMod && culprit.Assembly != Assembly
                ? $"The error came from mod '{culprit.Label}', not {Name}."
                : $"The error is in {Name} itself — please report it.";
            var msg = $"[{Name}] '{what}' failed and was skipped: {ex.GetType().Name}: {InnermostMessage(ex)}. {who}";
            _log.LogWarning(msg);
            AppendReport("STEP-FAILED " + msg);
            return false;
        }
    }

    // Harmony finalizer: runs after every prefix/postfix of every mod on the method (priority Last).
    // Return the exception to rethrow it, or null to swallow it.
    static Exception? Finalizer(Exception? __exception, MethodBase __originalMethod)
    {
        if (__exception == null)
            return null;

        try
        {
            return Handle(__exception, __originalMethod);
        }
        catch
        {
            return __exception;
        }
    }

    static Exception? Handle(Exception ex, MethodBase original)
    {
        var culprit = Blame(ex);
        ModGuard? own = null;
        lock (Gate)
        {
            foreach (var g in Guards)
            {
                if (culprit.Assembly != null && g.Assembly == culprit.Assembly)
                {
                    own = g;
                    break;
                }
            }
        }

        var where = Describe(original);
        var key = where + "|" + culprit.Label + "|" + ex.GetType().Name;
        int count;
        lock (ErrorCounts)
        {
            ErrorCounts.TryGetValue(key, out count);
            ErrorCounts[key] = ++count;
        }

        // Which of our guards is attached to this method? (for the "also patched by" list and the log target)
        ModGuard? attached;
        lock (Gate)
        {
            attached = own;
            if (attached == null)
            {
                var owners = Harmony.GetPatchInfo(original)?.Owners;
                if (owners != null)
                    attached = Guards.FirstOrDefault(g => owners.Contains(g._harmony.Id));
            }
        }

        if (attached == null)
            return ex;

        if (own != null)
        {
            var stage = StageOfOwnPatch(ex, original, own);
            var contain = stage == PatchStage.Postfix && own.ContainOwnPostfixErrors;
            if (count == 1)
            {
                var others = OtherModLabels(original, own);
                var msg = $"[Error origin: {own.Name} {own.Version}] {ex.GetType().Name} in {where}: {InnermostMessage(ex)}\n" +
                          $"  Thrown by {own.Name} (its {stage.ToString().ToLowerInvariant()} on this method).\n" +
                          $"  Conflict check: {OwnVerdict(original, own)}\n" +
                          (contain
                              ? "  Contained: the game method was not affected.\n"
                              : "  Not contained: it runs before/inside the game method, so it is passed on unchanged.\n") +
                          (others.Count > 0 ? $"  Also patching this method: {string.Join(", ", others)}.\n" : string.Empty) +
                          $"  Stack: {FirstFrames(ex, 6)}";
                own._log.LogError(msg);
                AppendReport("OWN-ERROR " + msg);
            }
            else if (count == 10 || count == 100 || count == 1000)
            {
                own._log.LogError($"[{own.Name}] same error in {where} has now happened {count} times (details above).");
            }

            return contain ? null : ex;
        }

        if (count == 1)
        {
            var others = OtherModLabels(original, attached);
            var origin = culprit.Assembly == null
                ? "game code or unidentified (no mod code in the stack)"
                : culprit.IsMod ? $"mod '{culprit.Label}'" : $"game ({culprit.Label})";
            var msg = $"[Error origin: {origin}] {ex.GetType().Name} in {where}: {InnermostMessage(ex)}\n" +
                      $"  Conflict check: {ForeignVerdict(ex, original, attached, culprit)}\n" +
                      (others.Count > 0 ? $"  Mods patching this method: {string.Join(", ", others)}.\n" : string.Empty) +
                      "  Passed on unchanged.";
            attached._log.LogWarning(msg);
            AppendReport("FOREIGN-ERROR " + msg);
        }

        return ex;
    }

    /// <summary>
    /// Called from the <c>catch</c> of a hardened patch method: logs once (then at 10/100/1000) with the mod the error
    /// originates from, and lets the game method carry on. Never throws.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void PatchFailed(string patchName, Exception ex)
    {
        try
        {
            var calling = Assembly.GetCallingAssembly();
            ModGuard? guard;
            lock (Gate)
                guard = Guards.FirstOrDefault(g => g.Assembly == calling) ?? Guards.FirstOrDefault();
            if (guard == null)
                return;

            var culprit = Blame(ex);
            var key = "patch|" + patchName + "|" + culprit.Label + "|" + ex.GetType().Name;
            int count;
            lock (ErrorCounts)
            {
                ErrorCounts.TryGetValue(key, out count);
                ErrorCounts[key] = ++count;
            }

            if (count == 1)
            {
                var ours = culprit.Assembly == guard.Assembly;
                var origin = culprit.Assembly == null
                    ? "game code or unidentified"
                    : ours ? $"{guard.Name} {guard.Version}" : culprit.IsMod ? $"mod '{culprit.Label}'" : $"game ({culprit.Label})";
                var msg = $"[Error origin: {origin}] {ex.GetType().Name} in {guard.Name} patch {patchName}: {InnermostMessage(ex)}\n" +
                          (ours
                              ? $"  Thrown by {guard.Name}'s own code. It was contained and the game method ran as normal.\n"
                              : $"  Thrown outside {guard.Name} while {guard.Name}'s patch was running, so another mod or the game may have handed it unexpected data. It was contained and the game method ran as normal.\n") +
                          $"  Stack: {FirstFrames(ex, 6)}";
                guard._log.LogWarning(msg);
                AppendReport("PATCH-ERROR " + msg);
            }
            else if (count == 10 || count == 100 || count == 1000)
            {
                guard._log.LogWarning($"[{guard.Name}] patch {patchName} has now failed {count} times (details above).");
            }
        }
        catch
        {
            // diagnostics must never throw into the game
        }
    }

    /// <summary>Plain-language reading of an error our own patch threw: could another mod have caused it?</summary>
    static string OwnVerdict(MethodBase original, ModGuard own)
    {
        var risky = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var all = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var (label, kind, note, ours) in EnumeratePatches(original))
            {
                if (ours)
                    continue;
                all.Add(label);
                if (note.Length > 0)
                    risky.Add($"{label} ({kind.Split(',')[0]})");
            }
        }
        catch
        {
            // best effort
        }

        if (risky.Count > 0)
            return $"possible conflict with {string.Join(", ", risky)}. It changes or can skip this method, so what {own.Name} received may not be what it expects. Try without it to confirm.";
        if (all.Count > 0)
            return $"{string.Join(", ", all)} also patch this method but nothing suggests they interfere; this looks like a {own.Name} bug or unexpected data.";
        return $"no other mod patches this method; this looks like a {own.Name} bug or unexpected game data.";
    }

    /// <summary>Plain-language reading of an error another mod threw on a method we also patch.</summary>
    static string ForeignVerdict(Exception ex, MethodBase original, ModGuard attached, Culprit culprit)
    {
        var who = culprit.Assembly == null ? "the source" : culprit.Label;
        if (StageOfOwnPatch(ex, original, attached) != PatchStage.Unknown)
            return $"{attached.Name}'s patch was running in the same call as {who}, so this may be an interaction between the two. Disabling either one would confirm.";
        return $"{attached.Name}'s code was not part of the failing call. {attached.Name} only patches this method too, so this looks like an issue inside {who}.";
    }

    enum PatchStage { Unknown, Prefix, Postfix, Other }

    static PatchStage StageOfOwnPatch(Exception ex, MethodBase original, ModGuard own)
    {
        try
        {
            var info = Harmony.GetPatchInfo(original);
            if (info == null)
                return PatchStage.Unknown;

            var frames = new StackTrace(ex.GetBaseException() ?? ex, false).GetFrames();
            if (frames == null)
                return PatchStage.Unknown;

            foreach (var f in frames)
            {
                var m = f.GetMethod();
                if (m == null)
                    continue;
                if (info.Prefixes.Any(p => p.owner == own._harmony.Id && p.PatchMethod == m))
                    return PatchStage.Prefix;
                if (info.Postfixes.Any(p => p.owner == own._harmony.Id && p.PatchMethod == m))
                    return PatchStage.Postfix;
                if (info.Transpilers.Any(p => p.owner == own._harmony.Id && p.PatchMethod == m))
                    return PatchStage.Other;
            }
        }
        catch
        {
            // fall through
        }

        return PatchStage.Unknown;
    }

    // ---- attribution --------------------------------------------------------------------------------------------

    readonly struct Culprit
    {
        public Culprit(Assembly? assembly, string label, bool isMod)
        {
            Assembly = assembly;
            Label = label;
            IsMod = isMod;
        }

        public Assembly? Assembly { get; }
        public string Label { get; }
        public bool IsMod { get; }
    }

    /// <summary>Walk the stack trace and name the first non-infrastructure assembly (the mod or game code that threw).</summary>
    static Culprit Blame(Exception ex)
    {
        try
        {
            var inner = ex.GetBaseException() ?? ex;
            var frames = new StackTrace(inner, false).GetFrames();
            if (frames == null)
                return new Culprit(null, "unknown", false);

            var plugins = PluginMap();
            Assembly? firstGame = null;
            foreach (var f in frames)
            {
                var m = f.GetMethod();
                var asm = m?.DeclaringType?.Assembly;
                if (asm == null)
                    continue; // dynamic/Harmony-generated frame

                var simple = asm.GetName().Name ?? "?";
                if (plugins.TryGetValue(asm, out var label))
                    return new Culprit(asm, label, true);
                if (IsGameAssembly(simple))
                {
                    firstGame ??= asm;
                    continue;
                }

                if (IsInfrastructure(simple))
                    continue;

                return new Culprit(asm, simple, true);
            }

            return firstGame != null
                ? new Culprit(firstGame, firstGame.GetName().Name ?? "game", false)
                : new Culprit(null, "unknown", false);
        }
        catch
        {
            return new Culprit(null, "unknown", false);
        }
    }

    static bool IsGameAssembly(string n) =>
        n == "assembly_valheim" || n == "assembly_utils" || n == "assembly_guiframework" ||
        n == "assembly_postprocessing" || n == "assembly_sunshafts" || n == "SoftReferenceableAssets" ||
        n == "gui_framework" || n == "Assembly-CSharp";

    static bool IsInfrastructure(string n) =>
        n == "mscorlib" || n == "netstandard" || n == "System" || n.StartsWith("System.", StringComparison.Ordinal) ||
        n.StartsWith("Mono.", StringComparison.Ordinal) || n.StartsWith("MonoMod", StringComparison.Ordinal) ||
        n == "0Harmony" || n.StartsWith("HarmonyLib", StringComparison.Ordinal) ||
        n.StartsWith("UnityEngine", StringComparison.Ordinal) || n.StartsWith("Unity.", StringComparison.Ordinal) ||
        n.StartsWith("BepInEx", StringComparison.Ordinal) || n.StartsWith("Microsoft.", StringComparison.Ordinal);

    static Dictionary<Assembly, string> PluginMap()
    {
        lock (Gate)
        {
            var infos = Chainloader.PluginInfos;
            if (_pluginByAssembly != null && infos != null && _pluginCountAtBuild == infos.Count)
                return _pluginByAssembly;

            var map = new Dictionary<Assembly, string>();
            if (infos != null)
            {
                foreach (var pi in infos.Values)
                {
                    try
                    {
                        var asm = pi.Instance?.GetType().Assembly;
                        if (asm != null && !map.ContainsKey(asm))
                            map[asm] = $"{pi.Metadata.Name} [{pi.Metadata.GUID} {pi.Metadata.Version}]";
                    }
                    catch
                    {
                        // skip broken plugin entries
                    }
                }

                _pluginCountAtBuild = infos.Count;
            }

            _pluginByAssembly = map;
            return map;
        }
    }

    static string LabelForAssembly(Assembly? asm, string fallbackOwner)
    {
        if (asm != null && PluginMap().TryGetValue(asm, out var label))
            return label;
        var simple = asm?.GetName().Name;
        return string.IsNullOrEmpty(simple) ? fallbackOwner : simple!;
    }

    /// <summary>(label, kind, risky-note, isOurs) for every patch on <paramref name="method"/>.</summary>
    static IEnumerable<(string Label, string Kind, string Note, bool Ours)> EnumeratePatches(MethodBase method)
    {
        var info = Harmony.GetPatchInfo(method);
        if (info == null)
            yield break;

        foreach (var (patches, kind) in new[]
                 {
                     (info.Prefixes, "prefix"), (info.Postfixes, "postfix"),
                     (info.Transpilers, "transpiler"), (info.Finalizers, "finalizer"),
                 })
        {
            foreach (var p in patches)
            {
                var asm = p.PatchMethod?.DeclaringType?.Assembly;
                var ours = false;
                lock (Gate)
                    ours = Guards.Any(g => g._harmony.Id == p.owner);

                var note = string.Empty;
                if (kind == "transpiler")
                    note = "rewrites the method IL";
                else if (kind == "prefix" && p.PatchMethod != null && p.PatchMethod.ReturnType == typeof(bool))
                    note = "can skip the original and lower-priority prefixes";

                yield return (LabelForAssembly(asm, p.owner), $"{kind}, priority {p.priority}", note, ours);
            }
        }
    }

    static List<string> OtherModLabels(MethodBase method, ModGuard own)
    {
        var labels = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var (label, kind, _, ours) in EnumeratePatches(method))
            {
                if (!ours)
                    labels.Add($"{label} ({kind.Split(',')[0]})");
            }
        }
        catch
        {
            // best effort
        }

        return labels.ToList();
    }

    // ---- 3. overlap report --------------------------------------------------------------------------------------

    static void SpawnRunner()
    {
        lock (Gate)
        {
            if (_runnerSpawned)
                return;
            _runnerSpawned = true;
        }

        try
        {
            var go = new GameObject("DrakeModsGuardRunner") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<GuardRunner>();
        }
        catch
        {
            // Report is a convenience; never fail plugin load over it.
        }
    }

    sealed class GuardRunner : MonoBehaviour
    {
        IEnumerator Start()
        {
            // Every plugin's Awake has finished by now.
            yield return null;
            try
            {
                foreach (var g in SnapshotGuards())
                    g.Arm();
                WriteOverlapReport();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[DrakeModsLibs] compat report failed: " + ex.Message);
            }

            Destroy(gameObject);
        }
    }

    static List<ModGuard> SnapshotGuards()
    {
        lock (Gate)
            return Guards.ToList();
    }

    static void WriteOverlapReport()
    {
        var guards = SnapshotGuards();
        if (guards.Count == 0)
            return;

        var plugins = new StringBuilder();
        try
        {
            foreach (var pi in Chainloader.PluginInfos.Values.OrderBy(p => p.Metadata.Name))
                plugins.AppendLine($"  {pi.Metadata.Name}  [{pi.Metadata.GUID}]  {pi.Metadata.Version}");
        }
        catch
        {
            plugins.AppendLine("  (unavailable)");
        }

        WriteSection("plugins", "Loaded BepInEx plugins (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "):\n" + plugins);

        var sb = new StringBuilder();
        foreach (var g in guards)
        {
            var shared = 0;
            var risky = 0;
            var lines = new StringBuilder();
            foreach (var m in g._armed.OrderBy(Describe, StringComparer.Ordinal))
            {
                var others = EnumeratePatches(m).Where(e => !e.Ours).ToList();
                if (others.Count == 0)
                    continue;

                shared++;
                var isRisky = others.Any(o => o.Note.Length > 0);
                if (isRisky)
                    risky++;

                lines.AppendLine($"  {(isRisky ? "[RISK] " : "       ")}{Describe(m)}");
                foreach (var o in others)
                    lines.AppendLine($"          also: {o.Label} ({o.Kind}){(o.Note.Length > 0 ? " — " + o.Note : string.Empty)}");
            }

            sb.AppendLine($"--- {g.Name} {g.Version} [{g.Guid}] ---");
            sb.AppendLine($"Patch classes applied: {g._appliedClasses}, skipped: {g._failedClasses.Count}");
            foreach (var f in g._failedClasses)
                sb.AppendLine($"  skipped: {f}");
            sb.AppendLine($"Patched methods: {g._armed.Count}; shared with other mods: {shared}; risky overlaps: {risky}");
            if (shared > 0)
                sb.Append(lines);
            sb.AppendLine();

            g._log.LogInfo(
                $"[{g.Name}] Compat check: {g._armed.Count} patched methods, {shared} shared with other mods, {risky} risky " +
                $"(other transpilers / skipping prefixes). {g._failedClasses.Count} patch class(es) skipped. " +
                $"Details: BepInEx/{ReportFileName}");
            if (risky > 0)
                g._log.LogWarning(
                    $"[{g.Name}] {risky} method(s) are also rewritten or short-circuited by other mods — if you see errors, " +
                    $"send BepInEx/{ReportFileName} and LogOutput.log; it names who touches what.");
        }

        WriteSection(guards[0].Assembly.GetName().Name ?? "mod", sb.ToString());
    }

    // The report file is shared by every Drake mod in the game (each has its own copy of this class), so each
    // one owns a delimited section; the file is reset once per game launch.
    static void WriteSection(string key, string body)
    {
        lock (FileGate)
        {
            try
            {
                var path = ReportPath();
                ResetIfStaleUnlocked(path);
                var text = File.Exists(path) ? File.ReadAllText(path) : "=== Drake mods compatibility report ===\n(runtime errors are appended as they happen)\n\n";
                var begin = $"#### BEGIN {key}\n";
                var end = $"#### END {key}\n";
                var a = text.IndexOf(begin, StringComparison.Ordinal);
                if (a >= 0)
                {
                    var b = text.IndexOf(end, a, StringComparison.Ordinal);
                    if (b >= 0)
                        text = text.Remove(a, b + end.Length - a);
                }

                File.WriteAllText(path, text + begin + body.TrimEnd() + "\n" + end + "\n");
            }
            catch
            {
                // read-only install dir etc. — the log lines still carry the summary
            }
        }
    }

    static void ResetIfStaleUnlocked(string path)
    {
        try
        {
            if (File.Exists(path) && File.GetLastWriteTime(path) < Process.GetCurrentProcess().StartTime)
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    static string ReportPath() => Path.Combine(Paths.BepInExRootPath, ReportFileName);

    static void AppendReport(string text)
    {
        lock (FileGate)
        {
            try
            {
                var path = ReportPath();
                ResetIfStaleUnlocked(path);
                if (File.Exists(path) && new FileInfo(path).Length > ReportFileCapBytes)
                    return;
                File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            }
            catch
            {
                // diagnostics must never throw
            }
        }
    }

    // ---- helpers ------------------------------------------------------------------------------------------------

    static string Describe(MethodBase m) => (m.DeclaringType?.Name ?? "?") + "." + m.Name;

    static string InnermostMessage(Exception ex)
    {
        var inner = ex;
        while (inner.InnerException != null)
            inner = inner.InnerException;
        return inner.Message;
    }

    static string FirstFrames(Exception ex, int count)
    {
        try
        {
            var lines = (ex.GetBaseException() ?? ex).StackTrace?.Split('\n') ?? new string[0];
            return string.Join(" | ", lines.Take(count).Select(l => l.Trim()));
        }
        catch
        {
            return "(unavailable)";
        }
    }
}
