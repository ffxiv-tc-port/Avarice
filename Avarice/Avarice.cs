using Avarice.Data;
using Avarice.Positional;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.EzSharedDataManager;
using ECommons.GameHelpers;
using ECommons.MathHelpers;
using ECommons.Schedulers;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using PunishLib;

#pragma warning disable CS0649

namespace Avarice;

public unsafe class Avarice : IDalamudPlugin
{
    public string Name
    {
        get
        {
            return "Avarice";
        }
    }

    internal Config config;
    internal Profile currentProfile;
    internal static Avarice P;
    private WindowSystem windowSystem;
    internal ConfigWindow configWindow;
    private Canvas canvas;
    internal Memory memory;

    internal static uint[] PositionalJobs = new uint[] { 2, 4, 29, 30, 20, 34, 39, 22 };
    internal uint Job = 0;
    internal HashSet<uint> StaticAutoDetectRadiusData;
    internal PositionalManager PositionalManager;
    internal uint[] PositionalStatus;
    internal RotationSolverWatcher RotationSolverWatcher;

    public Avarice(IDalamudPluginInterface pi)
    {
        P = this;
        ECommonsMain.Init(pi, this, Module.DalamudReflector, Module.ObjectFunctions);
        ECommons.LanguageHelpers.Localization.Init("ChineseTraditional");
        PunishLibMain.Init(pi, Svc.PluginInterface.InternalName, new AboutPlugin() { IconPath = LocalIconPath(pi) }, PunishOption.DefaultKoFi);
        _ = new TickScheduler(delegate
        {
            PositionalStatus = EzSharedData.GetOrCreate<uint[]>("Avarice.PositionalStatus", [0, 0]);
            config = Svc.PluginInterface.GetPluginConfig() as Config ?? new();
            if (config.Profiles.Count == 0)
            {
                config.Profiles.Add(new() { Name = "Default profile", IsDefault = true });
            }
            currentProfile = config.Profiles.FirstOr0(x => x.IsDefault);
            RotationSolverWatcher = new();
            memory = new();
            windowSystem = new();
            configWindow = new();
            windowSystem.AddWindow(configWindow);
            canvas = new();
            windowSystem.AddWindow(canvas);
            Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
            Svc.PluginInterface.UiBuilder.OpenConfigUi += delegate { configWindow.IsOpen = true; };
            Svc.Condition.ConditionChange += OnConditionChange;
            _ = Svc.Commands.AddHandler("/avarice", new CommandInfo((string cmd, string args) =>
            {
                if (args == "debug")
                {
                    P.currentProfile.Debug = !P.currentProfile.Debug;
                    Svc.Chat.Print($"Debug mode {(P.currentProfile.Debug ? "enabled" : "disabled")}");
                }
                else if (args == "draw") // Added new command for toggling drawing
                {
                    P.currentProfile.DrawingEnabled = !P.currentProfile.DrawingEnabled;
                    Svc.Chat.Print("Drawing ??".Loc(P.currentProfile.DrawingEnabled ? "enabled".Loc() : "disabled".Loc()));
                }
                else
                {
                    configWindow.IsOpen = !configWindow.IsOpen;
                }
            })
            { HelpMessage = "Toggle configuration window. Use '/avarice draw' to toggle drawing, '/avarice debug' for debug mode.".Loc() });
            LuminaSheets.Init();
            Svc.PluginInterface.GetIpcProvider<IntPtr, CardinalDirection>("Avarice.CardinalDirection").RegisterFunc(GetCardinalDirectionForObject);
            Svc.Framework.Update += Tick;
            StaticAutoDetectRadiusData = Util.LoadStaticAutoDetectRadiusData();
            if (config.SplatoonUnsafePixel)
            {
                TabSplatoon.WriteRequest();
            }

            ActionWatching.Enable();
            ComboCache.ComboCacheInstance = new ComboCache();

            PositionalManager = new();
        });
    }

    /// <summary>
    /// 出貨包裡與外掛庫圖示相同的 images/about-icon.png；不存在時回 null（關於分頁沿用 manifest 的 IconUrl）。
    /// 不用 images/icon.png 這個檔名：DalamudPackager 打包時會把它從 latest.zip 剔除。
    /// </summary>
    private static string LocalIconPath(IDalamudPluginInterface pluginInterface)
    {
        var dir = pluginInterface.AssemblyLocation.DirectoryName;
        if (dir is null) return null;
        var path = System.IO.Path.Combine(dir, "images/about-icon.png");
        return System.IO.File.Exists(path) ? path : null;
    }

