using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avarice;

/// <summary>
/// IPC 端點的「遊戲主執行緒閘門」。
/// </summary>
/// <remarks>
/// CallGate 是<b>直接方法呼叫</b>，提供端的碼跑在<b>呼叫端的執行緒</b>上；物件表與 <c>LocalPlayer</c> 由遊戲主執行緒
/// 每幀重建，從別的執行緒讀到一半被換掉就是 <c>try</c>/<c>catch</c> 攔不到的 AccessViolationException。
/// <para>
/// 所以對外端點把<b>整個方法體</b>交回主執行緒執行，不是只有第一行檢查 —— 下游的 helper 也一起被覆蓋。
/// 已經在主執行緒上呼叫時就地執行：不配置 Task、不改變例外型別、不多花任何一幀。
/// </para>
/// <para>
/// ⚠️ <b>逾時的處置</b>：等主執行緒最多 <see cref="TimeoutMs"/> 毫秒。逾時就回該端點的
/// 「不可用」值（<c>Avarice.CardinalDirection</c> 是 <c>(CardinalDirection)(-1)</c>，與算不出來時相同），
/// 因為呼叫端本來就要處理那個狀態。
/// 同時用 <see cref="Interlocked"/> 把還沒開始跑的工作標成放棄，避免「呼叫端已經拿到失敗
/// 走人了，五秒後動作才真的排進佇列」這種形狀。
/// </para>
/// <para>
/// 🔴 用 <c>RunOnFrameworkThread</c> 不是 <c>Framework.Run</c>：前者在已經是主執行緒時
/// 就地執行，同步等它不會死結；後者一律 <c>StartNew</c>，同步等會死結。
/// </para>
/// <para>
/// 📌 形狀對齊 <c>TCToolbox/TCToolbox/Core/IpcFrameworkGate.cs</c>（同一份閘門的移植）。
/// </para>
/// </remarks>
internal static class IpcFrameworkGate
{
    /// <summary>等主執行緒的上限。超過就當作「現在做不到」。</summary>
    internal const int TimeoutMs = 5000;

    private const int StatePending = 0;
    private const int StateRunning = 1;
    private const int StateAbandoned = 2;

    /// <summary>有回傳值的端點。<paramref name="unavailable"/> 是逾時時要回的「不可用」值。</summary>
    internal static T Get<T>(string endpoint, Func<T> body, T unavailable)
    {
        if (Svc.Framework.IsInFrameworkUpdateThread) return body();

        if (IsUnloading(endpoint)) return unavailable;

        var state = StatePending;
        var task = Svc.Framework.RunOnFrameworkThread(() =>
        {
            // 呼叫端已經逾時走人了就什麼都不做。
            if (Interlocked.CompareExchange(ref state, StateRunning, StatePending) != StatePending) return unavailable;
            return body();
        });

        // 🔴 WaitAny 對已經失敗的工作也回 0（不擲），交給 GetResult 原樣重擲原始例外，
        // 這樣呼叫端看到的例外型別與沒有這層閘門時完全一樣（不會變成 AggregateException）。
        if (Task.WaitAny([task], TimeoutMs) == 0) return task.GetAwaiter().GetResult();

        ReportTimeout(endpoint, Interlocked.CompareExchange(ref state, StateAbandoned, StatePending) == StatePending);
        return unavailable;
    }

    /// <summary>沒有回傳值的端點。</summary>
    internal static void Run(string endpoint, Action body)
    {
        if (Svc.Framework.IsInFrameworkUpdateThread)
        {
            body();
            return;
        }

        if (IsUnloading(endpoint)) return;

        var state = StatePending;
        var task = Svc.Framework.RunOnFrameworkThread(() =>
        {
            if (Interlocked.CompareExchange(ref state, StateRunning, StatePending) != StatePending) return;
            body();
        });

        if (Task.WaitAny([task], TimeoutMs) == 0)
        {
            task.GetAwaiter().GetResult();
            return;
        }

        ReportTimeout(endpoint, Interlocked.CompareExchange(ref state, StateAbandoned, StatePending) == StatePending);
    }

    /// <summary>同一個端點的逾時訊息重印間隔。</summary>
    private const long TimeoutLogIntervalMs = 10000;

    /// <summary>節流表上限，避免端點名意外發散時無限成長。</summary>
    private const int MaxTrackedTimeoutKeys = 128;

    private static readonly Dictionary<string, long> TimeoutLogTimes = [];

    /// <summary>
    /// Dalamud 卸載期的閘門旁路：<c>Framework.RunOnFrameworkThread</c> 在 <c>IsFrameworkUnloading</c> 為真時
    /// 會<b>就地在呼叫端執行緒</b>執行 body，等於這一層完全失效。
    /// 所以卸載期一律直接回該端點的「不可用」值：功能失效可以接受，卸載期的 AccessViolationException 不行。
    /// 已經在 framework 執行緒上時不受影響。
    /// </summary>
    private static bool IsUnloading(string endpoint)
    {
        if (!Svc.Framework.IsFrameworkUnloading || Svc.Framework.IsInFrameworkUpdateThread) return false;
        if (ShouldLogTimeout(endpoint + "/卸載期"))
        {
            Svc.Log.Information($"[Avarice IPC 閘門] {endpoint} 在 Dalamud 卸載期從別的執行緒被呼叫，已回傳「不可用」值。卸載期的 RunOnFrameworkThread 會就地在呼叫端執行緒執行，閘門保護不了原生記憶體存取；此時功能失效可以接受，崩潰不行。");
        }
        return true;
    }

    /// <summary>
    /// 自帶的節流：首次必放行，之後每 <see cref="TimeoutLogIntervalMs"/> 毫秒放行一次。
    /// </summary>
    /// <remarks>
    /// 🔴 這裡刻意<b>不用</b> <c>EzThrottler</c>：它是整個外掛共用的靜態裸 <c>Dictionary</c>
    /// 且零同步，而這條路徑跑在呼叫端的執行緒上，並行插入弄壞的是整張表 ——
    /// 連帶弄壞外掛裡所有模組的節流。所以自帶字典＋自己的鎖。
    /// 🔴 鎖內只碰字典 —— 不寫 log、不做 I/O、不呼叫任何別的外掛。
    /// </remarks>
    private static bool ShouldLogTimeout(string key)
    {
        var now = Environment.TickCount64;
        lock (TimeoutLogTimes)
        {
            if (TimeoutLogTimes.TryGetValue(key, out var last) && now - last < TimeoutLogIntervalMs) return false;
            if (TimeoutLogTimes.Count >= MaxTrackedTimeoutKeys && !TimeoutLogTimes.ContainsKey(key)) TimeoutLogTimes.Clear();
            TimeoutLogTimes[key] = now;
            return true;
        }
    }

    /// <summary>
    /// 要使用者回報的診斷。
    /// </summary>
    private static void ReportTimeout(string endpoint, bool abandoned)
    {
        if (!ShouldLogTimeout(endpoint)) return;

        var outcome = abandoned
            ? "工作還沒開始就被取消，什麼都沒做"
            : "工作已經開始執行，會照常跑完（呼叫端拿到的回值不代表它沒發生）";
        Svc.Log.Information(
            $"[Avarice IPC 閘門] {endpoint} 等待遊戲主執行緒超過 {TimeoutMs} 毫秒，已回傳「不可用」值。{outcome}。" +
            "通常代表遊戲正在讀取畫面或嚴重掉幀；若持續出現請回報。");
    }
}
