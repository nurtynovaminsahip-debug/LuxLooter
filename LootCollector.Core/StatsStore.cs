using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LootCollector.Core;

public sealed class StatsStore
{
	public sealed class Day
	{
		public int Items { get; set; }

		public double Usd { get; set; }
	}

	public sealed class GameStats
	{
		public Dictionary<string, Day> Days { get; set; } = new Dictionary<string, Day>();


		public Dictionary<string, double> TopItems { get; set; } = new Dictionary<string, double>();


		public Dictionary<string, Dictionary<string, double>> DayItems { get; set; } = new Dictionary<string, Dictionary<string, double>>();


		public Dictionary<string, int> Breakdown { get; set; } = new Dictionary<string, int>();


		public Dictionary<string, Day> Accounts { get; set; } = new Dictionary<string, Day>();

	}

	private Dictionary<string, GameStats> _games = new Dictionary<string, GameStats>();

	private static string FilePath => Path.Combine(Paths.DataDir, "stats.json");

	public static StatsStore Load()
	{
		StatsStore statsStore = new StatsStore();
		try
		{
			if (File.Exists(FilePath))
			{
				statsStore._games = JsonSerializer.Deserialize<Dictionary<string, GameStats>>(File.ReadAllText(FilePath)) ?? new Dictionary<string, GameStats>();
			}
		}
		catch
		{
		}
		statsStore.BackfillDayItems();
		return statsStore;
	}

	private void Save()
	{
		try
		{
			Directory.CreateDirectory(Paths.DataDir);
			File.WriteAllText(FilePath, JsonSerializer.Serialize(_games));
		}
		catch
		{
		}
	}

	private void BackfillDayItems()
	{
		bool flag = false;
		string text = DateTime.Now.ToString("yyyy-MM-dd");
		foreach (GameStats value in _games.Values)
		{
			if (value.DayItems.Count > 0 || value.TopItems.Count == 0 || value.Days.Count == 0)
			{
				continue;
			}
			foreach (string key in value.Days.Keys)
			{
				if (key != text)
				{
					value.DayItems[key] = new Dictionary<string, double>(value.TopItems);
				}
			}
			if (value.DayItems.Count > 0)
			{
				flag = true;
			}
		}
		if (flag)
		{
			Save();
		}
	}

	private GameStats Get(string game)
	{
		if (!_games.TryGetValue(game, out var value))
		{
			value = new GameStats();
			_games[game] = value;
		}
		return value;
	}

	public void AddRun(string game, int items, double usd, IEnumerable<(string name, double price)> top, IDictionary<string, int> breakdown, string account = null)
	{
		GameStats gameStats = Get(game);
		if (items > 0 || usd > 0.0)
		{
			string key = DateTime.Now.ToString("yyyy-MM-dd");
			if (!gameStats.Days.TryGetValue(key, out var value))
			{
				value = new Day();
				gameStats.Days[key] = value;
			}
			value.Items += items;
			value.Usd += usd;
		}
		if (!string.IsNullOrEmpty(account) && (items > 0 || usd > 0.0))
		{
			if (!gameStats.Accounts.TryGetValue(account, out var value2))
			{
				value2 = new Day();
				gameStats.Accounts[account] = value2;
			}
			value2.Items += items;
			value2.Usd += usd;
		}
		if (top != null)
		{
			string key2 = DateTime.Now.ToString("yyyy-MM-dd");
			if (!gameStats.DayItems.TryGetValue(key2, out var value3))
			{
				value3 = new Dictionary<string, double>();
				gameStats.DayItems[key2] = value3;
			}
			foreach (var (key3, num) in top)
			{
				if (!gameStats.TopItems.TryGetValue(key3, out var value4) || num > value4)
				{
					gameStats.TopItems[key3] = num;
				}
				if (!value3.TryGetValue(key3, out var value5) || num > value5)
				{
					value3[key3] = num;
				}
			}
			if (gameStats.DayItems.Count > 80)
			{
				foreach (string item in gameStats.DayItems.Keys.OrderBy((string k) => k).Take(gameStats.DayItems.Count - 70).ToList())
				{
					gameStats.DayItems.Remove(item);
				}
			}
		}
		if (breakdown != null)
		{
			foreach (KeyValuePair<string, int> item2 in breakdown)
			{
				gameStats.Breakdown.TryGetValue(item2.Key, out var value6);
				gameStats.Breakdown[item2.Key] = value6 + item2.Value;
			}
		}
		try
		{
			Directory.CreateDirectory(Paths.DataDir);
			File.WriteAllText(FilePath, JsonSerializer.Serialize(_games));
		}
		catch
		{
		}
	}

	public List<(string cat, int count)> Breakdown(string game)
	{
		return (from kv in Get(game).Breakdown
			orderby kv.Value descending
			select (Key: kv.Key, Value: kv.Value)).ToList();
	}

	public int TotalItems(string game)
	{
		return Get(game).Days.Values.Sum((Day d) => d.Items);
	}

	public double TotalUsd(string game)
	{
		return Get(game).Days.Values.Sum((Day d) => d.Usd);
	}

	public List<(DateTime date, int items, double usd)> Daily(string game, int lastDays)
	{
		DateTime result;
		return (from kv in Get(game).Days
			select (date: DateTime.TryParse(kv.Key, out result) ? result : DateTime.MinValue, items: kv.Value.Items, usd: kv.Value.Usd) into x
			where x.date != DateTime.MinValue
			orderby x.date
			select x).TakeLast(lastDays).ToList();
	}

	public List<(string name, double price)> TopItems(string game, int n, int lastDays = 0)
	{
		GameStats gameStats = Get(game);
		if (lastDays <= 0)
		{
			return (from kv in gameStats.TopItems.OrderByDescending((KeyValuePair<string, double> kv) => kv.Value).Take(n)
				select (Key: kv.Key, Value: kv.Value)).ToList();
		}
		DateTime dateTime = DateTime.Now.Date.AddDays(-(lastDays - 1));
		Dictionary<string, double> dictionary = new Dictionary<string, double>();
		foreach (KeyValuePair<string, Dictionary<string, double>> dayItem in gameStats.DayItems)
		{
			if (!DateTime.TryParse(dayItem.Key, out var result) || !(result.Date >= dateTime))
			{
				continue;
			}
			foreach (KeyValuePair<string, double> item in dayItem.Value)
			{
				if (!dictionary.TryGetValue(item.Key, out var value) || item.Value > value)
				{
					dictionary[item.Key] = item.Value;
				}
			}
		}
		return (from x in dictionary.OrderByDescending((KeyValuePair<string, double> x) => x.Value).Take(n)
			select (Key: x.Key, Value: x.Value)).ToList();
	}

	public List<(string name, int items, double usd)> TopAccounts(string game, int n)
	{
		return (from kv in Get(game).Accounts.OrderByDescending((KeyValuePair<string, Day> kv) => kv.Value.Usd).Take(n)
			select (Key: kv.Key, Items: kv.Value.Items, Usd: kv.Value.Usd)).ToList();
	}
}
