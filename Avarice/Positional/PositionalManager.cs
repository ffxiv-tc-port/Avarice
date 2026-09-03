using System;
using System.Linq;

namespace Avarice.Positional;

public class PositionalManager : IDisposable
{
	// 方位倍率表已內嵌於組件中(Avarice/StaticData/PositionalPotencies.cs)。
	//
	// 舊版是啟動時去 Google 試算表抓 CSV,再把結果快取到 Svc.PluginInterface.AssemblyLocation
	// 底下。那個目錄帶版號(installedPlugins/Avarice/<版號>/positionals.csv),所以每次外掛更新
	// 快取就跟著消失;一旦當下又連不上試算表,_actionStore 就是空的,IsPositional() 恆為 false,
	// 全部方位回饋都會靜默停用 —— 而且只留下一行 Warning,使用者端看起來就只是「功能不見了」。
	// 內嵌之後不再有任何網路或磁碟相依,建構完成即可用。
	private readonly Dictionary<int, PositionalAction> _actionStore = new();

	// 方位表校準診斷:記下已回報過的 (技能, 實測 percent) 組合,同一組合每次登入只印一次。
	// 只在 framework thread(Memory.cs 的 action-effect hook)上存取,因此不需要鎖。
	// 刻意不持久化 —— 這是用來補表的取樣資料,不是設定。
	private readonly HashSet<(int ActionId, int Percent)> _reportedCalibrationMisses = [];

	public PositionalManager()
	{
		Load();
		Svc.ClientState.Login += OnLogin;
	}

	public void Dispose()
	{
		Svc.ClientState.Login -= OnLogin;
	}

	private void OnLogin()
	{
		// 每次登入重新開始取樣,否則長時間掛著的 session 回報過一次之後就再也不會回報。
		_reportedCalibrationMisses.Clear();
	}

	/// <summary>
	/// 重新載入內嵌方位資料。
	/// ⚠️ 讀取端(Memory.cs 的 action-effect hook)在 framework thread 上讀這個字典,
	/// 因此只能從 framework thread 呼叫,否則會與讀取端競爭。
	/// </summary>
	public void Reset()
	{
		Load();
	}

	private void Load()
	{
		_actionStore.Clear();

		foreach (StaticData.PositionalPotencies.Row row in StaticData.PositionalPotencies.Records)
		{
			if (!_actionStore.TryGetValue(row.Id, out PositionalAction action))
			{
				action = new PositionalAction
				{
					Id = row.Id,
					ActionName = row.Name,
					ActionPosition = row.Position,
					Positionals = [],
				};
				_actionStore.Add(row.Id, action);
			}

			action.Positionals[row.Percent] = new PositionalParameters
			{
				Percent = row.Percent,
				IsHit = row.IsHit,
				Comment = row.Comment,
			};
		}

		// 用 Information:使用者跑 LogLevel 1,這行是判斷「方位表到底有沒有載進來」的唯一依據。
		PluginLog.Information($"Loaded {_actionStore.Count} positional actions ({StaticData.PositionalPotencies.Records.Length} rows) from embedded table");
	}

	public bool IsPositionalHit(int actionId, int percent)
	{
		if (!_actionStore.TryGetValue(actionId, out PositionalAction action))
		{
			// 技能根本不在表裡 —— 呼叫端(Memory.cs)已經先用 IsPositional() 擋過,
			// 正常不會走到這裡;真走到了也不是校準訊號,不回報。
			return false;
		}

		if (!action.Positionals.TryGetValue(percent, out PositionalParameters parameters))
		{
			// 技能在表裡、但實測到的 percent 不在表內 —— 這正是「表列不全」的校準訊號。
			ReportCalibrationMiss(action, percent);
			return false;
		}

		return parameters.IsHit;
	}

	/// <summary>
	/// 回報一筆「表內有這個技能、但沒有這個 percent」的取樣。
	/// 同一個 (技能, percent) 組合每次登入只印一次,避免連續戰鬥洗版。
	/// </summary>
	private void ReportCalibrationMiss(PositionalAction action, int percent)
	{
		if (!_reportedCalibrationMisses.Add((action.Id, percent)))
		{
			return;
		}

		string known = string.Join(",", action.Positionals.Keys.OrderBy(x => x));
		PluginLog.Information($"[方位表校準] action={action.Id} ({action.ActionName}) 實測percent={percent} 表內=[{known}]");
	}

	public PositionalParameters GetPositionalParameters(int actionId, int percent)
	{
		if (!_actionStore.TryGetValue(actionId, out PositionalAction action))
		{
			return null;
		}

		return action.Positionals.GetValueOrDefault(percent);
	}

	public bool IsPositional(int actionId)
	{
		return _actionStore.ContainsKey(actionId);
	}
}
