using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using LootCollector.Core;
using Wpf.Ui;
using Wpf.Ui.Controls;
namespace LootCollector.UI;

public class LicenseWindow : FluentWindow, IComponentConnector
{
    internal System.Windows.Controls.TextBox KeyBox;

    internal System.Windows.Controls.Button ActivateBtn;

    internal Wpf.Ui.Controls.ProgressRing Ring;

    internal System.Windows.Controls.TextBlock StatusTxt;

	private bool _contentLoaded;

	public LicenseWindow()
	{
		InitializeComponent();
		KeyBox.Text = LootCollector.Core.License.SavedKey();
		KeyBox.KeyDown += delegate(object _, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				_ = TryValidate(KeyBox.Text, auto: false);
			}
		};
		base.Loaded += OnLoaded;
	}

	private async void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (!LootCollector.Core.License.Configured)
		{
			Status("Сервер лицензий не настроен. Обратитесь к продавцу.", error: true);
			return;
		}
		string text = LootCollector.Core.License.SavedKey();
		if (!string.IsNullOrEmpty(text))
		{
			await TryValidate(text, auto: true);
		}
	}

	private async void OnActivate(object sender, RoutedEventArgs e)
	{
		await TryValidate(KeyBox.Text, auto: false);
	}

	private async Task TryValidate(string key, bool auto)
	{
		Busy(b: true);
		Status(auto ? "Проверка сохранённого ключа…" : "Проверяем ключ…", error: false);
		LicenseResult licenseResult = await LootCollector.Core.License.ValidateAsync(key);
		Busy(b: false);
		if (licenseResult.Ok)
		{
			base.DialogResult = true;
			Close();
		}
		else if (auto && licenseResult.Reason == "network")
		{
			Status("Нет связи с сервером. Проверьте интернет и нажмите «Активировать».", error: true);
		}
		else
		{
			Status(licenseResult.Message, error: true);
		}
	}

	private void Busy(bool b)
	{
		Ring.Visibility = ((!b) ? Visibility.Collapsed : Visibility.Visible);
		ActivateBtn.IsEnabled = !b;
		KeyBox.IsEnabled = !b;
	}

	private void Status(string text, bool error)
	{
		StatusTxt.Text = text;
		StatusTxt.Foreground = new SolidColorBrush(error ? Color.FromRgb(byte.MaxValue, 180, 171) : Color.FromRgb(157, 164, 140));
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.28.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/LuxLooter;component/ui/licensewindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.28.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			KeyBox = (Wpf.Ui.Controls.TextBox)target;
			break;
		case 2:
			ActivateBtn = (System.Windows.Controls.Button)target;
			ActivateBtn.Click += OnActivate;
			break;
		case 3:
			Ring = (ProgressRing)target;
			break;
		case 4:
			StatusTxt = (System.Windows.Controls.TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
