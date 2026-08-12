using System;

namespace LootCollector.Core;

public sealed class LicenseResult
{
	public bool Ok;

	public string Reason = "";

	public DateTime? ExpiresAt;

	public int DaysLeft;

	public string Plan = "";

	public string Message = "";

	public bool Admin;
}
