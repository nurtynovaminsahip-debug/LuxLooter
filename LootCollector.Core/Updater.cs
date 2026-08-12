using System;
using System.Threading.Tasks;

namespace LootCollector.Core;

public static class Updater
{
	public sealed class Info
	{
		public string version = "";

		public string url = "";

		public string notes = "";

		public string sha256 = "";

		public bool mandatory;
	}

	public static bool HasPending() => false;

	public static void CleanupOld() { }

	public static Task<Info> CheckAsync() => Task.FromResult<Info>(null);

	public static bool IsNewer(string remote) => false;

	public static Task<bool> DownloadAsync(Info info, Action<string> log = null) => Task.FromResult(false);

	public static void ApplyAndRestart() { }
}
