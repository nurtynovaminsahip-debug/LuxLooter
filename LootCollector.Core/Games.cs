namespace LootCollector.Core;

public static class Games
{
	public static readonly GameDef Mm2 = new GameDef
	{
		Key = "mm2",
		Label = "MM2",
		PlaceId = 142823291L,
		Autoexec = "autoexec_mm2.lua",
		Batch = new BatchConfig
		{
			Size = 1,
			LaunchDelaySec = 12.0,
			ActiveTimeoutSec = 600.0,
			JoinDetectSec = 35.0,
			AttachTimeoutSec = 20.0,
			MaxRetries = 2
		}
	};

	public static readonly GameDef Adoptme = new GameDef
	{
		Key = "adoptme",
		Label = "Adopt Me",
		PlaceId = 920587237L,
		Autoexec = "autoexec_adoptme.lua",
		Batch = new BatchConfig
		{
			Size = 1,
			LaunchDelaySec = 12.0,
			ActiveTimeoutSec = 1200.0,
			JoinDetectSec = 45.0,
			AttachTimeoutSec = 25.0,
			MaxRetries = 2
		}
	};

	public static readonly string[] WeaponRarities = new string[9] { "Common", "Uncommon", "Rare", "Legendary", "Godly", "Ancient", "Unique", "Vintage", "Chroma" };

	public static readonly string[] PetRarities = new string[6] { "Common", "Uncommon", "Rare", "Legendary", "Godly", "Chroma" };

	public static GameDef Get(string key)
	{
		if (!(key == "adoptme"))
		{
			return Mm2;
		}
		return Adoptme;
	}
}
