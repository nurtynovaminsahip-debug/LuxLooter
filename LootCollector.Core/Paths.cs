using System;
using System.IO;

namespace LootCollector.Core;

public static class Paths
{
	public static readonly string DataDir;

	private static readonly string[] AutoexecNames;

	private static readonly string[] WorkspaceNames;

	public static readonly string[] NoRarityExecutors;

	public static readonly string[] NoMultiThreadExecutors;

	private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

	public static string AccountsFile => Path.Combine(DataDir, "accounts.json");

	public static string SettingsFile => Path.Combine(DataDir, "settings.json");

	public static string LicenseFile => Path.Combine(DataDir, "license.json");

	public static string StateDir => Path.Combine(DataDir, "state");

	public static string PotassiumWorkspace => Path.Combine(Local, "Potassium", "workspace");

	private static string FindSub(string root, string[] names, string fallback)
	{
		try
		{
			if (Directory.Exists(root))
			{
				string[] directories = Directory.GetDirectories(root);
				foreach (string text in directories)
				{
					string fileName = Path.GetFileName(text);
					foreach (string b in names)
					{
						if (string.Equals(fileName, b, StringComparison.OrdinalIgnoreCase))
						{
							return text;
						}
					}
				}
			}
		}
		catch
		{
		}
		return Path.Combine(root, fallback);
	}

	private static bool HasAutoexec(string root)
	{
		return Directory.Exists(FindSub(root, AutoexecNames, "\0"));
	}

	public static string ExecutorRoot(string setting)
	{
		if (string.IsNullOrWhiteSpace(setting))
		{
			return DetectExecutorRoot();
		}
		setting = setting.TrimEnd('\\', '/');
		string fileName = Path.GetFileName(setting);
		string[] autoexecNames = AutoexecNames;
		foreach (string b in autoexecNames)
		{
			if (string.Equals(fileName, b, StringComparison.OrdinalIgnoreCase))
			{
				return Path.GetDirectoryName(setting) ?? setting;
			}
		}
		return setting;
	}

	public static string DetectExecutorRoot()
	{
		string[] array = new string[6] { "Potassium", "Xeno", "Wave", "Volt", "Solara", "Swift" };
		foreach (string path in array)
		{
			string text = Path.Combine(Local, path);
			if (HasAutoexec(text))
			{
				return text;
			}
		}
		return Path.Combine(Local, "Potassium");
	}

	public static string Autoexec(string setting)
	{
		return FindSub(ExecutorRoot(setting), AutoexecNames, "autoexec");
	}

	public static string Workspace(string setting)
	{
		return FindSub(ExecutorRoot(setting), WorkspaceNames, "workspace");
	}

	public static string NoRarityExecutor(string executorSetting)
	{
		string text = ExecutorRoot(executorSetting) ?? "";
		string[] noRarityExecutors = NoRarityExecutors;
		foreach (string text2 in noRarityExecutors)
		{
			if (text.IndexOf(text2, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return text2;
			}
		}
		return null;
	}

	public static string NoMultiThreadExecutor(string executorSetting)
	{
		string text = ExecutorRoot(executorSetting) ?? "";
		string[] noMultiThreadExecutors = NoMultiThreadExecutors;
		foreach (string text2 in noMultiThreadExecutors)
		{
			if (text.IndexOf(text2, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return text2;
			}
		}
		return null;
	}

	public static string DoneDir(string game, string workspace)
	{
		return Path.Combine(workspace, game + "_done");
	}

	public static string DoneDir(string game)
	{
		return Path.Combine(PotassiumWorkspace, game + "_done");
	}

	static Paths()
	{
		DataDir = Path.Combine(Local, "RobloxLootCollector");
		AutoexecNames = new string[2] { "autoexec", "autoexecute" };
		WorkspaceNames = new string[1] { "workspace" };
		NoRarityExecutors = new string[1] { "Xeno" };
		NoMultiThreadExecutors = new string[1] { "Xeno" };
		try
		{
			Directory.CreateDirectory(DataDir);
			Directory.CreateDirectory(StateDir);
		}
		catch
		{
		}
	}
}
