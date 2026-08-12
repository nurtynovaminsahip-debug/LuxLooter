using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace LootCollector.Core;

public static class ProcessUtil
{
	public static HashSet<int> RobloxPids()
	{
		HashSet<int> hashSet = new HashSet<int>();
		Process[] processesByName = Process.GetProcessesByName("RobloxPlayerBeta");
		foreach (Process process in processesByName)
		{
			try
			{
				hashSet.Add(process.Id);
			}
			catch
			{
			}
			finally
			{
				process.Dispose();
			}
		}
		return hashSet;
	}

	public static int? WaitNewPid(HashSet<int> before, double timeoutSec, CancellationToken ct = default(CancellationToken))
	{
		DateTime dateTime = DateTime.UtcNow.AddSeconds(timeoutSec);
		while (DateTime.UtcNow < dateTime && !ct.IsCancellationRequested)
		{
			HashSet<int> hashSet = RobloxPids();
			hashSet.ExceptWith(before);
			if (hashSet.Count > 0)
			{
				return hashSet.Min();
			}
			Thread.Sleep(1000);
		}
		return null;
	}

	public static void KillAllRoblox()
	{
		Process[] processes = Process.GetProcesses();
		foreach (Process process in processes)
		{
			try
			{
				if (process.ProcessName.IndexOf("roblox", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					process.Kill();
					process.WaitForExit(3000);
				}
			}
			catch
			{
			}
			finally
			{
				try
				{
					process.Dispose();
				}
				catch
				{
				}
			}
		}
	}

	public static void KillAllRobloxExcept(int? keepPid)
	{
		Process[] processes = Process.GetProcesses();
		foreach (Process process in processes)
		{
			try
			{
				if (!keepPid.HasValue)
				{
					goto IL_002a;
				}
				int valueOrDefault = keepPid.GetValueOrDefault();
				if (process.Id != valueOrDefault)
				{
					goto IL_002a;
				}
				goto end_IL_000e;
				IL_002a:
				if (process.ProcessName.IndexOf("roblox", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					process.Kill();
					process.WaitForExit(3000);
				}
				end_IL_000e:;
			}
			catch
			{
			}
			finally
			{
				try
				{
					process.Dispose();
				}
				catch
				{
				}
			}
		}
	}

	public static bool IsRunning(int? pid)
	{
		if (!pid.HasValue)
		{
			return false;
		}
		try
		{
			Process processById = Process.GetProcessById(pid.Value);
			bool result = !processById.HasExited;
			processById.Dispose();
			return result;
		}
		catch
		{
			return false;
		}
	}

	public static void KillPid(int? pid)
	{
		if (!pid.HasValue)
		{
			return;
		}
		try
		{
			Process processById = Process.GetProcessById(pid.Value);
			processById.Kill();
			processById.WaitForExit(5000);
		}
		catch
		{
		}
	}
}
