using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LootCollector.Core;

public static class WindowManager
{
	private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	private struct RECT
	{
		public int L;

		public int T;

		public int R;

		public int B;
	}

	private const int SW_RESTORE = 9;

	private const int SW_MAXIMIZE = 3;

	private const int GWL_STYLE = -16;

	private const int WS_OVERLAPPEDWINDOW = 13565952;

	private const int WS_VISIBLE = 268435456;

	private const uint SWP_FRAMECHANGED = 32u;

	private const uint SWP_NOZORDER = 4u;

	private const uint SWP_SHOWWINDOW = 64u;

	private static int _counter;

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc cb, nint l);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint h, out uint pid);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint h);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint h, out RECT r);

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(nint h, int idx);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(nint h, int idx, int v);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(nint h, nint after, int x, int y, int w, int hh, uint flags);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int n);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint h);

	[DllImport("user32.dll")]
	private static extern bool BringWindowToTop(nint h);

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint h, int cmd);

	[DllImport("user32.dll")]
	private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

	public static void Arrange(int? pid, string mode)
	{
		if (!pid.HasValue)
		{
			return;
		}
		int p = pid.GetValueOrDefault();
		Task.Run(delegate
		{
			try
			{
				nint num = WaitForWindow(p, 25);
				if (num != IntPtr.Zero)
				{
					int num2 = GetSystemMetrics(0);
					int num3 = GetSystemMetrics(1);
					if (num2 <= 0 || num3 <= 0)
					{
						num2 = 1920;
						num3 = 1080;
					}
					int windowLong = GetWindowLong(num, -16);
					SetWindowLong(num, -16, windowLong | 0xCF0000 | 0x10000000);
					if (mode == "full")
					{
						SetWindowPos(num, IntPtr.Zero, 0, 0, num2, num3, 100u);
						ShowWindow(num, 3);
					}
					else
					{
						int num4 = Interlocked.Increment(ref _counter) - 1;
						int num5;
						int num6;
						int num7;
						int num8;
						if (mode == "large")
						{
							num5 = num2 * 3 / 4;
							num6 = num3 * 3 / 4;
							num7 = num4 % 4 * 36;
							num8 = num4 % 4 * 36;
						}
						else
						{
							num5 = num2 / 2;
							num6 = num3 / 2;
							int num9 = num4 % 4;
							int num10 = num4 / 4 * 28;
							num7 = num9 % 2 * num5 + num10;
							num8 = num9 / 2 * num6 + num10;
						}
						if (num7 + num5 > num2)
						{
							num7 = num2 - num5;
						}
						if (num8 + num6 > num3)
						{
							num8 = num3 - num6;
						}
						if (num7 < 0)
						{
							num7 = 0;
						}
						if (num8 < 0)
						{
							num8 = 0;
						}
						SetWindowPos(num, IntPtr.Zero, num7, num8, num5, num6, 100u);
					}
				}
			}
			catch
			{
			}
		});
	}

	public static void ResetCounter()
	{
		Interlocked.Exchange(ref _counter, 0);
	}

	public static void ForegroundHwnd(nint hwnd)
	{
		if (hwnd == IntPtr.Zero)
		{
			return;
		}
		try
		{
			ShowWindow(hwnd, 9);
			nint foregroundWindow = GetForegroundWindow();
			if (foregroundWindow != hwnd)
			{
				uint pid;
				uint windowThreadProcessId = GetWindowThreadProcessId(foregroundWindow, out pid);
				uint windowThreadProcessId2 = GetWindowThreadProcessId(hwnd, out pid);
				if (windowThreadProcessId != 0 && windowThreadProcessId != windowThreadProcessId2)
				{
					AttachThreadInput(windowThreadProcessId, windowThreadProcessId2, attach: true);
				}
				BringWindowToTop(hwnd);
				SetForegroundWindow(hwnd);
				if (windowThreadProcessId != 0 && windowThreadProcessId != windowThreadProcessId2)
				{
					AttachThreadInput(windowThreadProcessId, windowThreadProcessId2, attach: false);
				}
			}
		}
		catch
		{
		}
	}

	public static nint WaitForWindow(int pid, int seconds)
	{
		DateTime dateTime = DateTime.UtcNow.AddSeconds(seconds);
		while (DateTime.UtcNow < dateTime)
		{
			nint found = IntPtr.Zero;
			EnumWindows(delegate(nint h, nint _)
			{
				if (!IsWindowVisible(h))
				{
					return true;
				}
				GetWindowThreadProcessId(h, out var pid2);
				if (pid2 != (uint)pid)
				{
					return true;
				}
				if (!GetWindowRect(h, out var r))
				{
					return true;
				}
				if (r.R - r.L < 200 || r.B - r.T < 150)
				{
					return true;
				}
				found = h;
				return false;
			}, IntPtr.Zero);
			if (found != IntPtr.Zero)
			{
				return found;
			}
			Thread.Sleep(700);
		}
		return IntPtr.Zero;
	}
}
