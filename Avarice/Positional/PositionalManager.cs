namespace Avarice.Positional;

public class PositionalManager
{
	// 方位倍率表已內嵌於組件中(Avarice/StaticData/PositionalPotencies.cs)。
	//
	// 舊版是啟動時去 Google 試算表抓 CSV,再把結果快取到 Svc.PluginInterface.AssemblyLocation
	// 底下。那個目錄帶版號(installedPlugins/Avarice/<版號>/positionals.csv),所以每次外掛更新
	// 快取就跟著消失;一旦當下又連不上試算表,_actionStore 就是空的,IsPositional() 恆為 false,
	// 全部方位回饋都會靜默停用 —— 而且只留下一行 Warning,使用者端看起來就只是「功能不見了」。
	// 內嵌之後不再有任何網路或磁碟相依,建構完成即可用。
	private readonly Dictionary<int, PositionalAction> _actionStore = new();

	public PositionalManager()
	{
		Load();
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

		// 用 Information:使用者跑 LogLevel 2,這行是判斷「方位表到底有沒有載進來」的唯一依據。
		PluginLog.Information($"Loaded {_actionStore.Count} positional actions ({StaticData.PositionalPotencies.Records.Length} rows) from embedded table");
	}

	public bool IsPositionalHit(int actionId, int percent)
	{
		if (!_actionStore.TryGetValue(actionId, out PositionalAction action))
		{
			return false;
		}

		if (!action.Positionals.TryGetValue(percent, out PositionalParameters parameters))
		{
			return false;
		}

		return parameters.IsHit;
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