    /// <summary>
    /// IPC <c>Avarice.CardinalDirection</c>：算出某個物件相對於本地玩家的方位。
    /// </summary>
    /// <remarks>
    /// 跑在呼叫端的執行緒上（CallGate 是直接方法呼叫），所以整段交回主執行緒執行
    /// （<see cref="IpcFrameworkGate"/>）；已經在主執行緒上呼叫時就地執行。
    /// <para>
    /// 參數是呼叫端給的裸原生位址，轉回主執行緒不會讓它變有效：<c>CreateObjectReference</c>
    /// 進去第一件事就是解參考它，位址已死就是攔不到的 <c>AccessViolationException</c>。
    /// 所以先在本幀的物件表裡找出位址相符的那一筆（只比對 <c>Address</c>，不解參考）才讀座標與朝向。
    /// </para>
    /// <para>
    /// 刻意的收窄：不在物件表裡的位址回 <c>(CardinalDirection)(-1)</c>，與「算不出來」同一個失敗值。
    /// </para>
    /// </remarks>
    private CardinalDirection GetCardinalDirectionForObject(IntPtr arg)
    {
        return IpcFrameworkGate.Get("Avarice.CardinalDirection", () =>
        {
            var me = Svc.Objects.LocalPlayer;
            if (me == null) return (CardinalDirection)(-1);

            // 本地玩家的座標當場抄走，之後就不再碰 LocalPlayer。
            var myPos = me.Position;

            var obj = FindLiveObjectByAddress(arg);
            if (obj == null) return (CardinalDirection)(-1);

            return MathHelper.GetCardinalDirection((MathHelper.GetRelativeAngle(myPos, obj.Position) + obj.Rotation.RadToDeg()) % 360);
        }, (CardinalDirection)(-1));
    }

