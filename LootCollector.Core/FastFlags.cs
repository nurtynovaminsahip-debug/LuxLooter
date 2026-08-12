using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LootCollector.Core;

public static class FastFlags
{
	private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

	public static void ApplyLowLoad(int fps, Action<string> log = null)
	{
		try
		{
			string path = Path.Combine(Local, "Roblox", "Versions");
			if (!Directory.Exists(path))
			{
				return;
			}
			Dictionary<string, object> dictionary = new Dictionary<string, object>
			{
				["DFIntTaskSchedulerTargetFps"] = fps,
				["DFIntDebugFRMQualityLevelOverride"] = 1
			};
			int num = 0;
			string[] directories = Directory.GetDirectories(path);
			foreach (string path2 in directories)
			{
				if (!File.Exists(Path.Combine(path2, "RobloxPlayerBeta.exe")))
				{
					continue;
				}
				string text = Path.Combine(path2, "ClientSettings");
				Directory.CreateDirectory(text);
				string path3 = Path.Combine(text, "ClientAppSettings.json");
				Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
				try
				{
					if (File.Exists(path3))
					{
						dictionary2 = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path3)) ?? new Dictionary<string, object>();
					}
				}
				catch
				{
					dictionary2 = new Dictionary<string, object>();
				}
				foreach (KeyValuePair<string, object> item in dictionary)
				{
					dictionary2[item.Key] = item.Value;
				}
				File.WriteAllText(path3, JsonSerializer.Serialize(dictionary2, new JsonSerializerOptions
				{
					WriteIndented = true
				}));
				num++;
			}
			if (num > 0)
			{
				log?.Invoke($"[лёгкий режим: FPS {fps} + низкая графика]");
			}
		}
		catch
		{
		}
	}

	public static void ApplyGameSettings(int fps)
	{
		try
		{
			bool flag = fps > 0;
			string path = Path.Combine(Local, "Roblox");
			if (!Directory.Exists(path))
			{
				return;
			}
			string[] files = Directory.GetFiles(path, "GlobalBasicSettings_*.xml");
			foreach (string text in files)
			{
				if (text.IndexOf("Studio", StringComparison.OrdinalIgnoreCase) < 0)
				{
					string xml;
					try
					{
						xml = File.ReadAllText(text);
					}
					catch
					{
						continue;
					}
					xml = SetVal(xml, "int", "FramerateCap", flag ? fps.ToString() : "240");
					xml = SetVal(xml, "token", "SavedQualityLevel", flag ? "1" : "0");
					xml = SetVal(xml, "int", "GraphicsQualityLevel", flag ? "1" : "0");
					try
					{
						File.WriteAllText(text, xml);
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
	}

	private static string SetVal(string xml, string tag, string key, string val)
	{
		return Regex.Replace(xml, $"(<{tag} name=\"{key}\">)[^<]*(</{tag}>)", "${1}" + val + "$2");
	}

	public static void Remove()
	{
		try
		{
			string path = Path.Combine(Local, "Roblox", "Versions");
			if (!Directory.Exists(path))
			{
				return;
			}
			string[] directories = Directory.GetDirectories(path);
			for (int i = 0; i < directories.Length; i++)
			{
				string path2 = Path.Combine(directories[i], "ClientSettings", "ClientAppSettings.json");
				if (File.Exists(path2))
				{
					Dictionary<string, object> dictionary;
					try
					{
						dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path2)) ?? new Dictionary<string, object>();
					}
					catch
					{
						continue;
					}
					dictionary.Remove("DFIntTaskSchedulerTargetFps");
					dictionary.Remove("DFIntDebugFRMQualityLevelOverride");
					File.WriteAllText(path2, JsonSerializer.Serialize(dictionary, new JsonSerializerOptions
					{
						WriteIndented = true
					}));
				}
			}
		}
		catch
		{
		}
	}
}
