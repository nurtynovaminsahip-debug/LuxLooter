namespace LootCollector.Core;

public sealed class GameDef
{
	public string Key;

	public string Label;

	public long PlaceId;

	public bool PublicOnly = true;

	public string Autoexec;

	public BatchConfig Batch = new BatchConfig();
}
