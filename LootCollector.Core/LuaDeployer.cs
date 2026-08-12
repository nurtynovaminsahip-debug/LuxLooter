using System;
using System.IO;
using System.Reflection;

namespace LootCollector.Core;

public static class LuaDeployer
{
	public static void Deploy(string autoexecResourceName, string executorSetting = null)
	{
		string text = Paths.Autoexec(executorSetting);
		Directory.CreateDirectory(text);
		string[] files = Directory.GetFiles(text, "*.lua");
		foreach (string path in files)
		{
			try
			{
				File.Delete(path);
			}
			catch
			{
			}
		}
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(autoexecResourceName) ?? throw new Exception("вшитый ресурс не найден: " + autoexecResourceName);
		using FileStream destination = File.Create(Path.Combine(text, autoexecResourceName));
		stream.CopyTo(destination);
	}
}
