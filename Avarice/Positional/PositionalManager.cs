using CsvHelper;
using System.Globalization;
using System.IO;
using System.Net.Http;

namespace Avarice.Positional;

public class PositionalManager
{
	private const string SheetUrl = "https://docs.google.com/spreadsheets/d/1z2skn_jokyj02Qv2GPEs6HSmAZVLiw2LbwQxkXPjiEs/gviz/tq?tqx=out:csv&sheet=main1";
	private readonly string _filePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "positionals.csv");

	private readonly HttpClient _client;

	// 只在 framework thread 上整份換掉;讀取端(Memory.cs 的 action-effect hook)也在主執行緒,
	// 因此不需要鎖,但絕對不可以從背景執行緒直接改動這個字典的內容。
	private Dictionary<int, PositionalAction> _actionStore;

	public PositionalManager()
	{
		// 不設 Timeout 會吃 .NET 預設的 100 秒;整段抓取原本又是同步等在主執行緒上,
		// 一旦 Google 試算表連不上,遊戲就整個卡住到逾時為止。
		_client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
		_actionStore = [];
		Refresh();
	}

	public void Reset()
	{
		Refresh();
	}

	/// <summary>
	/// 在背景抓取並解析連擊資料表,完成後才回到 framework thread 發布結果。
	/// 呼叫端不會被阻塞。
	/// </summary>
	private void Refresh()
	{
		_ = Task.Run(async () =>
		{
			try
			{
				Dictionary<int, PositionalAction> store = await GetAsync().ConfigureAwait(false);
				if (store == null)
				{
					return;
				}

				await Svc.Framework.RunOnFrameworkThread(() => _actionStore = store).ConfigureAwait(false);
				PluginLog.Debug($"Loaded {store.Count} positional actions");
			}
			catch (Exception e)
			{
				e.Log();
			}
		});
	}

	private async Task<Dictionary<int, PositionalAction>> GetAsync()
	{
		string text = null;
		try
		{
			text = await _client.GetStringAsync(SheetUrl).ConfigureAwait(false);
		}
		catch (Exception e)
		{
			PluginLog.Warning($"Failed to download positional data, falling back to local cache: {e.Message}");
		}

		if (text != null)
		{
			try
			{
				if (!File.Exists(_filePath) || File.ReadAllText(_filePath) != text)
				{
					File.WriteAllText(_filePath, text);
				}
			}
			catch (Exception e)
			{
				e.Log();
			}
		}
		else
		{
			// 抓不到就吃本機快取;連快取都沒有就只能放棄,保留目前(空的)資料。
			if (!File.Exists(_filePath))
			{
				PluginLog.Warning("No cached positional data available");
				return null;
			}

			text = File.ReadAllText(_filePath);
		}

		return Load(text);
	}

	private static Dictionary<int, PositionalAction> Load(string text)
	{
		Dictionary<int, PositionalAction> actionStore = [];
		using StringReader reader = new(text);
		using CsvReader csv = new(reader, CultureInfo.InvariantCulture);

		foreach (PositionalRecord record in csv.GetRecords<PositionalRecord>())
		{
			if (!actionStore.TryGetValue(record.Id, out PositionalAction action))
			{
				action = new PositionalAction
				{
					Id = record.Id,
					ActionName = record.ActionName,
					ActionPosition = record.ActionPosition,
					Positionals = [],
				};
				actionStore.Add(record.Id, action);
			}

			PositionalParameters parameters = new()
			{
				Percent = record.Percent,
				IsHit = record.IsHit == "TRUE",
				Comment = record.Comment,
			};
			action.Positionals.Add(record.Percent, parameters);
		}

		return actionStore;
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
