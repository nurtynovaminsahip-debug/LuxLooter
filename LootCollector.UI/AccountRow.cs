using System.Windows;

namespace LootCollector.UI;

public sealed class AccountRow
{
	public string Name { get; set; } = "";


	public bool DoneMm2 { get; set; }

	public bool DoneAdopt { get; set; }

	public Visibility DoneMm2Vis
	{
		get
		{
			if (!DoneMm2)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public Visibility DoneAdoptVis
	{
		get
		{
			if (!DoneAdopt)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}
}
