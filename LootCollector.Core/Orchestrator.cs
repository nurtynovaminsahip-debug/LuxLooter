using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LootCollector.Core;

public sealed class Orchestrator
{
    private sealed class StateData
    {
        public List<string> done { get; set; } = new List<string>();


        public List<string> failed { get; set; } = new List<string>();


        public List<string> under13 { get; set; } = new List<string>();


        public List<string> broken { get; set; } = new List<string>();

    }

    private sealed class PassResult
    {
        public List<string> done = new List<string>();

        public List<string> failed = new List<string>();

        public List<string> rerouted = new List<string>();
    }

    private const int PublicJoinAttempts = 3;

    private const int RejoinMax = 6;

    private readonly GameDef _game;

    private readonly Settings _settings;

    private readonly AccountStore _store;

    private readonly Action<string> _log;

    private readonly CancellationToken _ct;

    private readonly string _doneDir;

    private readonly string _workspace;

    private readonly string _statePath;

    private StateData _state;

    private int? _mainPid;

    private int _runItems;

    private readonly PriceCatalog _prices;

    private double _runUsd;

    private readonly List<(string name, double price)> _runTop = new List<(string, double)>();

    private readonly Dictionary<string, int> _runBreakdown = new Dictionary<string, int>();

    private readonly StatsStore _stats = StatsStore.Load();

    private Dictionary<string, int> _num = new Dictionary<string, int>();

    private int _total;

    private readonly string _mainOverride;

    private readonly List<string> _queueOverride;

    private readonly bool _skipClean;

    private readonly bool _keepMainAlive;

    private readonly bool _mainAlreadyUp;

    private readonly bool _reportFullInventory;

    private static readonly object LaunchGate = new object();

    internal static volatile bool SerializeSpawnFocus;

    private const int SpawnFocusSec = 45;

    internal int? MainPid => _mainPid;

    private string Main => _mainOverride ?? _settings.MainFor(_game.Key);

    internal Orchestrator(GameDef game, Settings settings, AccountStore store, Action<string> log, CancellationToken ct, string doneDir = null, string mainOverride = null, List<string> queueOverride = null, string statePath = null, bool skipClean = false, bool keepMainAlive = false, bool mainAlreadyUp = false, bool reportFullInventory = false)
    {
        _game = game;
        _settings = settings;
        _store = store;
        _log = log;
        _ct = ct;
        _workspace = Paths.Workspace(settings.AutoexecDir);
        _doneDir = doneDir ?? Paths.DoneDir(game.Key, _workspace);
        _statePath = statePath ?? Path.Combine(Paths.StateDir, "progress_" + game.Key + "_done.json");
        _mainOverride = mainOverride;
        _queueOverride = queueOverride;
        _skipClean = skipClean;
        _keepMainAlive = keepMainAlive;
        _mainAlreadyUp = mainAlreadyUp;
        _reportFullInventory = reportFullInventory;
        _state = LoadState();
        _prices = PriceCatalog.Load();
    }

    private void Log(string m)
    {
        _log?.Invoke(m);
    }

    private void Notify(string m)
    {
        _log?.Invoke(m);
    }

    private void Sleep(int ms)
    {
        try
        {
            _ct.WaitHandle.WaitOne(ms);
        }
        catch
        {
        }
    }

    private StateData LoadState()
    {
        try
        {
            if (File.Exists(_statePath))
            {
                return JsonSerializer.Deserialize<StateData>(File.ReadAllText(_statePath)) ?? new StateData();
            }
        }
        catch
        {
        }
        return new StateData();
    }

