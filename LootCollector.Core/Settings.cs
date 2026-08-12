using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LootCollector.Core;

public sealed class Settings
{
	public string Webhook { get; set; } = "";


	public string AutoexecDir { get; set; } = "";


	public Dictionary<string, string> Main { get; set; } = new Dictionary<string, string>();


	public List<string> WeaponRarities { get; set; }

	public List<string> PetRarities { get; set; }

	public List<string> AdoptCategories { get; set; }

	public List<DistTarget> AdoptmeDistribute { get; set; } = new List<DistTarget>();


	public List<string> AdoptmeExclude { get; set; } = new List<string>();


	public int Threads { get; set; } = 1;


	public Dictionary<string, List<string>> ExtraMains { get; set; } = new Dictionary<string, List<string>>();


	public bool LightMode { get; set; } = true;


	public int LightFps { get; set; } = -1;


	public string WinMode { get; set; } = "";


	public List<string> ExtraMainsFor(string game)
	{
		if (!ExtraMains.TryGetValue(game, out var value))
		{
			return new List<string>();
		}
		return value;
	}

	public static Settings Load()
	{
		Settings settings = null;
		try
		{
			if (File.Exists(Paths.SettingsFile))
			{
				settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Paths.SettingsFile));
			}
		}
		catch
		{
		}
		if (settings == null)
		{
			settings = new Settings();
		}
		if (settings.LightFps < 0)
		{
			settings.LightFps = (settings.LightMode ? 30 : 0);
		}
		if (string.IsNullOrEmpty(settings.WinMode))
		{
			settings.WinMode = (settings.LightMode ? "small" : "full");
		}
		return settings;
	}

	public void Save()
	{
		try
		{
			Directory.CreateDirectory(Paths.DataDir);
			File.WriteAllText(Paths.SettingsFile, JsonSerializer.Serialize(this, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
		catch
		{
		}
	}

	public string MainFor(string game)
	{
		if (!Main.TryGetValue(game, out var value))
		{
			return "";
		}
		return value;
	}
}
