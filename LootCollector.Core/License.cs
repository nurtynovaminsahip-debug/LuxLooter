using System;
using System.Threading.Tasks;

namespace LootCollector.Core;

public static class License
{
	public static readonly string Endpoint = "";

	public static readonly string AppId = "";

	public static LicenseResult Current { get; private set; } = new LicenseResult
	{
		Ok = true,
		ExpiresAt = DateTime.UtcNow.AddDays(36500.0),
		Plan = "free",
		Reason = "stub",
		Message = "",
		DaysLeft = 36500,
		Admin = false
	};


	public static string CurrentKey { get; private set; } = "";


	public static bool Configured => false;

	public static string DeviceId() => "";

	public static string SavedKey() => "";

	public static bool LoadCachedCurrent() => true;

	public static void Forget() { }

	public static Task<LicenseResult> ValidateAsync(string key) => Task.FromResult(Current);

	public static Task<LicenseResult> ValidateSavedAsync() => Task.FromResult(Current);

	public static string Mask(string key) => "";

	public static bool IsLifetime(LicenseResult r) => true;
}