    private void SaveState()
    {
        HashSet<string> done = new HashSet<string>(_state.done);
        _state.done = _state.done.Distinct().ToList();
        _state.failed = (from a in _state.failed.Distinct()
                         where !done.Contains(a)
                         select a).ToList();
        _state.broken = (from a in _state.broken.Distinct()
                         where !done.Contains(a)
                         select a).ToList();
        _state.under13 = _state.under13.Distinct().ToList();
        try
        {
            Directory.CreateDirectory(Paths.StateDir);
            File.WriteAllText(_statePath, JsonSerializer.Serialize(_state, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch
        {
        }
    }

    private List<string> AltQueue()
    {
        List<string> source = _store.Usernames();
        HashSet<string> exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(Main))
        {
            exclude.Add(Main);
        }
        if (_game.Key == "adoptme")
        {
            foreach (string item in _settings.AdoptmeExclude)
            {
                exclude.Add(item);
            }
        }
        HashSet<string> skip = new HashSet<string>(_state.done, StringComparer.OrdinalIgnoreCase);
        foreach (string item2 in _state.broken)
        {
            skip.Add(item2);
        }
        return source.Where((string n) => !exclude.Contains(n) && !skip.Contains(n)).ToList();
    }

    private List<string> AltQueueExcluding(IEnumerable<string> mains)
    {
        List<string> source = _store.Usernames();
        HashSet<string> exclude = new HashSet<string>(mains, StringComparer.OrdinalIgnoreCase);
        if (_game.Key == "adoptme")
        {
            foreach (string item in _settings.AdoptmeExclude)
            {
                exclude.Add(item);
            }
        }
        HashSet<string> skip = new HashSet<string>(_state.done, StringComparer.OrdinalIgnoreCase);
        foreach (string item2 in _state.broken)
        {
            skip.Add(item2);
        }
        return source.Where((string n) => !exclude.Contains(n) && !skip.Contains(n)).ToList();
    }

    private string Tag(string a)
    {
        int value;
        return $"{(_num.TryGetValue(a, out value) ? value : 0)}/{_total} {a}";
    }

    private string P(string a, string ext)
    {
        return Path.Combine(_doneDir, a + ext);
    }

    private bool Flag(string a)
    {
        return File.Exists(P(a, ".txt"));
    }

    private bool Joined(string a)
    {
        return File.Exists(P(a, ".join"));
    }

    private bool Errored(string a)
    {
        return File.Exists(P(a, ".err"));
    }

    private bool Failed(string a)
    {
        return File.Exists(P(a, ".fail"));
    }

    private bool Attached(string a)
    {
        return File.Exists(P(a, ".run"));
    }

    private bool Rejoined(string a)
    {
        return File.Exists(P(a, ".rejoin"));
    }

    private string ReadErr(string a)
    {
        try
        {
            return File.ReadAllText(P(a, ".err")).Trim();
        }
        catch
        {
            return "?";
        }
    }

    private void Clear(string a)
    {
        string[] array = new string[6] { ".txt", ".join", ".err", ".fail", ".run", ".rejoin" };
        foreach (string ext in array)
        {
            try
            {
                if (File.Exists(P(a, ext)))
                {
                    File.Delete(P(a, ext));
                }
            }
            catch
            {
            }
        }
    }

    private void WriteFile(string name, string body)
    {
        try
        {
            Directory.CreateDirectory(_doneDir);
            File.WriteAllText(Path.Combine(_doneDir, name), body);
        }
        catch
        {
        }
    }

    private void SetActive(string name)
    {
        WriteFile("_active.txt", name);
    }

    private void SetMode(string mode)
    {
        WriteFile("_mode.txt", mode);
    }

    private void SetCategory(string cat)
    {
        WriteFile("_category.txt", cat);
    }

    private void WriteRoutes(List<string> alts)
    {
        try
        {
            string text = Paths.DoneDir(_game.Key, _workspace);
            Directory.CreateDirectory(text);
            string fileName = Path.GetFileName(_doneDir.TrimEnd('\\', '/'));
            List<string> list = new List<string>(alts);
            if (!string.IsNullOrEmpty(Main))
            {
                list.Add(Main);
            }
            foreach (string item in list)
            {
                try
                {
                    File.WriteAllText(Path.Combine(text, "route_" + item + ".txt"), fileName);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private void ClearRoutes()
    {
        try
        {
            string[] files = Directory.GetFiles(Paths.DoneDir(_game.Key, _workspace), "route_*.txt");
            foreach (string path in files)
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private static string RarText(List<string> v, string dflt)
    {
        if (v != null)
        {
            if (v.Count <= 0)
            {
                return "NONE";
            }
            return string.Join(",", v);
        }
        return dflt;
    }

    private void WriteRuntime()
    {
        Directory.CreateDirectory(_doneDir);
        WriteFile("_main.txt", Main);
        WriteFile("_webhook.txt", "");
        WriteFile("_weapon_rarities.txt", RarText(_settings.WeaponRarities, "ALL"));
        WriteFile("_pet_rarities.txt", RarText(_settings.PetRarities, "NONE"));
        List<string> adoptCategories = _settings.AdoptCategories;
        WriteFile("_categories.txt", (adoptCategories == null || adoptCategories.Count == 0) ? "" : string.Join(",", adoptCategories));
    }

    private int ReadCount(string a)
    {
        try
        {
            int result;
            return int.TryParse(File.ReadAllText(P(a, ".count")).Trim(), out result) ? result : 0;
        }
        catch
        {
            return 0;
        }
    }

    private string PriceKey(string name, string[] parts)
    {
        if (_game.Key == "adoptme" && parts.Length > 3)
        {
            string text = parts[3].Trim().Replace(',', '|');
            if (text.Length > 0)
            {
                return name + "|" + text;
            }
        }
        return name;
    }

    private (double usd, List<(string name, double price)> top, Dictionary<string, int> bd) ValueOf(string acct)
    {
        double num = 0.0;
        List<(string, double)> list = new List<(string, double)>();
        Dictionary<string, int> dictionary = new Dictionary<string, int>();
        try
        {
            string path = P(acct, ".items");
            if (!File.Exists(path))
            {
                return (usd: 0.0, top: list, bd: dictionary);
            }
            string[] array = File.ReadAllLines(path);
            foreach (string text in array)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    string[] array2 = text.Split('\t');
                    string text2 = array2[0].Trim();
                    int result;
                    int num2 = ((array2.Length <= 1 || !int.TryParse(array2[1].Trim(), out result)) ? 1 : result);
                    string text3 = ((array2.Length > 2) ? array2[2].Trim() : null);
                    if (!string.IsNullOrEmpty(text3))
                    {
                        dictionary.TryGetValue(text3, out var value);
                        dictionary[text3] = value + num2;
                    }
                    double num3 = _prices.Price(_game.Key, PriceKey(text2, array2));
                    if (!(num3 <= 0.0))
                    {
                        num += num3 * (double)num2;
                        list.Add((text2, num3));
                    }
                }
            }
        }
        catch
        {
        }
        return (usd: num, top: list, bd: dictionary);
    }

    private (int count, double usd) DumpMainInventory()
    {
        try
        {
            string path = P(Main, ".fullitems");
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
            WriteFile("_dumpfull.txt", Main);
            Log("считаю полный инвентарь главного " + Main + "…");
            DateTime utcNow = DateTime.UtcNow;
            while ((DateTime.UtcNow - utcNow).TotalSeconds < 30.0 && !_ct.IsCancellationRequested && !File.Exists(path))
            {
                Sleep(500);
            }
            try
            {
                File.Delete(Path.Combine(_doneDir, "_dumpfull.txt"));
            }
            catch
            {
            }
            if (!File.Exists(path))
            {
                return (count: 0, usd: 0.0);
            }
            double num = 0.0;
            int num2 = 0;
            string[] array = File.ReadAllLines(path);
            foreach (string text in array)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    string[] array2 = text.Split('\t');
                    string name = array2[0].Trim();
                    int result;
                    int num3 = ((array2.Length <= 1 || !int.TryParse(array2[1].Trim(), out result)) ? 1 : result);
                    num2 += num3;
                    double num4 = _prices.Price(_game.Key, PriceKey(name, array2));
                    if (num4 > 0.0)
                    {
                        num += num4 * (double)num3;
                    }
                }
            }
            return (count: num2, usd: num);
        }
        catch
        {
            return (count: 0, usd: 0.0);
        }
    }

    private void ClearCounts()
    {
        try
        {
            string[] files = Directory.GetFiles(_doneDir, "*.count");
            foreach (string path in files)
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
        try
        {
            string[] files = Directory.GetFiles(_doneDir, "*.items");
            foreach (string path2 in files)
            {
                try
                {
                    File.Delete(path2);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private int TotalItems()
    {
        int num = 0;
        try
        {
            string[] files = Directory.GetFiles(_doneDir, "*.count");
            for (int i = 0; i < files.Length; i++)
            {
                if (int.TryParse(File.ReadAllText(files[i]).Trim(), out var result))
                {
                    num += result;
                }
            }
        }
        catch
        {
        }
        return num;
    }

    private string MainJobId()
    {
        try
        {
            string text = File.ReadAllText(Path.Combine(_doneDir, "_mainjob.txt")).Trim();
            return (text.Length > 0) ? text : null;
        }
        catch
        {
            return null;
        }
    }

    private int? Launch(string account, string mode)
    {
        string cookie = _store.GetCookie(account);
        if (string.IsNullOrEmpty(cookie))
        {
            Log("     [x] " + account + ": нет куки в сторе");
            return null;
        }
        lock (LaunchGate)
        {
            HashSet<int> before = ProcessUtil.RobloxPids();
            try
            {
                if (mode == "follow")
                {
                    string text = MainJobId();
                    if (text != null)
                    {
                        RobloxLauncher.LaunchAsync(cookie, _game.PlaceId, text, null, 0L).GetAwaiter().GetResult();
                    }
                    else
                    {
                        Account account2 = _store.Find(Main);
                        RobloxLauncher.LaunchAsync(cookie, _game.PlaceId, null, null, account2?.UserId ?? 0).GetAwaiter().GetResult();
                    }
                }
                else
                {
                    RobloxLauncher.LaunchAsync(cookie, _game.PlaceId, null, null, 0L).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                Log("     [x] запуск " + account + ": " + ex.Message);
                return null;
            }
            // 🔥 Увеличен таймаут до 60 секунд
            int? num = ProcessUtil.WaitNewPid(before, 60.0, _ct);
            WindowManager.Arrange(num, _settings.WinMode);
            if (SerializeSpawnFocus && num.HasValue)
            {
                int valueOrDefault = num.GetValueOrDefault();
                // 🔥 Увеличен таймаут до 40 секунд
                nint num2 = WindowManager.WaitForWindow(valueOrDefault, 40);
                DateTime utcNow = DateTime.UtcNow;
                while ((DateTime.UtcNow - utcNow).TotalSeconds < 45.0 && !_ct.IsCancellationRequested && !Joined(account) && ProcessUtil.IsRunning(valueOrDefault))
                {
                    if (num2 != IntPtr.Zero)
                    {
                        WindowManager.ForegroundHwnd(num2);
                    }
                    Thread.Sleep(600);
                }
            }
            return num;
        }
    }

    private void Close(Dictionary<string, object> slot, List<Dictionary<string, object>> pool)
    {
        if (slot.TryGetValue("pid", out var value) && value is int value2)
        {
            ProcessUtil.KillPid(value2);
        }
        Clear((string)slot["acct"]);
        pool.Remove(slot);
    }

    private void PrepareClean()
    {
        Log("\ud83e\uddf9 закрываю старые окна Roblox…");
        ProcessUtil.KillAllRoblox();
        WindowManager.ResetCounter();
        if (_settings.LightFps > 0)
        {
            FastFlags.ApplyLowLoad(_settings.LightFps, Log);
            FastFlags.ApplyGameSettings(_settings.LightFps);
        }
        else
        {
            FastFlags.Remove();
            FastFlags.ApplyGameSettings(0);
        }
        RobloxLauncher.HoldSingletonMutex();
        for (int i = 0; i < 40; i++)
        {
            if (RobloxLauncher.SingletonOwned)
            {
                break;
            }
            if (_ct.IsCancellationRequested)
            {
                break;
            }
            Sleep(100);
        }
    }

    private void EnsureMain(string mode)
    {
        if (string.IsNullOrEmpty(Main))
        {
            Log("[!] главный не задан — выбери в окне");
            return;
        }
        if (!_store.Usernames().Any((string n) => string.Equals(n, Main, StringComparison.OrdinalIgnoreCase)))
        {
            Notify("[!] Главного '" + Main + "' нет в списке акков — загрузи его куку. Прерываю.");
            throw new OperationCanceledException("нет главного");
        }
        if (_mainPid.HasValue)
        {
            ProcessUtil.KillPid(_mainPid);
        }
        Clear(Main);
        try
        {
            File.Delete(Path.Combine(_doneDir, "_mainjob.txt"));
        }
        catch
        {
        }
        Log("запускаю главного " + Main + ", жду захода…");
        _mainPid = Launch(Main, "public");
        DateTime utcNow = DateTime.UtcNow;
        while ((DateTime.UtcNow - utcNow).TotalSeconds < 60.0 && !_ct.IsCancellationRequested)
        {
            if (Joined(Main))
            {
                DateTime utcNow2 = DateTime.UtcNow;
                while (MainJobId() == null && (DateTime.UtcNow - utcNow2).TotalSeconds < 20.0 && !_ct.IsCancellationRequested)
                {
                    Sleep(1000);
                }
                Log("✅ главный " + Main + " зашёл — запускаю альтов");
                Sleep(5000);
                return;
            }
            Sleep(1000);
        }
        if (!_ct.IsCancellationRequested)
        {
            Notify("⚠\ufe0f главный " + Main + " не зашёл за 60с — всё равно продолжаю");
            Sleep(3000);
        }
    }

    public void Run()
    {
        List<string> list = _queueOverride ?? AltQueue();
        _num = list.Select((string a, int i) => (a: a, i: i)).ToDictionary(((string a, int i) x) => x.a, ((string a, int i) x) => x.i + 1);
        _total = list.Count;
        if (list.Count == 0)
        {
            Log("Очередь пуста — всё уже сдано.");
            return;
        }
        Directory.CreateDirectory(_doneDir);
        if (_queueOverride != null)
        {
            WriteRoutes(list);
        }
        WriteRuntime();
        SetActive("");
        SetMode("collect");
        Notify($"▶\ufe0f Старт сбора лута: {list.Count} альтов");
        ClearCounts();
        _runItems = 0;
        _runUsd = 0.0;
        _runTop.Clear();
        _runBreakdown.Clear();
        if (_prices.IsStale(24.0) || !_prices.HasGame(_game.Key))
        {
            _prices.RefreshAsync(_game.Key, Log);
        }
        if (!_skipClean)
        {
            PrepareClean();
        }
        if (!_mainAlreadyUp)
        {
            EnsureMain("public");
        }
        else
        {
            Log("приёмник " + Main + " уже в игре — сразу запускаю сдающих");
        }
        if (_ct.IsCancellationRequested)
        {
            if (_mainPid.HasValue)
            {
                ProcessUtil.KillPid(_mainPid);
            }
            SetActive("");
            Log("[остановлено]");
            return;
        }
        PassResult passResult = PoolPass(list, "follow", detectUnder13: false);
        RetryPass(passResult.failed);
        SetActive("");
        string text = "";
        if (_reportFullInventory)
        {
            var (num, value) = DumpMainInventory();
            if (num > 0)
            {
                text = $"\n\ud83d\udcb0 Инвентарь главного {Main}: {num} предметов на ~{value:0.00}$";
            }
        }
        if (!_keepMainAlive && _mainPid.HasValue)
        {
            ProcessUtil.KillPid(_mainPid);
        }
        string m = RunSummary(list, _runItems) + text;
        Log(m);
        MaybeDistribute();
    }

    public static void RunStreams(GameDef game, Settings settings, AccountStore store, Action<string> log, CancellationToken ct, List<string> mains, bool consolidate)
    {
        mains = mains.Where((string m) => !string.IsNullOrWhiteSpace(m)).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
        if (mains.Count <= 1)
        {
            new Orchestrator(game, settings, store, log, ct, null, mains.FirstOrDefault(), null, null, skipClean: false, keepMainAlive: false, mainAlreadyUp: false, reportFullInventory: true).Run();
            return;
        }
        Orchestrator orchestrator = new Orchestrator(game, settings, store, log, ct);
        log($"▶\ufe0f Многопоток: {mains.Count} потоков — главные: {string.Join(", ", mains)}");
        orchestrator.ClearRoutes();
        orchestrator.PrepareClean();
        if (ct.IsCancellationRequested)
        {
            return;
        }
        List<string> list = orchestrator.AltQueueExcluding(mains);
        List<string>[] array = (from _ in Enumerable.Range(0, mains.Count)
                                select new List<string>()).ToArray();
        for (int i = 0; i < list.Count; i++)
        {
            array[i % mains.Count].Add(list[i]);
        }
        log($"   альтов в очереди: {list.Count} → по потокам: {string.Join(" / ", array.Select((List<string> g) => g.Count))}");
        string[] array2 = new string[mains.Count];
        Orchestrator[] array3 = new Orchestrator[mains.Count];
        List<Task> list2 = new List<Task>();
        for (int j = 0; j < mains.Count; j++)
        {
            int idx = j;
            string text = Paths.DoneDir(game.Key, Paths.Workspace(settings.AutoexecDir));
            string doneDir = ((idx == 0) ? text : (text + "_s" + idx));
            array2[idx] = Path.Combine(Paths.StateDir, $"progress_{game.Key}_s{idx}.json");
            array3[idx] = new Orchestrator(game, settings, store, delegate (string m)
            {
                log($"[П{idx + 1}] {m}");
            }, ct, doneDir, mains[idx], array[idx], array2[idx], skipClean: true, idx == 0 && consolidate);
            Orchestrator o = array3[idx];
            list2.Add(Task.Run(delegate
            {
                try
                {
                    o.Run();
                }
                catch (Exception ex)
                {
                    log($"[П{idx + 1}] ошибка: {ex.Message}");
                }
            }, ct));
        }
        try
        {
            Task.WaitAll(list2.ToArray());
        }
        catch
        {
        }
        string[] array4 = array2;
        foreach (string path in array4)
        {
            StateData stateData = ReadStateFile(path);
            orchestrator._state.done.AddRange(stateData.done);
            orchestrator._state.failed.AddRange(stateData.failed);
            orchestrator._state.broken.AddRange(stateData.broken);
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
        orchestrator.SaveState();
        orchestrator.ClearRoutes();
        if (ct.IsCancellationRequested)
        {
            log("[остановлено]");
            ProcessUtil.KillAllRoblox();
            return;
        }
        if (consolidate)
        {
            List<string> list3 = mains.Skip(1).ToList();
            int? mainPid = array3[0].MainPid;
            if (ProcessUtil.IsRunning(mainPid))
            {
                log($"\ud83d\udd17 Свожу лут на {mains[0]} (с {list3.Count} акков) — приёмник не перезапускаю…");
                ProcessUtil.KillAllRobloxExcept(mainPid);
                new Orchestrator(game, settings, store, log, ct, null, mains[0], list3, null, skipClean: true, keepMainAlive: true, mainAlreadyUp: true, reportFullInventory: true).Run();
                ProcessUtil.KillPid(mainPid);
            }
            else
            {
                log($"\ud83d\udd17 Свожу лут на {mains[0]} (с {list3.Count} акков)…");
                orchestrator.PrepareClean();
                new Orchestrator(game, settings, store, log, ct, null, mains[0], list3, null, skipClean: false, keepMainAlive: false, mainAlreadyUp: false, reportFullInventory: true).Run();
            }
        }
        log("\ud83c\udfc1 Многопоточный прогон завершён.");
    }

    private static StateData ReadStateFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<StateData>(File.ReadAllText(path)) ?? new StateData();
            }
        }
        catch
        {
        }
        return new StateData();
    }

    private void RetryPass(List<string> failed, int rounds = 1)
    {
        for (int i = 1; i <= rounds; i++)
        {
            if (_ct.IsCancellationRequested)
            {
                break;
            }
            List<string> list = (from a in failed.Distinct()
                                 where !_state.done.Contains(a)
                                 select a).ToList();
            if (list.Count == 0)
            {
                break;
            }
            Log($"[*] Ретрай-проход {i}: {list.Count} упавших");
            HashSet<string> rq = new HashSet<string>(list);
            _state.failed = _state.failed.Where((string a) => !rq.Contains(a)).ToList();
            SaveState();
            EnsureMain("public");
            failed = PoolPass(list, "follow", detectUnder13: false).failed;
        }
    }

    private void MaybeDistribute()
    {
        List<DistTarget> list = ((_game.Key == "adoptme") ? _settings.AdoptmeDistribute : new List<DistTarget>());
        if (list != null && list.Count != 0)
        {
            HashSet<string> inStore = new HashSet<string>(_store.Usernames(), StringComparer.OrdinalIgnoreCase);
            if (!list.Any((DistTarget t) => inStore.Contains(t.Account)))
            {
                Notify("ℹ\ufe0f Расформировка пропущена — получателей нет в списке акков");
                return;
            }
            Notify("\ud83d\udce6 Сбор завершён -> запускаю расформировку");
            Distribute();
        }
    }

    public void Distribute()
    {
        List<DistTarget> source = ((_game.Key == "adoptme") ? _settings.AdoptmeDistribute : new List<DistTarget>());
        HashSet<string> inStore = new HashSet<string>(_store.Usernames(), StringComparer.OrdinalIgnoreCase);
        List<DistTarget> list = source.Where((DistTarget t) => inStore.Contains(t.Account)).ToList();
        if (list.Count == 0)
        {
            Log("[!] нет загруженных получателей — нечего раздавать");
            return;
        }
        Directory.CreateDirectory(_doneDir);
        WriteRuntime();
        ClearCounts();
        SetMode("distribute");
        SetActive("");
        SetCategory("");
        Notify($"▶\ufe0f Расформировка: {list.Count} акков (источник {Main})");
        PrepareClean();
        EnsureMain("public");
        int num = 0;
        int num2 = 0;
        int num3 = 0;
        foreach (DistTarget item in list)
        {
            if (_ct.IsCancellationRequested)
            {
                break;
            }
            num3++;
            string text = $"{num3}/{list.Count} {item.Account}";
            Notify($"\ud83d\udce6 {text}: получает '{item.Category}'");
            if (DistributeOne(item.Account, item.Category, text))
            {
                num++;
            }
            else
            {
                num2++;
            }
            SetActive("");
            SetCategory("");
            SaveState();
        }
        SetMode("collect");
        if (_mainPid.HasValue)
        {
            ProcessUtil.KillPid(_mainPid);
        }
        int value = TotalItems();
        Notify($"\ud83c\udfc1 Расформировка готова. Роздано на {num} акков, провал: {num2}, всего предметов: {value}");
    }

    private bool WaitJoin(string acct, string tag)
    {
        DateTime utcNow = DateTime.UtcNow;
        double attachTimeoutSec = _game.Batch.AttachTimeoutSec;
        double joinDetectSec = _game.Batch.JoinDetectSec;
        while (!_ct.IsCancellationRequested)
        {
            if (Joined(acct))
            {
                return true;
            }
            if (Errored(acct))
            {
                Log("     [x] " + tag + " ошибка " + ReadErr(acct));
                return false;
            }
            double totalSeconds = (DateTime.UtcNow - utcNow).TotalSeconds;
            if ((!Attached(acct) && totalSeconds > attachTimeoutSec) || totalSeconds > joinDetectSec)
            {
                Log($"     [x] {tag} не зашёл за {(int)totalSeconds}с");
                return false;
            }
            Sleep(1000);
        }
        return false;
    }

    private bool WaitDistributeDone(string acct, string tag)
    {
        DateTime utcNow = DateTime.UtcNow;
        double activeTimeoutSec = _game.Batch.ActiveTimeoutSec;
        while ((DateTime.UtcNow - utcNow).TotalSeconds < activeTimeoutSec && !_ct.IsCancellationRequested)
        {
            if (Flag(acct))
            {
                return true;
            }
            if (Failed(acct))
            {
                Log("     [x] " + tag + " сбой получателя (.fail)");
                return false;
            }
            Sleep(1000);
        }
        Log("     [x] " + tag + " таймаут раздачи");
        return false;
    }

    private bool DistributeOne(string acct, string cat, string tag)
    {
        int num = _game.Batch.MaxRetries + 1;
        for (int i = 1; i <= num; i++)
        {
            if (_ct.IsCancellationRequested)
            {
                return false;
            }
            Clear(acct);
            SetCategory(cat);
            SetActive("");
            Log($"  -> запускаю {tag} (follow), попытка {i}/{num}");
            int? pid = Launch(acct, "follow");
            if (!WaitJoin(acct, tag))
            {
                ProcessUtil.KillPid(pid);
                Clear(acct);
                continue;
            }
            Notify($"\ud83d\udd04 {tag} зашёл — раздаю '{cat}'");
            SetActive(acct);
            bool num2 = WaitDistributeDone(acct, tag);
            ProcessUtil.KillPid(pid);
            if (num2)
            {
                int value = ReadCount(acct);
                Notify($"✅ {tag} получил {value} ('{cat}')");
                Clear(acct);
                return true;
            }
            Clear(acct);
            SetActive("");
        }
        Notify($"❌ {tag} не удалось раздать '{cat}'");
        return false;
    }

    private string RunSummary(List<string> queue, int items)
    {
        HashSet<string> source = new HashSet<string>(queue);
        int value = source.Count((string a) => _state.done.Contains(a));
        int value2 = source.Count((string a) => _state.failed.Contains(a) && !_state.done.Contains(a));
        string text = $"\ud83c\udfc1 Готово. Сдали: {value}/{queue.Count} акков, провал: {value2}";
        if (items > 0)
        {
            text += $", предметов передано: {items}";
        }
        if (_runUsd > 0.0)
        {
            text += $"\n\ud83d\udcb2 Передано на ~{_runUsd:0.00}$";
        }
        var list = (from t in _runTop
                    group t by t.name into g
                    select new
                    {
                        Name = g.Key,
                        Price = g.Max(((string name, double price) x) => x.price)
                    } into x
                    orderby x.Price descending
                    select x).Take(3).ToList();
        if (list.Count > 0)
        {
            text = text + "\n\ud83c\udfc6 Топ-3: " + string.Join(", ", list.Select(t => $"{t.Name} (~{t.Price:0.00}$)"));
        }
        return text;
    }

    private void Report(string title, PassResult res)
    {
        List<string> list = new List<string>
        {
            title,
            $"✅ Сдали ({res.done.Count}): " + ((res.done.Count > 0) ? string.Join(", ", res.done) : "—")
        };
        if (res.rerouted.Count > 0)
        {
            list.Add($"\ud83d\udd1e Под-13 ({res.rerouted.Count}): " + string.Join(", ", res.rerouted));
        }
        if (res.failed.Count > 0)
        {
            list.Add($"❌ Ошибки ({res.failed.Count}): " + string.Join(", ", res.failed));
        }
        Notify(string.Join("\n", list));
    }

    private PassResult PoolPass(List<string> queue, string mode, bool detectUnder13)
    {
        List<string> list = new List<string>(queue);
        Dictionary<string, int> d2 = queue.ToDictionary((string n) => n, (string _) => 0);
        Dictionary<string, int> d3 = queue.ToDictionary((string n) => n, (string _) => 0);
        Dictionary<string, int> d4 = queue.ToDictionary((string n) => n, (string _) => 0);
        List<Dictionary<string, object>> list2 = new List<Dictionary<string, object>>();
        string text = null;
        PassResult passResult = new PassResult();
        int size = _game.Batch.Size;
        double joinDetectSec = _game.Batch.JoinDetectSec;
        double attachTimeoutSec = _game.Batch.AttachTimeoutSec;
        DateTime dateTime = DateTime.MinValue;
        while ((list.Count > 0 || list2.Count > 0) && !_ct.IsCancellationRequested)
        {
            if (list2.Count < size && list.Count > 0 && (DateTime.UtcNow - dateTime).TotalSeconds >= _game.Batch.LaunchDelaySec)
            {
                string text2 = list[0];
                list.RemoveAt(0);
                Clear(text2);
                Log("▶ запускаю " + Tag(text2));
                int? num = Launch(text2, mode);
                list2.Add(new Dictionary<string, object>
                {
                    ["acct"] = text2,
                    ["pid"] = num,
                    ["t"] = DateTime.UtcNow,
                    ["joined"] = false,
                    ["active_since"] = null
                });
                dateTime = DateTime.UtcNow;
            }
            foreach (Dictionary<string, object> item in list2.ToList())
            {
                string text3 = (string)item["acct"];
                bool flag = true;
                if (Errored(text3))
                {
                    string value = ReadErr(text3);
                    Notify($"⛔ {Tag(text3)} ошибка {value} — пропуск (запомнен)");
                    passResult.failed.Add(text3);
                    _state.failed.Add(text3);
                    if (!_state.broken.Contains(text3))
                    {
                        _state.broken.Add(text3);
                    }
                }
                else if (Flag(text3))
                {
                    int num2 = ReadCount(text3);
                    _runItems += num2;
                    var (num3, list3, dictionary) = ValueOf(text3);
                    _runUsd += num3;
                    _runTop.AddRange(list3);
                    foreach (KeyValuePair<string, int> item2 in dictionary)
                    {
                        _runBreakdown.TryGetValue(item2.Key, out var value2);
                        _runBreakdown[item2.Key] = value2 + item2.Value;
                    }
                    try
                    {
                        _stats.AddRun(_game.Key, num2, num3, list3, dictionary, text3);
                    }
                    catch
                    {
                    }
                    Notify((num2 > 0) ? $"✅ {Tag(text3)} — сдал {num2} предметов (\ud83d\udcb2~{num3:0.00}$)" : ("\ud83d\udced " + Tag(text3) + " — инвентарь пуст (нечего сдавать)"));
                    passResult.done.Add(text3);
                    _state.done.Add(text3);
                }
                else if (Rejoined(text3))
                {
                    int num4 = Inc(d4, text3);
                    if (num4 <= 6)
                    {
                        Log($"     [↻] {Tag(text3)} главного нет в его сервере -> реджоин {num4}/{6}");
                        list.Add(text3);
                    }
                    else
                    {
                        Notify($"❌ {Tag(text3)} не попал к серверу главного ({6} реджоинов)");
                        passResult.failed.Add(text3);
                        _state.failed.Add(text3);
                    }
                }
                else if (Failed(text3))
                {
                    int num5 = Inc(d2, text3);
                    if (num5 <= _game.Batch.MaxRetries)
                    {
                        Notify($"⚠\ufe0f {Tag(text3)} сбой, ретрай {num5}");
                        list.Add(text3);
                    }
                    else
                    {
                        Notify("❌ " + Tag(text3) + " провал");
                        passResult.failed.Add(text3);
                        _state.failed.Add(text3);
                    }
                }
                else
                {
                    flag = false;
                }
                if (flag)
                {
                    Close(item, list2);
                    if (text3 == text)
                    {
                        text = null;
                    }
                }
            }
            foreach (Dictionary<string, object> item3 in list2.ToList())
            {
                if ((bool)item3["joined"])
                {
                    continue;
                }
                string text4 = (string)item3["acct"];
                if (Joined(text4))
                {
                    item3["joined"] = true;
                    continue;
                }
                double totalSeconds = (DateTime.UtcNow - (DateTime)item3["t"]).TotalSeconds;
                bool flag2 = Attached(text4);
                if ((flag2 || !(totalSeconds > attachTimeoutSec)) && !(totalSeconds > joinDetectSec))
                {
                    continue;
                }
                string value3 = (flag2 ? "застрял после атача" : "не атачнулся");
                if (detectUnder13)
                {
                    Notify($"\ud83d\udd1e {Tag(text4)} {value3} -> публичка");
                    passResult.rerouted.Add(text4);
                    Close(item3, list2);
                }
                else
                {
                    int num6 = Inc(d3, text4);
                    if (num6 <= 3)
                    {
                        Log($"     [wait] {Tag(text4)} {value3} -> повтор {num6}");
                        list.Add(text4);
                    }
                    else
                    {
                        Notify($"⚠\ufe0f {Tag(text4)} {value3} — перечекается позже");
                        passResult.failed.Add(text4);
                        _state.failed.Add(text4);
                    }
                    Close(item3, list2);
                }
                if (text4 == text)
                {
                    text = null;
                }
            }
            if (text == null)
            {
                Dictionary<string, object> dictionary2 = list2.FirstOrDefault((Dictionary<string, object> s) => (bool)s["joined"]);
                if (dictionary2 != null)
                {
                    text = (string)dictionary2["acct"];
                    dictionary2["active_since"] = DateTime.UtcNow;
                    SetActive(text);
                }
            }
            foreach (Dictionary<string, object> item4 in list2.ToList())
            {
                string text5 = (string)item4["acct"];
                if (text5 == text && item4["active_since"] is DateTime dateTime2 && (DateTime.UtcNow - dateTime2).TotalSeconds > _game.Batch.ActiveTimeoutSec)
                {
                    int num7 = Inc(d2, text5);
                    if (num7 <= _game.Batch.MaxRetries)
                    {
                        Log($"     [~] {Tag(text5)} таймаут -> ретрай {num7}");
                        list.Add(text5);
                    }
                    else
                    {
                        Notify("❌ " + Tag(text5) + " провал (таймаут)");
                        passResult.failed.Add(text5);
                        _state.failed.Add(text5);
                    }
                    Close(item4, list2);
                    text = null;
                }
            }
            SaveState();
            Sleep(1000);
        }
        if (_ct.IsCancellationRequested)
        {
            foreach (Dictionary<string, object> item5 in list2.ToList())
            {
                Close(item5, list2);
            }
        }
        SetActive("");
        return passResult;
        static int Inc(Dictionary<string, int> d, string k)
        {
            d.TryGetValue(k, out var value4);
            d[k] = value4 + 1;
            return value4 + 1;
        }
    }
}