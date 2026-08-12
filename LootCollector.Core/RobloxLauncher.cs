using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LootCollector.Core;

public static class RobloxLauncher
{
	public sealed class AccountInfo
	{
		public string Name;

		public long UserId;
	}

	private const string Referer = "https://www.roblox.com/games/4924922222/Brookhaven-RP";

	private const string SingletonMutex = "ROBLOX_singletonMutex";

	private static readonly HttpClient Http = new HttpClient(new HttpClientHandler
	{
		UseCookies = false,
		AllowAutoRedirect = false
	})
	{
		Timeout = TimeSpan.FromSeconds(20.0)
	};

	private static readonly Random Rng = new Random();

	private static bool _singletonStarted;

	private static volatile bool _ownEvent;

	private static volatile bool _ownMutex;

	private const string AuthUrl = "https://auth.roblox.com/v1/authentication-ticket/";

	public static bool SingletonOwned
	{
		get
		{
			if (!_ownEvent)
			{
				return _ownMutex;
			}
			return true;
		}
	}

	public static void HoldSingletonMutex()
	{
		if (_singletonStarted)
		{
			return;
		}
		_singletonStarted = true;
		Thread thread = new Thread((ThreadStart)delegate
		{
			Mutex mutex = null;
			Mutex mutex2 = null;
			while (true)
			{
				try
				{
					if (mutex == null)
					{
						mutex = new Mutex(initiallyOwned: false, "ROBLOX_singletonEvent");
					}
					if (!_ownEvent && mutex.WaitOne(0))
					{
						_ownEvent = true;
					}
				}
				catch (AbandonedMutexException)
				{
					_ownEvent = true;
				}
				catch
				{
					mutex = null;
				}
				try
				{
					if (mutex2 == null)
					{
						mutex2 = new Mutex(initiallyOwned: false, "ROBLOX_singletonMutex");
					}
					if (!_ownMutex && mutex2.WaitOne(0))
					{
						_ownMutex = true;
					}
				}
				catch (AbandonedMutexException)
				{
					_ownMutex = true;
				}
				catch
				{
					mutex2 = null;
				}
				Thread.Sleep(1000);
			}
		});
		thread.IsBackground = true;
		thread.Name = "RbxSingleton";
		thread.Start();
	}

	public static async Task<AccountInfo> GetAccountInfoAsync(string cookie)
	{
		cookie = (cookie ?? "").Trim();
		if (cookie.Length == 0)
		{
			throw new Exception("пустая кука");
		}
		int attempt = 1;
		while (true)
		{
			using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://www.roblox.com/my/account/json"))
			{
				req.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + cookie);
				using HttpResponseMessage resp = await Http.SendAsync(req);
				if (resp.StatusCode != HttpStatusCode.TooManyRequests && resp.StatusCode != HttpStatusCode.ServiceUnavailable)
				{
					if (!resp.IsSuccessStatusCode)
					{
						throw new Exception($"кука не принята Roblox (HTTP {resp.StatusCode}) — просрочена/битая");
					}
					using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
					JsonElement rootElement = jsonDocument.RootElement;
					return new AccountInfo
					{
						Name = rootElement.GetProperty("Name").GetString(),
						UserId = rootElement.GetProperty("UserId").GetInt64()
					};
				}
				if (attempt > 6)
				{
					throw new Exception("Roblox рейт-лимит (HTTP 429) — слишком много кук разом; добавляй пачками поменьше или повтори позже");
				}
				TimeSpan timeSpan = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Min(30, 4 * attempt));
				await Task.Delay((timeSpan < TimeSpan.FromSeconds(3.0)) ? TimeSpan.FromSeconds(3.0) : timeSpan);
			}
			attempt++;
		}
	}

	private static HttpRequestMessage AuthReq(string cookie, string csrf)
	{
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/")
		{
			Content = new StringContent("", Encoding.UTF8, "application/json")
		};
		httpRequestMessage.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + cookie);
		httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://www.roblox.com/games/4924922222/Brookhaven-RP");
		httpRequestMessage.Headers.TryAddWithoutValidation("Origin", "https://www.roblox.com");
		httpRequestMessage.Headers.TryAddWithoutValidation("RBXAuthenticationNegotiation", "1");
		if (!string.IsNullOrEmpty(csrf))
		{
			httpRequestMessage.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
		}
		return httpRequestMessage;
	}

	private static async Task<string> GetCsrfAsync(string cookie)
	{
		using HttpRequestMessage req = AuthReq(cookie, null);
		using HttpResponseMessage httpResponseMessage = await Http.SendAsync(req);
		if (httpResponseMessage.Headers.TryGetValues("x-csrf-token", out IEnumerable<string> values))
		{
			using IEnumerator<string> enumerator = values.GetEnumerator();
			if (enumerator.MoveNext())
			{
				return enumerator.Current;
			}
		}
		return null;
	}

	private static async Task<string> GetAuthTicketAsync(string cookie)
	{
		string text = await GetCsrfAsync(cookie);
		if (string.IsNullOrEmpty(text))
		{
			throw new Exception("сессия истекла (нет x-csrf-token) — обнови куку");
		}
		using HttpRequestMessage req = AuthReq(cookie, text);
		using HttpResponseMessage httpResponseMessage = await Http.SendAsync(req);
		if (httpResponseMessage.Headers.TryGetValues("rbx-authentication-ticket", out IEnumerable<string> values))
		{
			foreach (string item in values)
			{
				if (!string.IsNullOrEmpty(item))
				{
					return item;
				}
			}
		}
		throw new Exception("Roblox не выдал auth-ticket — кука, вероятно, разлогинена");
	}

	public static async Task LaunchAsync(string cookie, long placeId, string jobId = null, string vipLink = null, long followUserId = 0L)
	{
		HoldSingletonMutex();
		string value = await GetAuthTicketAsync(cookie);
		string text = $"{Rng.Next(100000, 175000)}{Rng.Next(100000, 900000)}";
		long value2 = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
		string stringToEscape = PlaceLauncherUrl(placeId, jobId, vipLink, followUserId, text);
		string fileName = $"roblox-player:1+launchmode:play+gameinfo:{value}+launchtime:{value2}+placelauncherurl:{Uri.EscapeDataString(stringToEscape)}+browsertrackerid:{text}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
		Process.Start(new ProcessStartInfo
		{
			FileName = fileName,
			UseShellExecute = true
		});
	}

	private static string PlaceLauncherUrl(long placeId, string jobId, string vipLink, long followUserId, string btid)
	{
		if (!string.IsNullOrEmpty(vipLink))
		{
			Match match = Regex.Match(vipLink, "privateServerLinkCode=([^&]+)");
			string value = (match.Success ? match.Groups[1].Value : vipLink);
			return "https://assetgame.roblox.com/game/PlaceLauncher.ashx?" + $"request=RequestPrivateGame&placeId={placeId}&linkCode={value}";
		}
		if (followUserId > 0)
		{
			return "https://assetgame.roblox.com/game/PlaceLauncher.ashx?" + $"request=RequestFollowUser&userId={followUserId}";
		}
		string value2 = (string.IsNullOrEmpty(jobId) ? "" : "Job");
		string value3 = (string.IsNullOrEmpty(jobId) ? "" : ("&gameId=" + jobId));
		return "https://assetgame.roblox.com/game/PlaceLauncher.ashx?" + $"request=RequestGame{value2}&browserTrackerId={btid}&placeId={placeId}{value3}&isPlayTogetherGame=false";
	}
}
