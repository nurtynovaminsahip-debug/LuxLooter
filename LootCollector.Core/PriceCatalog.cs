using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LootCollector.Core;

public sealed class PriceCatalog
{
	private sealed class Cache
	{
		public DateTime UpdatedUtc { get; set; }

		public Dictionary<string, Dictionary<string, double>> Prices { get; set; } = new Dictionary<string, Dictionary<string, double>>();

	}

	private Cache _c = new Cache();

	private static string FilePath => Path.Combine(Paths.DataDir, "prices.json");

	public DateTime UpdatedUtc => _c.UpdatedUtc;

	public bool IsStale(double hours)
	{
		return (DateTime.UtcNow - _c.UpdatedUtc).TotalHours >= hours;
	}

	public bool HasGame(string game)
	{
		if (_c.Prices.TryGetValue(game, out var value))
		{
			return value.Count > 0;
		}
		return false;
	}

	public static PriceCatalog Load()
	{
		PriceCatalog priceCatalog = new PriceCatalog();
		try
		{
			if (File.Exists(FilePath))
			{
				priceCatalog._c = JsonSerializer.Deserialize<Cache>(File.ReadAllText(FilePath)) ?? new Cache();
			}
		}
		catch
		{
		}
		return priceCatalog;
	}

	private void Save()
	{
		try
		{
			Directory.CreateDirectory(Paths.DataDir);
			File.WriteAllText(FilePath, JsonSerializer.Serialize(_c));
		}
		catch
		{
		}
	}

	public double Price(string game, string itemName)
	{
		if (!_c.Prices.TryGetValue(game, out var value))
		{
			return 0.0;
		}
		string text = Norm(itemName);
		if (value.TryGetValue(text, out var value2))
		{
			return value2;
		}
		if (text.Contains("chroma") && value.TryGetValue(text + "#c", out var value3))
		{
			return value3;
		}
		int num = text.IndexOf('|');
		if (num > 0)
		{
			string text2 = text.Substring(0, num);
			string text3 = text.Substring(num);
			if (value.TryGetValue(text2 + "|default|0|0", out var value4))
			{
				return value4;
			}
			Match match = Regex.Match(text2, "^.+?_\\d{4}_(.+)$");
			if (match.Success)
			{
				string value5 = match.Groups[1].Value;
				if (value.TryGetValue(value5 + text3, out var value6))
				{
					return value6;
				}
				if (value.TryGetValue(value5 + "|default|0|0", out var value7))
				{
					return value7;
				}
			}
		}
		return 0.0;
	}

	private static string Norm(string s)
	{
		return (s ?? "").Trim().ToLowerInvariant();
	}

	public async Task RefreshAsync(string game, Action<string> log = null)
	{
		_ = 1;
		try
		{
			string text = (License.Endpoint ?? "").TrimEnd('/');
			if (text.Length == 0)
			{
				log?.Invoke("[цены " + game + ": не задан адрес сервера]");
				return;
			}
			using HttpClient http = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(20.0)
			};
			using HttpResponseMessage resp = await http.GetAsync(text + "/prices/" + game);
			if (!resp.IsSuccessStatusCode)
			{
				log?.Invoke($"[цены {game}: сервер вернул HTTP {resp.StatusCode}]");
				return;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
			if (!jsonDocument.RootElement.TryGetProperty("prices", out var value) || value.ValueKind != JsonValueKind.Object)
			{
				log?.Invoke("[цены " + game + ": пустой ответ сервера]");
				return;
			}
			Dictionary<string, double> dictionary = new Dictionary<string, double>();
			foreach (JsonProperty item in value.EnumerateObject())
			{
				if (item.Value.ValueKind == JsonValueKind.Number)
				{
					dictionary[item.Name] = item.Value.GetDouble();
				}
			}
			if (dictionary.Count > 0)
			{
				_c.Prices[game] = dictionary;
				_c.UpdatedUtc = DateTime.UtcNow;
				Save();
				log?.Invoke($"[цены {game}: {dictionary.Count} позиций с сервера]");
			}
		}
		catch (Exception ex)
		{
			log?.Invoke($"[цены {game}: ошибка обновления — {ex.Message}]");
		}
	}
}
