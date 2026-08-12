using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace LootCollector.Core;

public sealed class Notifier
{
	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(15.0)
	};

	private readonly string _url;

	public Notifier(string url)
	{
		_url = url ?? "";
	}

	public void Send(string message)
	{
		if (string.IsNullOrEmpty(_url))
		{
			return;
		}
		try
		{
			string content = JsonSerializer.Serialize(new
			{
				content = message
			});
			Http.PostAsync(_url, new StringContent(content, Encoding.UTF8, "application/json"));
		}
		catch
		{
		}
	}
}
