using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LootCollector.Core;

public static class Progress
{
	public sealed class Data
	{
		public List<string> done { get; set; } = new List<string>();


		public List<string> failed { get; set; } = new List<string>();


		public List<string> under13 { get; set; } = new List<string>();


		public List<string> broken { get; set; } = new List<string>();

	}

	private static string FileFor(string game)
	{
		return Path.Combine(Paths.StateDir, "progress_" + game + "_done.json");
	}

	public static Data Load(string game)
	{
		try
		{
			string path = FileFor(game);
			if (File.Exists(path))
			{
				return JsonSerializer.Deserialize<Data>(File.ReadAllText(path)) ?? new Data();
			}
		}
		catch
		{
		}
		return new Data();
	}

	public static bool IsDone(Data d, string nick)
	{
		return d.done.Contains<string>(nick, StringComparer.OrdinalIgnoreCase);
	}

	public static void ClearAll(string game)
	{
		try
		{
			string path = FileFor(game);
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	public static void ClearStatus(string game, string nick)
	{
		ClearStatuses(game, new string[1] { nick });
	}

	public static void ClearStatuses(string game, IEnumerable<string> nicks)
	{
		HashSet<string> set = new HashSet<string>(nicks ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		if (set.Count == 0)
		{
			return;
		}
		Data data = Load(game);
		data.done = data.done.Where((string x) => !set.Contains(x)).ToList();
		data.failed = data.failed.Where((string x) => !set.Contains(x)).ToList();
		data.broken = data.broken.Where((string x) => !set.Contains(x)).ToList();
		data.under13 = data.under13.Where((string x) => !set.Contains(x)).ToList();
		try
		{
			Directory.CreateDirectory(Paths.StateDir);
			File.WriteAllText(FileFor(game), JsonSerializer.Serialize(data, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
		catch
		{
		}
	}
}
