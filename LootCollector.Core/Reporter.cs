using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LootCollector.Core;

public static class Reporter
{
	public sealed class ClientRow
	{
		public string Key = "";

		public int Items;

		public double Usd;

		public string Version = "";

		public string Status = "";

		public long LastSeen;
	}

	public sealed class AdminStats
	{
		public int Clients;

		public int Online;

		public int Running;

		public int Items;

		public double Usd;

		public (int items, double usd) Mm2;

		public (int items, double usd) Adopt;

		public List<ClientRow> List = new List<ClientRow>();
	}

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(12.0)
	};

	public static async Task ReportAsync(string status = "idle")
	{
		if (!License.Configured || string.IsNullOrEmpty(License.CurrentKey))
		{
			return;
		}
		try
		{
			StatsStore statsStore = StatsStore.Load();
			string content = JsonSerializer.Serialize(new
			{
				key = License.CurrentKey,
				version = "2.8.5",
				device = License.DeviceId(),
				status = status,
				games = new
				{
					mm2 = new
					{
						items = statsStore.TotalItems("mm2"),
						usd = statsStore.TotalUsd("mm2")
					},
					adoptme = new
					{
						items = statsStore.TotalItems("adoptme"),
						usd = statsStore.TotalUsd("adoptme")
					}
				}
			});
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, License.Endpoint.TrimEnd('/') + "/report");
			req.Headers.TryAddWithoutValidation("X-App-Id", License.AppId);
			req.Content = new StringContent(content, Encoding.UTF8, "application/json");
			await Http.SendAsync(req);
		}
		catch
		{
		}
	}

	private static int I(JsonElement e, string n)
	{
		if (!e.TryGetProperty(n, out var value) || value.ValueKind != JsonValueKind.Number)
		{
			return 0;
		}
		return (int)value.GetDouble();
	}

	private static double D(JsonElement e, string n)
	{
		if (!e.TryGetProperty(n, out var value) || value.ValueKind != JsonValueKind.Number)
		{
			return 0.0;
		}
		return value.GetDouble();
	}

	public static async Task<AdminStats> FetchAdminStatsAsync()
	{
		if (!License.Configured || string.IsNullOrEmpty(License.CurrentKey))
		{
			return null;
		}
		try
		{
			string content = JsonSerializer.Serialize(new
			{
				key = License.CurrentKey
			});
			using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, License.Endpoint.TrimEnd('/') + "/admin/stats");
			req.Headers.TryAddWithoutValidation("X-App-Id", License.AppId);
			req.Content = new StringContent(content, Encoding.UTF8, "application/json");
			using HttpResponseMessage resp = await Http.SendAsync(req);
			using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
			JsonElement rootElement = jsonDocument.RootElement;
			if (!rootElement.TryGetProperty("ok", out var value) || value.ValueKind != JsonValueKind.True)
			{
				return null;
			}
			AdminStats adminStats = new AdminStats
			{
				Clients = I(rootElement, "clients"),
				Online = I(rootElement, "online"),
				Running = I(rootElement, "running")
			};
			if (rootElement.TryGetProperty("total", out var value2))
			{
				adminStats.Items = I(value2, "items");
				adminStats.Usd = D(value2, "usd");
			}
			if (rootElement.TryGetProperty("games", out var value3))
			{
				if (value3.TryGetProperty("mm2", out var value4))
				{
					adminStats.Mm2 = (items: I(value4, "items"), usd: D(value4, "usd"));
				}
				if (value3.TryGetProperty("adoptme", out var value5))
				{
					adminStats.Adopt = (items: I(value5, "items"), usd: D(value5, "usd"));
				}
			}
			if (rootElement.TryGetProperty("list", out var value6) && value6.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item in value6.EnumerateArray())
				{
					adminStats.List.Add(new ClientRow
					{
						Key = (item.TryGetProperty("key", out var value7) ? (value7.GetString() ?? "") : ""),
						Items = I(item, "items"),
						Usd = D(item, "usd"),
						Version = (item.TryGetProperty("version", out var value8) ? (value8.GetString() ?? "") : ""),
						Status = (item.TryGetProperty("status", out var value9) ? (value9.GetString() ?? "") : ""),
						LastSeen = (long)D(item, "lastSeen")
					});
				}
			}
			return adminStats;
		}
		catch
		{
			return null;
		}
	}
}