    /// <summary>
    /// 在<b>本幀</b>的物件表裡找出位址等於 <paramref name="address"/> 的那一筆；
    /// 找不到回 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 🔴 只比對 <c>Address</c> 這個欄位，<b>不解參考</b> —— 所以拿一根已經死掉的位址進來
    /// 也只會比不中，不會崩。
    /// 🔴 回傳的包裝<b>只在這一次呼叫裡用完就丟</b>，不存進任何欄位：本 pin 的
    /// <c>ObjectTable</c> 是每格預先配好一個包裝、存取時就地改寫它的 <c>Address</c> 再交出來
    /// ⇒ 跨幀持有會靜默換人或懸空。
    /// ⚠️ 只能在遊戲主執行緒上呼叫（呼叫點已由 <see cref="IpcFrameworkGate"/> 保證）。
    /// </remarks>
    private static IGameObject FindLiveObjectByAddress(IntPtr address)
    {
        if (address == IntPtr.Zero) return null;

        var length = Svc.Objects.Length;
        for (var i = 0; i < length; i++)
        {
            var o = Svc.Objects[i];
            if (o != null && o.Address == address) return o;
        }

        return null;
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {

        if (flag == ConditionFlag.InCombat)
        {
            Safe(delegate
            {
                if (value)
                {
                    PluginLog.Debug("Entered combat");
                }
                else
                {
                    PluginLog.Debug("Exited combat");
                    Svc.PluginInterface.SavePluginConfig(config);
                    if (currentProfile.Announce && !currentProfile.CurrentEncounterStats.Finished &&
              (currentProfile.CurrentEncounterStats.Hits > 0 || currentProfile.CurrentEncounterStats.Missed > 0))
                    {
                        var total = currentProfile.CurrentEncounterStats.Hits + currentProfile.CurrentEncounterStats.Missed;
                        var success = (int)(100f * currentProfile.CurrentEncounterStats.Hits / total);
                        Svc.Chat.Print(new SeStringBuilder()
                    .AddText("Positionals summary for encounter: ??/?? - ".Loc(currentProfile.CurrentEncounterStats.Hits, total))
                    .AddUiForeground($"{success}%", ECommons.GenericHelpers.GetParsedSeSetingColor(success))
                    .Build());
                    }
                    currentProfile.CurrentEncounterStats.Finished = true;
                }
            });
        }
    }

    internal static bool IsConditionMatching(DisplayCondition c)
    {
        if (c == DisplayCondition.Only_in_combat)
        {
            return Svc.Condition[ConditionFlag.InCombat];
        }
        else if (c == DisplayCondition.Only_in_duty)
        {
            return Svc.Condition[ConditionFlag.BoundByDuty56];
        }
        else if (c == DisplayCondition.In_duty_or_combat)
        {
            return Svc.Condition[ConditionFlag.InCombat] || Svc.Condition[ConditionFlag.BoundByDuty56];
        }
        else if (c == DisplayCondition.In_duty_and_combat)
        {
            return Svc.Condition[ConditionFlag.InCombat] && Svc.Condition[ConditionFlag.BoundByDuty56];
        }
        else
        {
            return true;
        }
    }

    internal void RecordStat(bool isMiss)
    {
        if (currentProfile.CurrentEncounterStats.Finished)
        {
            currentProfile.CurrentEncounterStats = new();
        }
        if (!currentProfile.Stats.ContainsKey((uint)Player.Job))
        {
            currentProfile.Stats[(uint)Player.Job] = new();
        }
        if (isMiss)
        {
            currentProfile.Stats[(uint)Player.Job].Missed++;
            currentProfile.CurrentEncounterStats.Missed++;
        }
        else
        {
            currentProfile.Stats[(uint)Player.Job].Hits++;
            currentProfile.CurrentEncounterStats.Hits++;
        }
    }

    internal Profile GetProfileForJob(uint job)
    {
        if (P.config.JobProfiles.TryGetValue(job, out var guid))
        {
            if (P.config.Profiles.TryGetFirst(x => x.GUID == guid, out var profile))
            {
                return profile;
            }
        }
        return null;
    }

    public void Dispose()
    {
        Safe(() => Svc.PluginInterface.SavePluginConfig(config));
        Svc.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        _ = Svc.Commands.RemoveHandler("/avarice");
        Svc.Condition.ConditionChange -= OnConditionChange;
        Svc.Framework.Update -= Tick;
        Safe(() =>
        {
            Svc.PluginInterface.GetIpcProvider<IntPtr, CardinalDirection>("Avarice.CardinalDirection").UnregisterFunc();
        });
        // PositionalManager 在 TickScheduler 裡才建立,Dispose 可能早於它 → 一律 ?. 並包 Safe。
        // 它訂閱了 Svc.ClientState.Login(方位表校準診斷的節流重置),不解訂閱會留下懸空處理常式。
        Safe(() => PositionalManager?.Dispose());
        memory.Dispose();
        ActionWatching.Dispose();
        ComboCache.ComboCacheInstance.Dispose();
        PunishLibMain.Dispose();
        ECommonsMain.Dispose();
        P = null;
    }

    private void Tick(object framework)
    {
        // 🔴 Framework.Instance() 可能合法回 null，裸解參考是攔不到的 AVE ⇒ 與寫入端
        //    （Drawing/Functions.DrawAnticipatedPos）共用 CurrentFrameCounter()。
        //    取不到就回 0：`0 - stamp > 1` 對 uint 環繞後幾乎必為真 ⇒ 方位狀態被清成 0，
        //    與「戳記過期」同一條路徑，不會顯示過期的方位資訊。
        if (Drawing.Functions.CurrentFrameCounter() - PositionalStatus[0] > 1)
        {
            PositionalStatus[1] = 0;
        }
        if (Svc.Objects.LocalPlayer != null)
        {
            var newJob = (uint)Player.Job;
            if (newJob != Job)
            {
                PluginLog.Debug($"Job changed from {Job} to {newJob}");
                var newJobProfile = GetProfileForJob(newJob);
                if (newJobProfile != null)
                {
                    currentProfile = newJobProfile;
                    PluginLog.Debug($"Switched profile to job profile {newJobProfile.Name}");
                }
                else
                {
                    if (GetProfileForJob(Job) != null)
                    {
                        currentProfile = P.config.Profiles.FirstOr0(x => x.IsDefault);
                        PluginLog.Debug($"Switched profile to default {currentProfile.Name}");
                    }
                }
            }
            Job = newJob;
        }
    }
}