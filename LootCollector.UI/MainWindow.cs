using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using LootCollector.Core;
using Microsoft.Win32;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace LootCollector.UI;

public class MainWindow : FluentWindow, IComponentConnector, IStyleConnector
{
	private sealed class Brand
	{
		public Brush Card = New("#353436");

		public Brush Stroke = New("#1FFFFFFF");

		public Brush Txt = New("#E5E2E3");

		public Brush Dim = New("#9DA48C");

		public Brush Lime = New("#ABD600");

		public Brush LimeFaint = New("#22ABD600");

		private static SolidColorBrush New(string h)
		{
			return new SolidColorBrush((Color)ColorConverter.ConvertFromString(h));
		}
	}

	private static readonly Brand Co = new Brand();

	private static readonly Brush LogGreen = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ABD600"));

	private static readonly Brush LogRed = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8A80"));

	private static readonly Brush LogYellow = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8C84A"));

	private static readonly Brush LogDim = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9DB58A"));

	private static readonly FontFamily UiFont = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#Inter");

	private static readonly FontFamily MonoFont = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#JetBrains Mono");

	private readonly Settings _settings = Settings.Load();

	private readonly AccountStore _store = new AccountStore();

	private readonly Dictionary<string, ToggleButton> _weap = new Dictionary<string, ToggleButton>();

	private readonly Dictionary<string, ToggleButton> _pet = new Dictionary<string, ToggleButton>();

	private readonly Dictionary<string, ToggleButton> _adoptCat = new Dictionary<string, ToggleButton>();

	private FrameworkElement _weapDrop;

	private FrameworkElement _petDrop;

	private FrameworkElement _catDrop;

	private static readonly (string label, string key)[] AdoptCats = new(string, string)[5]
	{
		("Питомцы", "pets"),
		("Одежда", "pet_accessories"),
		("Коляски", "strollers"),
		("Машины", "transport"),
		("Игрушки", "toys")
	};

	private static readonly string[] AdoptCatTokens = new string[5] { "Питомцы", "Одежда", "Коляски", "Машины", "Игрушки" };

	private readonly Dictionary<string, ToggleButton> _nav = new Dictionary<string, ToggleButton>();

	private readonly List<ComboBox> _extraMainCombos = new List<ComboBox>();

	private string _game = "mm2";

	private bool _loading;

	private CancellationTokenSource _cts;

	private Task _task;

	private bool _reloggingIn;

	private Updater.Info _pendingUpdate;

	private string _updateSeen = "";

	private string _adminSig = "";

	private static readonly Brush LimeBrightBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C3F400"));

	private static readonly Brush GuideCardBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1C1B1C"));

	private static readonly Brush GuideAmber = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8C84A"));

	private static readonly Brush GuideAmberFaint = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1FE8C84A"));

	private const double GuideW = 720.0;

	private string _chartMetric = "usd";

	private int _topDays;

	private static readonly (string key, string label)[] StatusGames = new(string, string)[2]
	{
		("mm2", "MM2"),
		("adoptme", "Adopt")
	};

	internal System.Windows.Controls.TextBlock VersionLbl;

	internal StackPanel NavBottom;

	internal StackPanel NavTop;

	internal System.Windows.Controls.TextBlock HeaderTitle;

	internal ComboBox GameCombo;

	internal System.Windows.Controls.TextBlock BellBtn;

	internal System.Windows.Controls.TextBlock ProfileBtn;

	internal Popup ProfilePopup;

	internal System.Windows.Controls.TextBlock ProfKey;

	internal System.Windows.Controls.TextBlock ProfDays;

	internal System.Windows.Controls.TextBlock ProfExp;

	internal Popup BellPopup;

	internal System.Windows.Controls.TextBlock BellText;

	internal System.Windows.Controls.Button UpdateBtn;

	internal Grid Body;

	internal Grid DashView;

	internal System.Windows.Controls.TextBlock FilterTitle;

	internal StackPanel FilterHost;

	internal System.Windows.Controls.RichTextBox Log;

	internal ProgressRing Ring;

	internal System.Windows.Controls.Button StopBtn;

	internal System.Windows.Controls.Button StartBtn;

	internal Grid SettingsView;

	internal ComboBox MainCombo;

	internal Wpf.Ui.Controls.TextBox AutoexecBox;

	internal ComboBox ThreadsCombo;

	internal StackPanel ExtraMainsHost;

	internal ComboBox LightFpsCombo;

	internal ComboBox WinModeCombo;

	internal Grid AccountsView;

	internal Wpf.Ui.Controls.Button CookiesBtn;

	internal Wpf.Ui.Controls.Button DistBtn;

	internal Wpf.Ui.Controls.Button ResetStatusBtn;

	internal Wpf.Ui.Controls.Button RemoveDoneBtn;

	internal Wpf.Ui.Controls.Button ClearExceptMainBtn;

	internal ItemsControl AccountsList;

	internal System.Windows.Controls.TextBlock AccountsEmpty;

	internal Grid AnalyticsView;

	internal Border ItemsCard;

	internal System.Windows.Controls.TextBlock TotalItemsLbl;

	internal System.Windows.Controls.TextBlock TotalUsdLbl;

	internal System.Windows.Controls.TextBlock AvgLbl;

	internal Popup BreakdownPopup;

	internal StackPanel BreakdownHost;

	internal System.Windows.Controls.TextBlock ChartTitle;

	internal Wpf.Ui.Controls.Button MetricUsdBtn;

	internal Wpf.Ui.Controls.Button MetricItemsBtn;

	internal Grid ChartHost;

	internal ComboBox TopDaysCombo;

	internal StackPanel TopHost;

	internal StackPanel AccHost;

	internal Grid AdminView;

	internal Wpf.Ui.Controls.Button AdminRefreshBtn;

	internal ProgressRing AdmSpinner;

	internal System.Windows.Controls.TextBlock AdmClients;

	internal System.Windows.Controls.TextBlock AdmOnline;

	internal System.Windows.Controls.TextBlock AdmItems;

	internal System.Windows.Controls.TextBlock AdmUsd;

	internal System.Windows.Controls.TextBlock AdmGames;

	internal StackPanel AdminList;

	internal Grid HelpView;

	internal ScrollViewer HelpScroll;

	internal StackPanel HelpContent;

	internal Grid ChangelogView;

	internal ScrollViewer ChangelogScroll;

	internal StackPanel ChangelogContent;

	internal Grid StubView;

	internal System.Windows.Controls.TextBlock StubText;

	private bool _contentLoaded;

	private static string CatKey(string label)
	{
		(string, string)[] adoptCats = AdoptCats;
		for (int i = 0; i < adoptCats.Length; i++)
		{
			(string, string) tuple = adoptCats[i];
			if (tuple.Item1 == label)
			{
				return tuple.Item2;
			}
		}
		return label;
	}

	public MainWindow()
	{
		InitializeComponent();
		VersionLbl.Text = "v2.8.5";
		Log.Document.Blocks.Clear();
		Log.Document.PagePadding = new Thickness(0.0);
		MainCombo.SelectionChanged += delegate
		{
			if (!_loading)
			{
				_settings.Main[_game] = (MainCombo.SelectedItem as string) ?? "";
				_settings.Save();
			}
		};
		BuildNav();
		BuildHelp();
		BuildChangelog();
		SmoothScroll.Enable(HelpScroll);
		SmoothScroll.Enable(ChangelogScroll);
		_weapDrop = BuildDropdown("Оружие", "Weapon", "⚔", Games.WeaponRarities, _weap);
		_petDrop = BuildDropdown("Питомцы", "Pet", "\ud83d\udc3e", Games.PetRarities, _pet);
		_catDrop = BuildDropdown("Категории", "AdoptCat", "\ud83d\udce6", AdoptCatTokens, _adoptCat, rarityNote: false);
		FilterHost.Children.Add(_weapDrop);
		FilterHost.Children.Add(_petDrop);
		FilterHost.Children.Add(_catDrop);
		GameCombo.Items.Add("MM2");
		GameCombo.Items.Add("Adopt Me");
		for (int i = 1; i <= 4; i++)
		{
			ThreadsCombo.Items.Add(i.ToString());
		}
		string[] array = new string[4] { "Всё время", "30 дней", "7 дней", "Сегодня" };
		foreach (string newItem in array)
		{
			TopDaysCombo.Items.Add(newItem);
		}
		TopDaysCombo.SelectedIndex = 0;
		array = new string[4] { "Без изменений", "30 FPS", "15 FPS", "5 FPS" };
		foreach (string newItem2 in array)
		{
			LightFpsCombo.Items.Add(newItem2);
		}
		array = new string[3] { "Маленькое", "Большое", "Полноэкранное" };
		foreach (string newItem3 in array)
		{
			WinModeCombo.Items.Add(newItem3);
		}
		LoadFromSettings();
		base.Loaded += async delegate
		{
			RefreshAccounts();
			await CheckLicenseAsync();
			await CheckUpdateAsync();
		};
		base.Closing += delegate
		{
			SaveToSettings();
		};
		DispatcherTimer dispatcherTimer = new DispatcherTimer();
		dispatcherTimer.Interval = TimeSpan.FromSeconds(2.0);
		dispatcherTimer.Tick += delegate
		{
			Task task = _task;
			if (task != null && !task.IsCompleted)
			{
				if (AccountsView.Visibility == Visibility.Visible)
				{
					RefreshAccountsList();
				}
				if (AnalyticsView.Visibility == Visibility.Visible)
				{
					BuildAnalytics();
				}
			}
		};
		dispatcherTimer.Start();
		DispatcherTimer dispatcherTimer2 = new DispatcherTimer();
		dispatcherTimer2.Interval = TimeSpan.FromMinutes(30.0);
		dispatcherTimer2.Tick += async delegate
		{
			await CheckLicenseAsync();
			await CheckUpdateAsync();
		};
		dispatcherTimer2.Start();
		Reporter.ReportAsync();
	}

	private async Task CheckLicenseAsync()
	{
		if (!_reloggingIn)
		{
			LicenseResult licenseResult = await LootCollector.Core.License.ValidateSavedAsync();
			if (!licenseResult.Ok && licenseResult.Reason != "network" && licenseResult.Reason != "offline")
			{
				ReLogin();
			}
		}
	}

	private async Task CheckUpdateAsync()
	{
		Updater.Info info = await Updater.CheckAsync();
		if (info != null && Updater.IsNewer(info.version) && !(info.version == _updateSeen))
		{
			_updateSeen = info.version;
			if (info.mandatory)
			{
				AppendLog("\ud83d\udd04 Обязательное обновление " + info.version + ". Загружаю…");
				await ApplyUpdate(info, forced: true);
			}
			else
			{
				_pendingUpdate = info;
				BellBtn.Foreground = Co.Lime;
			}
		}
	}

	private async void OnUpdateNow(object sender, RoutedEventArgs e)
	{
		BellPopup.IsOpen = false;
		if (_pendingUpdate != null)
		{
			await ApplyUpdate(_pendingUpdate, forced: false);
		}
	}

	private async Task ApplyUpdate(Updater.Info info, bool forced)
	{
		if (await Updater.DownloadAsync(info, AppendLog))
		{
			Task task = _task;
			if (task != null && !task.IsCompleted)
			{
				AppendLog("✅ Обновление " + info.version + " загружено — применится при следующем запуске.");
				return;
			}
			AppendLog("✅ Обновление " + info.version + " готово. Перезапуск…");
			Updater.ApplyAndRestart();
			Application.Current.Shutdown();
		}
	}

	private void OnSupport(object sender, RoutedEventArgs e)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "https://t.me/luxeeaa",
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			AppendLog("(не удалось открыть Telegram: " + ex.Message + ")");
		}
	}

	private void OnAdminRefresh(object sender, RoutedEventArgs e)
	{
		BuildAdmin();
	}

	private static (string text, Brush color) StatusVisual(string status)
	{
		if (!(status == "running"))
		{
			if (status == "online")
			{
				return (text: "\ud83d\udfe1 онлайн", color: new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6C84F")));
			}
			return (text: "⚪ оффлайн", color: Co.Dim);
		}
		return (text: "\ud83d\udfe2 работает", color: Co.Lime);
	}

	private async Task BuildAdmin()
	{
		if (AdmSpinner != null)
		{
			AdmSpinner.Visibility = Visibility.Visible;
		}
		Reporter.AdminStats adminStats = await Reporter.FetchAdminStatsAsync();
		if (AdmSpinner != null)
		{
			AdmSpinner.Visibility = Visibility.Collapsed;
		}
		if (adminStats == null)
		{
			if (AdminList.Children.Count == 0)
			{
				AdminList.Children.Add(new System.Windows.Controls.TextBlock
				{
					Text = "Не удалось получить статистику.",
					Foreground = Co.Dim,
					FontSize = 13.0,
					Margin = new Thickness(8.0)
				});
			}
			return;
		}
		AdmClients.Text = adminStats.Clients.ToString();
		AdmOnline.Text = $"{adminStats.Online} / {adminStats.Running}";
		AdmItems.Text = adminStats.Items.ToString("N0");
		AdmUsd.Text = $"{adminStats.Usd:0.00}$";
		AdmGames.Text = $"MM2: {adminStats.Mm2.items} шт / {adminStats.Mm2.usd:0.00}$      Adopt: {adminStats.Adopt.items} шт / {adminStats.Adopt.usd:0.00}$";
		StringBuilder stringBuilder = new StringBuilder();
		foreach (Reporter.ClientRow item3 in adminStats.List)
		{
			stringBuilder.Append(item3.Key).Append(':').Append(item3.Status)
				.Append(':')
				.Append(item3.Items)
				.Append(':')
				.Append(item3.Usd.ToString("0.00"))
				.Append(':')
				.Append(item3.Version)
				.Append('|');
		}
		string text = stringBuilder.ToString();
		if (text == _adminSig && AdminList.Children.Count > 0)
		{
			return;
		}
		_adminSig = text;
		AdminList.Children.Clear();
		if (adminStats.List.Count == 0)
		{
			AdminList.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = "Пока нет данных от клиентов.",
				Foreground = Co.Dim,
				FontSize = 13.0,
				Margin = new Thickness(8.0)
			});
			return;
		}
		foreach (Reporter.ClientRow item4 in adminStats.List)
		{
			Grid grid = new Grid
			{
				Margin = new Thickness(8.0, 3.0, 8.0, 3.0)
			};
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(130.0)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(72.0)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(170.0)
			});
			System.Windows.Controls.TextBlock element = new System.Windows.Controls.TextBlock
			{
				Text = item4.Key,
				Foreground = Co.Txt,
				FontFamily = MonoFont,
				FontSize = 12.5,
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(element, 0);
			grid.Children.Add(element);
			(string text, Brush color) tuple = StatusVisual(item4.Status);
			string item = tuple.text;
			Brush item2 = tuple.color;
			System.Windows.Controls.TextBlock element2 = new System.Windows.Controls.TextBlock
			{
				Text = item,
				Foreground = item2,
				FontSize = 12.0,
				FontWeight = FontWeights.SemiBold,
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(element2, 1);
			grid.Children.Add(element2);
			System.Windows.Controls.TextBlock element3 = new System.Windows.Controls.TextBlock
			{
				Text = (string.IsNullOrEmpty(item4.Version) ? "—" : ("v" + item4.Version)),
				Foreground = Co.Dim,
				FontFamily = MonoFont,
				FontSize = 11.5,
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(element3, 2);
			grid.Children.Add(element3);
			System.Windows.Controls.TextBlock element4 = new System.Windows.Controls.TextBlock
			{
				Text = $"{item4.Items} шт / {item4.Usd:0.00}$",
				Foreground = Co.Lime,
				FontSize = 12.5,
				FontWeight = FontWeights.SemiBold,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(element4, 3);
			grid.Children.Add(element4);
			AdminList.Children.Add(grid);
		}
	}

	private void ReLogin()
	{
		if (!_reloggingIn)
		{
			_reloggingIn = true;
			try
			{
				_cts?.Cancel();
			}
			catch
			{
			}
			LootCollector.Core.License.Forget();
			Hide();
			if (new LicenseWindow().ShowDialog().GetValueOrDefault())
			{
				_reloggingIn = false;
				Show();
			}
			else
			{
				Application.Current.Shutdown();
			}
		}
	}

	private void OnProfile(object sender, MouseButtonEventArgs e)
	{
		LicenseResult current = LootCollector.Core.License.Current;
		ProfKey.Text = LootCollector.Core.License.Mask(LootCollector.Core.License.CurrentKey);
		if (current == null)
		{
			ProfDays.Text = "—";
			ProfExp.Text = "";
		}
		else if (LootCollector.Core.License.IsLifetime(current))
		{
			ProfDays.Text = "Навсегда";
			ProfExp.Text = "бессрочный ключ";
		}
		else
		{
			ProfDays.Text = $"Осталось дней: {current.DaysLeft}";
			System.Windows.Controls.TextBlock profExp = ProfExp;
			DateTime? expiresAt = current.ExpiresAt;
			object text;
			if (expiresAt.HasValue)
			{
				DateTime valueOrDefault = expiresAt.GetValueOrDefault();
				text = $"до {valueOrDefault.ToLocalTime():dd.MM.yyyy HH:mm}";
			}
			else
			{
				text = "";
			}
			profExp.Text = (string)text;
		}
		ProfilePopup.IsOpen = true;
	}

	private void OnBell(object sender, MouseButtonEventArgs e)
	{
		LicenseResult current = LootCollector.Core.License.Current;
		if (_pendingUpdate != null)
		{
			string text = (string.IsNullOrWhiteSpace(_pendingUpdate.notes) ? "" : ("\n" + _pendingUpdate.notes));
			BellText.Text = "\ud83d\udd04 Доступно обновление " + _pendingUpdate.version + "." + text;
			UpdateBtn.Visibility = Visibility.Visible;
		}
		else
		{
			BellText.Text = ((current != null && !LootCollector.Core.License.IsLifetime(current) && current.DaysLeft <= 3) ? $"⚠\ufe0f Лицензия скоро истекает: осталось {current.DaysLeft} дн. Продлите ключ." : "Новых уведомлений нет.");
			UpdateBtn.Visibility = Visibility.Collapsed;
		}
		BellPopup.IsOpen = true;
	}

	private void BuildNav()
	{
		AddNav(NavTop, "Dashboard", "▦", "dash");
		AddNav(NavTop, "Accounts", "\ud83d\udc65", "accounts");
		AddNav(NavTop, "Settings", "⚙", "settings");
		AddNav(NavTop, "Analytics", "\ud83d\udcc8", "analytics");
		LicenseResult current = LootCollector.Core.License.Current;
		if (current != null && current.Admin)
		{
			AddNav(NavTop, "Admin", "\ud83d\udee0", "admin");
		}
		AddNav(NavTop, "Help", "❔", "help");
		AddNav(NavTop, "Changelog", "\ud83d\udd52", "changelog");
		AddNav(NavBottom, "Support", "\ud83d\udedf", null, delegate
		{
			OnSupport(this, null);
		});
		SetActiveNav("Dashboard");
	}

	private void AddNav(Panel host, string label, string icon, string view, Action onClick = null)
	{
		System.Windows.Controls.TextBlock textBlock = new System.Windows.Controls.TextBlock
		{
			Text = icon,
			FontSize = 16.0,
			Width = 24.0,
			VerticalAlignment = VerticalAlignment.Center,
			Foreground = Co.Dim
		};
		System.Windows.Controls.TextBlock textBlock2 = new System.Windows.Controls.TextBlock
		{
			Text = label,
			FontFamily = UiFont,
			FontSize = 14.0,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			Foreground = Co.Dim
		};
		StackPanel element = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Children = 
			{
				(UIElement)textBlock,
				(UIElement)textBlock2
			}
		};
		Border border = new Border
		{
			Width = 2.0,
			CornerRadius = new CornerRadius(1.0),
			Background = Brushes.Transparent,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		DockPanel dockPanel = new DockPanel();
		DockPanel.SetDock(border, Dock.Left);
		dockPanel.Children.Add(border);
		dockPanel.Children.Add(element);
		Border border2 = new Border
		{
			CornerRadius = new CornerRadius(10.0),
			Padding = new Thickness(8.0, 9.0, 12.0, 9.0),
			Child = dockPanel,
			Background = Brushes.Transparent
		};
		ToggleButton tb = new ToggleButton
		{
			Content = border2,
			Style = (Style)FindResource("Plain"),
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		};
		tb.Tag = new object[4] { border2, border, textBlock, textBlock2 };
		if (onClick != null)
		{
			tb.Click += delegate
			{
				tb.IsChecked = false;
				onClick();
			};
		}
		else
		{
			tb.Click += delegate
			{
				SetActiveNav(label);
				ShowView(view, label);
			};
		}
		_nav[label] = tb;
		host.Children.Add(tb);
	}

	private void SetActiveNav(string label)
	{
		foreach (KeyValuePair<string, ToggleButton> item in _nav)
		{
			object[] obj = (object[])item.Value.Tag;
			Border border = (Border)obj[0];
			Border border2 = (Border)obj[1];
			System.Windows.Controls.TextBlock textBlock = (System.Windows.Controls.TextBlock)obj[2];
			System.Windows.Controls.TextBlock obj2 = (System.Windows.Controls.TextBlock)obj[3];
			bool flag = item.Key == label;
			border.Background = (flag ? Co.LimeFaint : Brushes.Transparent);
			border2.Background = (flag ? Co.Lime : Brushes.Transparent);
			textBlock.Foreground = (flag ? Co.Lime : Co.Dim);
			obj2.Foreground = (flag ? Co.Lime : Co.Dim);
		}
	}

	private void ShowView(string view, string label)
	{
		DashView.Visibility = ((!(view == "dash")) ? Visibility.Collapsed : Visibility.Visible);
		SettingsView.Visibility = ((!(view == "settings")) ? Visibility.Collapsed : Visibility.Visible);
		AccountsView.Visibility = ((!(view == "accounts")) ? Visibility.Collapsed : Visibility.Visible);
		AnalyticsView.Visibility = ((!(view == "analytics")) ? Visibility.Collapsed : Visibility.Visible);
		AdminView.Visibility = ((!(view == "admin")) ? Visibility.Collapsed : Visibility.Visible);
		HelpView.Visibility = ((!(view == "help")) ? Visibility.Collapsed : Visibility.Visible);
		ChangelogView.Visibility = ((!(view == "changelog")) ? Visibility.Collapsed : Visibility.Visible);
		StubView.Visibility = ((!(view == "stub")) ? Visibility.Collapsed : Visibility.Visible);
		if (view == "accounts")
		{
			RefreshAccountsList();
		}
		if (view == "analytics")
		{
			BuildAnalytics();
		}
		if (view == "admin")
		{
			_adminSig = "";
			BuildAdmin();
		}
		if (view == "stub")
		{
			StubText.Text = "«" + label + "» — раздел в разработке";
		}
		Body.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180.0)));
	}

	private System.Windows.Controls.TextBlock GuideH1(string t)
	{
		return new System.Windows.Controls.TextBlock
		{
			Text = t,
			Foreground = Co.Txt,
			FontFamily = UiFont,
			FontSize = 26.0,
			FontWeight = FontWeights.Bold,
			Margin = new Thickness(0.0, 0.0, 0.0, 6.0),
			HorizontalAlignment = HorizontalAlignment.Left
		};
	}

	private System.Windows.Controls.TextBlock GuideSub(string t)
	{
		return new System.Windows.Controls.TextBlock
		{
			Text = t,
			Foreground = Co.Dim,
			FontFamily = UiFont,
			FontSize = 14.0,
			TextWrapping = TextWrapping.Wrap,
			LineHeight = 21.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 22.0),
			MaxWidth = 720.0,
			HorizontalAlignment = HorizontalAlignment.Left
		};
	}

	private System.Windows.Controls.TextBlock GuideSectionTitle(string t)
	{
		return new System.Windows.Controls.TextBlock
		{
			Text = t,
			Foreground = Co.Txt,
			FontFamily = UiFont,
			FontSize = 16.0,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(2.0, 14.0, 0.0, 12.0),
			HorizontalAlignment = HorizontalAlignment.Left
		};
	}

	private static Run T(string s)
	{
		return new Run(s);
	}

	private Run Hl(string s)
	{
		return new Run(s)
		{
			Foreground = Co.Lime,
			FontWeight = FontWeights.SemiBold
		};
	}

	private System.Windows.Controls.TextBlock Rich(params Inline[] ins)
	{
		System.Windows.Controls.TextBlock textBlock = new System.Windows.Controls.TextBlock
		{
			Foreground = Co.Dim,
			FontFamily = UiFont,
			FontSize = 13.5,
			TextWrapping = TextWrapping.Wrap,
			LineHeight = 21.0
		};
		foreach (Inline item in ins)
		{
			textBlock.Inlines.Add(item);
		}
		return textBlock;
	}

	private Border Card(UIElement child, Brush bg = null, Brush stroke = null)
	{
		return new Border
		{
			Background = (bg ?? GuideCardBg),
			CornerRadius = new CornerRadius(16.0),
			Padding = new Thickness(22.0, 18.0, 22.0, 20.0),
			BorderBrush = (stroke ?? Co.Stroke),
			BorderThickness = new Thickness(1.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 14.0),
			Child = child,
			MaxWidth = 720.0,
			HorizontalAlignment = HorizontalAlignment.Left
		};
	}

	private UIElement Bullet(params Inline[] ins)
	{
		System.Windows.Controls.TextBlock element = new System.Windows.Controls.TextBlock
		{
			Text = "•",
			Foreground = Co.Lime,
			FontSize = 14.0,
			Margin = new Thickness(0.0, 0.0, 9.0, 0.0),
			VerticalAlignment = VerticalAlignment.Top
		};
		DockPanel obj = new DockPanel
		{
			Margin = new Thickness(2.0, 0.0, 0.0, 7.0)
		};
		DockPanel.SetDock(element, Dock.Left);
		obj.Children.Add(element);
		obj.Children.Add(Rich(ins));
		return obj;
	}

	private UIElement Step(int n, string title, params Inline[] body)
	{
		Border element = new Border
		{
			Width = 30.0,
			Height = 30.0,
			CornerRadius = new CornerRadius(15.0),
			Background = Co.LimeFaint,
			BorderBrush = Co.Lime,
			BorderThickness = new Thickness(1.0),
			VerticalAlignment = VerticalAlignment.Top,
			Child = new System.Windows.Controls.TextBlock
			{
				Text = n.ToString(),
				Foreground = Co.Lime,
				FontFamily = UiFont,
				FontWeight = FontWeights.Bold,
				FontSize = 14.0,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(14.0, 0.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(new System.Windows.Controls.TextBlock
		{
			Text = title,
			Foreground = Co.Txt,
			FontFamily = UiFont,
			FontSize = 14.5,
			FontWeight = FontWeights.SemiBold,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 4.0, 0.0, 4.0)
		});
		if (body.Length != 0)
		{
			stackPanel.Children.Add(Rich(body));
		}
		DockPanel obj = new DockPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 18.0)
		};
		DockPanel.SetDock(element, Dock.Left);
		obj.Children.Add(element);
		obj.Children.Add(stackPanel);
		return obj;
	}

	private Border Concept(string emoji, string title, params UIElement[] body)
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		stackPanel.Children.Add(new System.Windows.Controls.TextBlock
		{
			Text = emoji,
			FontSize = 19.0,
			Margin = new Thickness(0.0, 0.0, 11.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		});
		stackPanel.Children.Add(new System.Windows.Controls.TextBlock
		{
			Text = title,
			Foreground = Co.Txt,
			FontFamily = UiFont,
			FontSize = 15.5,
			FontWeight = FontWeights.SemiBold,
			VerticalAlignment = VerticalAlignment.Center
		});
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Children.Add(stackPanel);
		foreach (UIElement element in body)
		{
			stackPanel2.Children.Add(element);
		}
		return Card(stackPanel2);
	}

	private Border Callout(string emoji, params Inline[] body)
	{
		DockPanel dockPanel = new DockPanel();
		System.Windows.Controls.TextBlock element = new System.Windows.Controls.TextBlock
		{
			Text = emoji,
			FontSize = 16.0,
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
			VerticalAlignment = VerticalAlignment.Top
		};
		DockPanel.SetDock(element, Dock.Left);
		dockPanel.Children.Add(element);
		System.Windows.Controls.TextBlock textBlock = Rich(body);
		textBlock.Foreground = Co.Txt;
		dockPanel.Children.Add(textBlock);
		return new Border
		{
			Background = GuideAmberFaint,
			BorderBrush = GuideAmber,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(12.0),
			Padding = new Thickness(14.0, 11.0, 16.0, 11.0),
			Margin = new Thickness(0.0, 2.0, 0.0, 4.0),
			Child = dockPanel
		};
	}

	private void BuildHelp()
	{
		HelpContent.Children.Clear();
		HelpContent.Children.Add(GuideH1("Как пользоваться"));
		HelpContent.Children.Add(GuideSub("Пошаговый гайд: от загрузки аккаунтов до сбора лута. Делайте по порядку — и всё заработает. Внизу есть кнопка поддержки, если что-то непонятно."));
		HelpContent.Children.Add(GuideSectionTitle("С чего начать"));
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(Step(1, "Загрузите аккаунты (куки)", T("Вкладка "), Hl("Accounts"), T(" → кнопка "), Hl("\ud83c\udf6a Загрузить куки"), T(". Вставьте "), T(".ROBLOSECURITY каждого аккаунта — по одной куке на строку — и нажмите "), Hl("Импортировать"), T(". Ники программа определит сама. Куки — это «вход» в аккаунт; они хранятся только на вашем ПК.")));
		stackPanel.Children.Add(Step(2, "Выберите главного (приёмника)", T("Вкладка "), Hl("Settings"), T(" → поле "), Hl("Главный (приёмник)"), T(". Это аккаунт, на который слетится весь лут. Если ника нет в списке — нажмите "), Hl("Обновить"), T(".")));
		stackPanel.Children.Add(Step(3, "Укажите папку экзекьютора и включите авто-атач", T("Вкладка "), Hl("Settings"), T(" → "), Hl("Папка экзекьютора"), T(". Нажмите "), Hl("Найти"), T(" — программа сама подхватит инжектор (Potassium, Xeno, Wave, Volt). Не нашла — нажмите "), Hl("Обзор"), T(" и выберите папку инжектора (внутри неё должны лежать "), Hl("autoexec"), T(" и "), Hl("workspace"), T("). Оставите пусто — будет авто-режим (Potassium). "), Hl("⚠ Обязательно"), T(" включите в самом экзекьюторе "), Hl("авто-атач (Auto-Attach / Auto-Execute / Auto-Inject)"), T(" — иначе скрипт не вколется в Roblox и сбор не начнётся.")));
		stackPanel.Children.Add(Step(4, "По желанию: потоки и лёгкий режим", Hl("Потоки"), T(" — сколько главных собирают параллельно (быстрее, но тяжелее для ПК). "), Hl("Лёгкий режим"), T(" — настраиваемый кап FPS и размер окон Roblox, чтобы тянуть много окон сразу. Подробнее — ниже.")));
		stackPanel.Children.Add(Step(5, "Выберите игру, фильтры и жмите СТАРТ", T("Вверху выберите "), Hl("MM2"), T(" или "), Hl("Adopt Me"), T(". На "), Hl("Dashboard"), T(" при желании настройте "), Hl("Фильтры редкости"), T(". Затем — "), Hl("▶ СТАРТ"), T(". Прогресс виден в "), Hl("Activity Log"), T(". Остановить в любой момент — "), Hl("■ СТОП"), T(".")));
		HelpContent.Children.Add(Card(stackPanel));
		HelpContent.Children.Add(GuideSectionTitle("Разбираемся в настройках"));
		HelpContent.Children.Add(Concept("\ud83d\udcc1", "Папка экзекьютора", Rich(T("Это папка вашего инжектора. Внутри неё программа находит "), Hl("autoexec"), T(" (туда кладётся скрипт сбора) и "), Hl("workspace"), T(" (туда игра пишет результат).")), new System.Windows.Controls.TextBlock
		{
			Height = 8.0
		}, Bullet(Hl("Найти"), T(" — авто-поиск среди Potassium, Xeno, Wave, Volt.")), Bullet(Hl("Обзор"), T(" — выбрать папку вручную, если инжектор в нестандартном месте.")), Bullet(T("Регистр не важен: "), Hl("autoexec/AutoExecute"), T(" и "), Hl("workspace/Workspace"), T(" распознаются сами."))));
		HelpContent.Children.Add(Concept("\ud83e\uddf5", "Потоки", Rich(T("Сколько аккаунтов-главных работают одновременно. На каждый поток открывается своё окно Roblox.")), new System.Windows.Controls.TextBlock
		{
			Height = 8.0
		}, Bullet(Hl("1"), T(" — обычный режим, одно окно.")), Bullet(T("Больше — быстрее сбор, но выше нагрузка на ПК (по окну на поток).")), Bullet(T("Для каждого потока выбирается свой "), Hl("Главный #2…N"), T(" (приёмник этого потока) прямо под выбором потоков.")), Bullet(T("В конце прогона весь лут сводится на первого главного — итог в одном месте."))));
		HelpContent.Children.Add(Concept("\ud83c\udf9a", "Фильтры редкости", Rich(T("На "), Hl("Dashboard"), T(" выбираете, какие редкости собирать — отдельно "), Hl("Оружие"), T(" и "), Hl("Питомцы"), T(". По умолчанию берётся всё.")), new System.Windows.Controls.TextBlock
		{
			Height = 10.0
		}, Callout("⚠", T("На "), Hl("Xeno"), T(" фильтр редкости не работает — собирается всё подряд (на этом инжекторе база редкостей недоступна). На Potassium фильтр работает как обычно."))));
		HelpContent.Children.Add(Concept("⚡", "Лёгкий режим", Rich(T("Настройте, если ПК слабый или открываете много окон. Выберите "), Hl("кап FPS"), T(" (5 / 15 / 30 или без изменений) и "), Hl("размер окна Roblox"), T(" (маленькое / большое / полноэкранное). FPS ниже + меньше окно = меньше нагрузки. Это только производительность — на сам сбор и аккаунты не влияет."))));
		HelpContent.Children.Add(Concept("\ud83d\udc64", "Аккаунты: кнопки и статусы", Bullet(Hl("\ud83c\udf6a Загрузить куки"), T(" — добавить аккаунты.")), Bullet(Hl("\ud83d\udce6 Расформировка"), T(" — обратный режим: раздать предметы с главного по другим аккаунтам.")), Bullet(Hl("\ud83e\uddf9 Убрать всех, кроме главного"), T(" — удалить куки всех альтов, оставить только главных (с подтверждением).")), new System.Windows.Controls.TextBlock
		{
			Height = 6.0
		}, Bullet(T("Во время прогона рядом с аккаунтом появляется статус "), Hl("сдал MM2/Adopt"), T(". Наведите на него — появится крестик, чтобы снять статус вручную."))));
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Children.Add(new System.Windows.Controls.TextBlock
		{
			Text = "Остались вопросы?",
			Foreground = Co.Txt,
			FontFamily = UiFont,
			FontSize = 16.0,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		});
		stackPanel2.Children.Add(new System.Windows.Controls.TextBlock
		{
			Text = "Если есть вопросы по программе, предложения по улучшению или вы нашли баг — напишите в поддержку. Поможем с настройкой и ответим на любой вопрос.",
			Foreground = Co.Dim,
			FontFamily = UiFont,
			FontSize = 13.5,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 14.0)
		});
		System.Windows.Controls.TextBlock child = new System.Windows.Controls.TextBlock
		{
			Text = "\ud83d\udcac Написать в поддержку  ·  @luxeeaa",
			Foreground = Brushes.Black,
			FontFamily = UiFont,
			FontWeight = FontWeights.Bold,
			FontSize = 14.0
		};
		Border border = new Border
		{
			Background = Co.Lime,
			CornerRadius = new CornerRadius(24.0),
			Padding = new Thickness(26.0, 13.0, 26.0, 13.0),
			HorizontalAlignment = HorizontalAlignment.Left,
			Cursor = Cursors.Hand,
			Child = child
		};
		border.MouseEnter += delegate(object s, MouseEventArgs _)
		{
			((Border)s).Background = LimeBrightBrush;
		};
		border.MouseLeave += delegate(object s, MouseEventArgs _)
		{
			((Border)s).Background = Co.Lime;
		};
		border.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs _)
		{
			OnSupport(s, null);
		};
		stackPanel2.Children.Add(border);
		HelpContent.Children.Add(Card(stackPanel2, null, Co.Lime));
	}

	private void BuildChangelog()
	{
		ChangelogContent.Children.Clear();
		ChangelogContent.Children.Add(GuideH1("История обновлений"));
		ChangelogContent.Children.Add(GuideSub("Что нового в каждой версии. Обновления приходят автоматически — отдельно качать ничего не нужно."));
		(string, bool, string[])[] array = new(string, bool, string[])[23]
		{
			("2.8.5", true, new string[1] { "Исправление стабильности на части экзекьюторов." }),
			("2.8.4", false, new string[1] { "Технические улучшения и защита скриптов." }),
			("2.8.3", false, new string[1] { "Оптимизация: меньше фоновых обращений к серверу (статус шлётся по событиям, без постоянного опроса) — стабильнее и легче." }),
			("2.8.2", false, new string[1] { "Help: подсказка про поддержку — вопросы, предложения и баги пишите в @luxeeaa." }),
			("2.8.1", false, new string[1] { "Admin: статистика обновляется по кнопке «Обновить» (убран постоянный авто-опрос — меньше нагрузки на сеть)." }),
			("2.8.0", false, new string[1] { "Аккаунты: рядом с каждым ником — значок копирования (клик → ник в буфере, значок становится зелёной галочкой)." }),
			("2.7.9", false, new string[1] { "Аккаунты: кнопка «Убрать всех, кроме главного» перенесена под остальные — больше не вылезает за край." }),
			("2.7.8", false, new string[6] { "Аккаунты: вкладка больше не подвисает при 1000+ акков — список рисует только видимые строки.", "Аккаунты: кнопка «Убрать сданные» — разом удаляет акки, что уже отдали лут (главные не трогаются).", "Лёгкий режим теперь настраивается: кап FPS (5 / 15 / 30 / без изменений) и размер окна Roblox (маленькое / большое / полноэкранное).", "Аналитика: топ аккаунтов по собранному луту + фильтр топа предметов по дням.", "Аналитика: убран белый ползунок прокрутки, перекрывавший цены.", "Настройки стали чище — убраны лишние подсказки." }),
			("2.7.7", false, new string[4] { "«Убрать всех, кроме главного» больше не удаляет доп-главных при многопотоке.", "Импорт кук: при большом количестве не теряются из-за лимита Roblox (ждём и повторяем).", "MM2: аккаунты ниже 10 уровня пропускаются (трейд им недоступен).", "Аккаунты: кнопка «Сбросить статусы». Аналитика: топ предметов по дням." }),
			("2.7.6", false, new string[1] { "Подсказка про авто-атач экзекьютора — в настройках и гайде (Help)." }),
			("2.7.5", false, new string[1] { "Технические улучшения статистики и стабильности." }),
			("2.7.4", false, new string[1] { "Adopt Me: исправлено зависание при сдаче — нетрейдабл-предметы больше не стопорят трейд." }),
			("2.7.3", false, new string[1] { "Мелкие правки оформления (единый шрифт у кнопок Старт/Стоп)." }),
			("2.7.2", false, new string[2] { "Adopt Me: исправлены подвисания при сдаче — трейды идут без задержек.", "Цены: исправлен подсчёт стоимости (бывало $0) и сделан надёжный источник цен — без сбоев обновления." }),
			("2.7.1", false, new string[4] { "Adopt Me: фильтр категорий — выбор, что сдавать (питомцы, одежда, коляски, машины, игрушки).", "Adopt Me: точная оценка с вариантами — neon, mega neon, летающие и ездовые считаются по своей цене.", "Adopt Me: надёжное чтение инвентаря и сдача (работает и на экзекьюторах без getgc).", "Многопоток: для нескольких потоков используйте Potassium (на Xeno — 1 поток)." }),
			("2.6.9", false, new string[1] { "Исправлена оценка Chroma-предметов — раньше цена не находилась и считалась $0." }),
			("2.6.8", false, new string[2] { "Плавная прокрутка в гайде и истории обновлений.", "Доработки оформления гайда (перенос текста, кнопки)." }),
			("2.6.7", false, new string[3] { "Подробный гайд «Как пользоваться» в разделе Help.", "Новый раздел «История обновлений».", "Обновлённый логотип приложения." }),
			("2.6.6", false, new string[1] { "Логотип в боковом меню и на иконке приложения." }),
			("2.6.5", false, new string[1] { "Своя иконка приложения вместо стандартной." }),
			("2.6.4", false, new string[4] { "Поддержка инжекторов Xeno, Wave и Volt (не только Potassium): авто-поиск папки + кнопки «Найти» и «Обзор».", "Лёгкий режим теперь реально ограничивает FPS до 30 и снижает графику.", "Исправлен баг Xeno: персонаж больше не «плавает», вернулся звук.", "Подсказка про фильтр редкости на Xeno." }),
			("2.6.2", false, new string[2] { "Многопоточный сбор: несколько главных работают параллельно, в конце лут сводится на одного.", "Кнопка «Убрать всех, кроме главного» в Accounts." }),
			("2.6.0", false, new string[4] { "Лицензионные ключи с привязкой к ПК и фоновые авто-обновления.", "Полная стоимость инвентаря главного в итоговом отчёте.", "Аналитика: средняя цена за предмет, разбивка по категориям, живой график по дням.", "Живые статусы аккаунтов («сдал MM2/Adopt») прямо в списке." })
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, bool, string[]) tuple = array[i];
			ChangelogContent.Children.Add(ChangelogCard(tuple.Item1, tuple.Item2, tuple.Item3));
		}
	}

	private Border ChangelogCard(string ver, bool latest, string[] items)
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		stackPanel.Children.Add(new Border
		{
			Background = (latest ? Co.Lime : Co.LimeFaint),
			CornerRadius = new CornerRadius(8.0),
			Padding = new Thickness(11.0, 3.0, 11.0, 4.0),
			VerticalAlignment = VerticalAlignment.Center,
			Child = new System.Windows.Controls.TextBlock
			{
				Text = "v" + ver,
				Foreground = (latest ? Brushes.Black : Co.Lime),
				FontFamily = MonoFont,
				FontSize = 13.0,
				FontWeight = FontWeights.Bold
			}
		});
		if (latest)
		{
			stackPanel.Children.Add(new Border
			{
				Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
				Background = Co.LimeFaint,
				CornerRadius = new CornerRadius(8.0),
				Padding = new Thickness(9.0, 3.0, 9.0, 4.0),
				VerticalAlignment = VerticalAlignment.Center,
				Child = new System.Windows.Controls.TextBlock
				{
					Text = "сейчас",
					Foreground = Co.Lime,
					FontFamily = UiFont,
					FontSize = 11.0,
					FontWeight = FontWeights.SemiBold
				}
			});
		}
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Children.Add(stackPanel);
		foreach (string s in items)
		{
			stackPanel2.Children.Add(Bullet(T(s)));
		}
		return Card(stackPanel2);
	}

	private void BuildCards(WrapPanel host, string category, string icon, string[] tokens, Dictionary<string, ToggleButton> store)
	{
		foreach (string text in tokens)
		{
			System.Windows.Controls.TextBlock nm = new System.Windows.Controls.TextBlock
			{
				Text = text,
				FontFamily = UiFont,
				FontSize = 12.5,
				FontWeight = FontWeights.SemiBold,
				Foreground = Co.Txt,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			System.Windows.Controls.TextBlock child = nm;
			Border border = new Border
			{
				CornerRadius = new CornerRadius(9.0),
				Padding = new Thickness(10.0, 5.0, 12.0, 5.0),
				Background = Co.Card,
				BorderBrush = Co.Stroke,
				BorderThickness = new Thickness(1.0),
				Child = child,
				RenderTransformOrigin = new Point(0.5, 0.5)
			};
			ScaleTransform st = new ScaleTransform(1.0, 1.0);
			border.RenderTransform = st;
			ToggleButton tb = new ToggleButton
			{
				Content = border,
				Style = (Style)FindResource("Plain"),
				Margin = new Thickness(0.0, 0.0, 7.0, 7.0)
			};
			tb.Checked += delegate
			{
				Upd();
				Pop();
			};
			tb.Unchecked += delegate
			{
				Upd();
				Pop();
			};
			Upd();
			store[text] = tb;
			host.Children.Add(tb);
			void Pop()
			{
				DoubleAnimation animation = new DoubleAnimation(0.93, 1.0, TimeSpan.FromMilliseconds(170.0))
				{
					EasingFunction = new BackEase
					{
						EasingMode = EasingMode.EaseOut,
						Amplitude = 0.5
					}
				};
				st.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
				st.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
			}
			void Upd()
			{
				bool valueOrDefault = tb.IsChecked.GetValueOrDefault();
				border.BorderBrush = (valueOrDefault ? Co.Lime : Co.Stroke);
				border.Effect = (valueOrDefault ? new DropShadowEffect
				{
					Color = (Color)ColorConverter.ConvertFromString("#ABD600"),
					BlurRadius = 12.0,
					ShadowDepth = 0.0,
					Opacity = 0.35
				} : null);
				nm.Foreground = (valueOrDefault ? Co.Lime : Co.Txt);
			}
		}
	}

	private FrameworkElement BuildDropdown(string title, string category, string icon, string[] tokens, Dictionary<string, ToggleButton> store, bool rarityNote = true)
	{
		System.Windows.Controls.TextBlock element = new System.Windows.Controls.TextBlock
		{
			Text = icon,
			FontSize = 14.0,
			Foreground = Co.Dim,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		System.Windows.Controls.TextBlock element2 = new System.Windows.Controls.TextBlock
		{
			Text = title,
			FontFamily = UiFont,
			FontSize = 13.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = Co.Txt,
			VerticalAlignment = VerticalAlignment.Center
		};
		System.Windows.Controls.TextBlock cnt = new System.Windows.Controls.TextBlock
		{
			FontFamily = MonoFont,
			FontSize = 11.0,
			Foreground = Co.Lime,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(8.0, 0.0, 8.0, 0.0)
		};
		System.Windows.Controls.TextBlock element3 = new System.Windows.Controls.TextBlock
		{
			Text = "▾",
			FontSize = 11.0,
			Foreground = Co.Dim,
			VerticalAlignment = VerticalAlignment.Center
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		stackPanel.Children.Add(element);
		stackPanel.Children.Add(element2);
		stackPanel.Children.Add(cnt);
		stackPanel.Children.Add(element3);
		Border content = new Border
		{
			CornerRadius = new CornerRadius(9.0),
			Padding = new Thickness(14.0, 9.0, 14.0, 9.0),
			Background = Co.Card,
			BorderBrush = Co.Stroke,
			BorderThickness = new Thickness(1.0),
			Child = stackPanel
		};
		ToggleButton header = new ToggleButton
		{
			Content = content,
			Style = (Style)FindResource("Plain"),
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
		};
		WrapPanel wrapPanel = new WrapPanel
		{
			MaxWidth = 380.0
		};
		BuildCards(wrapPanel, category, icon, tokens, store);
		System.Windows.Controls.TextBlock note = new System.Windows.Controls.TextBlock
		{
			Foreground = new SolidColorBrush(Color.FromRgb(byte.MaxValue, 193, 7)),
			FontSize = 11.5,
			FontFamily = UiFont,
			TextWrapping = TextWrapping.Wrap,
			MaxWidth = 360.0,
			Margin = new Thickness(2.0, 0.0, 0.0, 9.0),
			Visibility = Visibility.Collapsed
		};
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Children.Add(note);
		stackPanel2.Children.Add(wrapPanel);
		Border child = new Border
		{
			Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#201F20")),
			BorderBrush = Co.Stroke,
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(12.0),
			Padding = new Thickness(12.0, 12.0, 5.0, 5.0),
			Child = stackPanel2,
			Effect = new DropShadowEffect
			{
				Color = Colors.Black,
				BlurRadius = 24.0,
				ShadowDepth = 0.0,
				Opacity = 0.55
			}
		};
		Popup popup = new Popup
		{
			Child = child,
			PlacementTarget = header,
			Placement = PlacementMode.Bottom,
			StaysOpen = false,
			AllowsTransparency = true,
			PopupAnimation = PopupAnimation.Fade,
			VerticalOffset = 6.0
		};
		header.Checked += delegate
		{
			if (rarityNote)
			{
				string text = Paths.NoRarityExecutor(_settings.AutoexecDir);
				if (!string.IsNullOrEmpty(text))
				{
					note.Text = "⚠ На " + text + " фильтр редкости не работает — собирается всё.";
					note.Visibility = Visibility.Visible;
				}
				else
				{
					note.Visibility = Visibility.Collapsed;
				}
			}
			popup.IsOpen = true;
		};
		header.Unchecked += delegate
		{
			popup.IsOpen = false;
		};
		popup.Closed += delegate
		{
			header.IsChecked = false;
		};
		foreach (ToggleButton value in store.Values)
		{
			value.Checked += delegate
			{
				UpdCount();
			};
			value.Unchecked += delegate
			{
				UpdCount();
			};
		}
		UpdCount();
		return new Grid
		{
			VerticalAlignment = VerticalAlignment.Top,
			Children = 
			{
				(UIElement)header,
				(UIElement)popup
			}
		};
		void UpdCount()
		{
			cnt.Text = $"{store.Values.Count((ToggleButton c) => c.IsChecked.GetValueOrDefault())}/{tokens.Length}";
		}
	}

	private void OnGameChanged(object sender, SelectionChangedEventArgs e)
	{
		Task task = _task;
		if (task == null || task.IsCompleted)
		{
			_game = ((GameCombo.SelectedIndex == 1) ? "adoptme" : "mm2");
			bool flag = _game == "mm2";
			if (_weapDrop != null)
			{
				_weapDrop.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			}
			if (_petDrop != null)
			{
				_petDrop.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			}
			if (_catDrop != null)
			{
				_catDrop.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			}
			if (FilterTitle != null)
			{
				FilterTitle.Text = (flag ? "Фильтры редкости" : "Фильтры");
			}
			Body?.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.3, 1.0, TimeSpan.FromMilliseconds(220.0)));
			if (MainCombo != null)
			{
				string text = _settings.MainFor(_game);
				MainCombo.SelectedItem = (MainCombo.Items.Contains(text) ? text : null);
			}
			BuildExtraMains();
			if (AnalyticsView != null && AnalyticsView.Visibility == Visibility.Visible)
			{
				BuildAnalytics();
			}
		}
	}

	private void LoadFromSettings()
	{
		_loading = true;
		GameCombo.SelectedIndex = 0;
		_game = "mm2";
		ThreadsCombo.SelectedItem = ((_settings.Threads < 1) ? 1 : _settings.Threads).ToString();
		ComboBox lightFpsCombo = LightFpsCombo;
		lightFpsCombo.SelectedItem = _settings.LightFps switch
		{
			30 => "30 FPS", 
			15 => "15 FPS", 
			5 => "5 FPS", 
			_ => "Без изменений", 
		};
		lightFpsCombo = WinModeCombo;
		string winMode = _settings.WinMode;
		string selectedItem = ((winMode == "large") ? "Большое" : ((!(winMode == "full")) ? "Маленькое" : "Полноэкранное"));
		lightFpsCombo.SelectedItem = selectedItem;
		AutoexecBox.Text = _settings.AutoexecDir ?? "";
		foreach (KeyValuePair<string, ToggleButton> item in _weap)
		{
			item.Value.IsChecked = _settings.WeaponRarities == null || _settings.WeaponRarities.Contains(item.Key);
		}
		foreach (KeyValuePair<string, ToggleButton> item2 in _pet)
		{
			item2.Value.IsChecked = _settings.PetRarities != null && _settings.PetRarities.Contains(item2.Key);
		}
		foreach (KeyValuePair<string, ToggleButton> item3 in _adoptCat)
		{
			item3.Value.IsChecked = _settings.AdoptCategories == null || _settings.AdoptCategories.Contains(CatKey(item3.Key));
		}
		_loading = false;
	}

	private void SaveToSettings()
	{
		_settings.AutoexecDir = AutoexecBox.Text.Trim();
		_settings.Main[_game] = (MainCombo.SelectedItem as string) ?? "";
		_settings.Threads = ThreadsCount();
		Settings settings = _settings;
		settings.LightFps = (LightFpsCombo.SelectedItem as string) switch
		{
			"30 FPS" => 30, 
			"15 FPS" => 15, 
			"5 FPS" => 5, 
			_ => 0, 
		};
		settings = _settings;
		string text = WinModeCombo.SelectedItem as string;
		string winMode = ((text == "Большое") ? "large" : ((!(text == "Полноэкранное")) ? "small" : "full"));
		settings.WinMode = winMode;
		_settings.ExtraMains[_game] = _extraMainCombos.Select((ComboBox c) => (c.SelectedItem as string) ?? "").ToList();
		_settings.WeaponRarities = (from kv in _weap
			where kv.Value.IsChecked.GetValueOrDefault()
			select kv.Key).ToList();
		_settings.PetRarities = (from kv in _pet
			where kv.Value.IsChecked.GetValueOrDefault()
			select kv.Key).ToList();
		_settings.AdoptCategories = (from kv in _adoptCat
			where kv.Value.IsChecked.GetValueOrDefault()
			select CatKey(kv.Key)).ToList();
		_settings.Save();
	}

	private void RefreshAccounts(object sender = null, RoutedEventArgs e = null)
	{
		_loading = true;
		List<string> list = _store.Usernames();
		string text = MainCombo.SelectedItem as string;
		MainCombo.Items.Clear();
		foreach (string item in list)
		{
			MainCombo.Items.Add(item);
		}
		string text2 = text ?? _settings.MainFor(_game);
		if (!string.IsNullOrEmpty(text2) && MainCombo.Items.Contains(text2))
		{
			MainCombo.SelectedItem = text2;
		}
		BuildExtraMains();
		_loading = false;
		AppendLog($"[акков загружено: {list.Count}]");
	}

	private void OnRefresh(object sender, RoutedEventArgs e)
	{
		RefreshAccounts();
	}

	private int ThreadsCount()
	{
		if (!int.TryParse(ThreadsCombo.SelectedItem as string, out var result) || result < 1)
		{
			return 1;
		}
		return result;
	}

	private void OnThreadsChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_loading)
		{
			_settings.Threads = ThreadsCount();
			_settings.Save();
			BuildExtraMains();
			string text = Paths.NoMultiThreadExecutor(_settings.AutoexecDir);
			if (_settings.Threads >= 2 && text != null)
			{
				AppendLog($"[!] {text} не поддерживает мультипоток (2+). Для нескольких потоков нужен Potassium; на {text} ставь 1 поток.");
			}
		}
	}

	private void OnLightFpsChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_loading)
		{
			SaveToSettings();
		}
	}

	private void OnWinModeChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_loading)
		{
			SaveToSettings();
		}
	}

	private void BuildExtraMains()
	{
		if (ExtraMainsHost == null)
		{
			return;
		}
		ExtraMainsHost.Children.Clear();
		_extraMainCombos.Clear();
		int num = ThreadsCount();
		if (num <= 1)
		{
			return;
		}
		List<string> list = _store.Usernames();
		List<string> list2 = _settings.ExtraMainsFor(_game);
		for (int i = 2; i <= num; i++)
		{
			ExtraMainsHost.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = $"Главный #{i} (приёмник потока {i})",
				Foreground = Co.Dim,
				FontFamily = MonoFont,
				FontSize = 11.0,
				Margin = new Thickness(0.0, 6.0, 0.0, 4.0)
			});
			ComboBox comboBox = new ComboBox
			{
				Width = 240.0,
				HorizontalAlignment = HorizontalAlignment.Left
			};
			foreach (string item in list)
			{
				comboBox.Items.Add(item);
			}
			string text = ((i - 2 < list2.Count) ? list2[i - 2] : null);
			if (!string.IsNullOrEmpty(text) && comboBox.Items.Contains(text))
			{
				comboBox.SelectedItem = text;
			}
			comboBox.SelectionChanged += delegate
			{
				if (!_loading)
				{
					SaveExtraMains();
				}
			};
			_extraMainCombos.Add(comboBox);
			ExtraMainsHost.Children.Add(comboBox);
		}
	}

	private void SaveExtraMains()
	{
		_settings.ExtraMains[_game] = _extraMainCombos.Select((ComboBox c) => (c.SelectedItem as string) ?? "").ToList();
		_settings.Save();
	}

	private List<string> MainsForRun()
	{
		List<string> list = new List<string>();
		list.Add((MainCombo.SelectedItem as string) ?? "");
		list.AddRange(_extraMainCombos.Select((ComboBox c) => (c.SelectedItem as string) ?? ""));
		return list.Where((string s) => !string.IsNullOrWhiteSpace(s)).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
	}

	private void OnMetricUsd(object sender, RoutedEventArgs e)
	{
		_chartMetric = "usd";
		BuildAnalytics();
	}

	private void OnMetricItems(object sender, RoutedEventArgs e)
	{
		_chartMetric = "items";
		BuildAnalytics();
	}

	private void OnTopDaysChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_loading)
		{
			_topDays = (TopDaysCombo.SelectedItem as string) switch
			{
				"Сегодня" => 1, 
				"7 дней" => 7, 
				"30 дней" => 30, 
				_ => 0, 
			};
			if (AnalyticsView != null && AnalyticsView.Visibility == Visibility.Visible)
			{
				BuildAnalytics();
			}
		}
	}

	private void OnItemsBreakdown(object sender, MouseButtonEventArgs e)
	{
		BreakdownHost.Children.Clear();
		List<(string, int)> list = StatsStore.Load().Breakdown(_game);
		if (list.Count == 0)
		{
			BreakdownHost.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = "Пока пусто",
				Foreground = Co.Dim,
				FontSize = 12.0
			});
		}
		else
		{
			foreach (var item3 in list)
			{
				string item = item3.Item1;
				int item2 = item3.Item2;
				DockPanel dockPanel = new DockPanel
				{
					Margin = new Thickness(0.0, 3.0, 0.0, 3.0)
				};
				System.Windows.Controls.TextBlock element = new System.Windows.Controls.TextBlock
				{
					Text = item2.ToString(),
					Foreground = Co.Lime,
					FontSize = 12.0,
					FontWeight = FontWeights.SemiBold
				};
				DockPanel.SetDock(element, Dock.Right);
				dockPanel.Children.Add(element);
				dockPanel.Children.Add(new System.Windows.Controls.TextBlock
				{
					Text = item,
					Foreground = Co.Txt,
					FontSize = 12.0
				});
				BreakdownHost.Children.Add(dockPanel);
			}
		}
		BreakdownPopup.IsOpen = true;
	}

	private void BuildAnalytics()
	{
		StatsStore statsStore = StatsStore.Load();
		int num = statsStore.TotalItems(_game);
		double num2 = statsStore.TotalUsd(_game);
		TotalItemsLbl.Text = num.ToString();
		TotalUsdLbl.Text = $"{num2:0.00}$";
		AvgLbl.Text = ((num > 0) ? $"{num2 / (double)num:0.00}$" : "0.00$");
		bool usd = _chartMetric == "usd";
		ChartTitle.Text = (usd ? "Передано по дням, $" : "Предметов по дням");
		MetricUsdBtn.Appearance = ((!usd) ? ControlAppearance.Secondary : ControlAppearance.Primary);
		MetricItemsBtn.Appearance = (usd ? ControlAppearance.Secondary : ControlAppearance.Primary);
		ChartHost.Children.Clear();
		List<(DateTime, int, double)> list = statsStore.Daily(_game, 14);
		if (list.Count == 0)
		{
			ChartHost.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = "Пока нет данных — запусти сбор",
				Foreground = Co.Dim,
				FontSize = 14.0,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			});
		}
		else
		{
			double num3 = Math.Max(0.01, ((IEnumerable<(DateTime, int, double)>)list).Max((Func<(DateTime, int, double), double>)Val));
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Bottom,
				HorizontalAlignment = HorizontalAlignment.Left
			};
			foreach (var item5 in list)
			{
				double num4 = Val(item5);
				double val = 185.0 * (num4 / num3);
				StackPanel stackPanel2 = new StackPanel
				{
					Margin = new Thickness(7.0, 0.0, 7.0, 0.0),
					VerticalAlignment = VerticalAlignment.Bottom
				};
				stackPanel2.Children.Add(new System.Windows.Controls.TextBlock
				{
					Text = (usd ? $"{num4:0.##}$" : $"{(int)num4}"),
					Foreground = Co.Lime,
					FontSize = 10.0,
					FontWeight = FontWeights.SemiBold,
					HorizontalAlignment = HorizontalAlignment.Center,
					Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
				});
				stackPanel2.Children.Add(new Border
				{
					Width = 28.0,
					Height = Math.Max(3.0, val),
					CornerRadius = new CornerRadius(5.0, 5.0, 0.0, 0.0),
					Background = Co.Lime
				});
				stackPanel2.Children.Add(new System.Windows.Controls.TextBlock
				{
					Text = item5.Item1.ToString("dd.MM"),
					Foreground = Co.Dim,
					FontSize = 9.0,
					HorizontalAlignment = HorizontalAlignment.Center,
					Margin = new Thickness(0.0, 5.0, 0.0, 0.0)
				});
				stackPanel.Children.Add(stackPanel2);
			}
			ChartHost.Children.Add(stackPanel);
		}
		TopHost.Children.Clear();
		List<(string, double)> list2 = statsStore.TopItems(_game, 15, _topDays);
		if (list2.Count == 0)
		{
			TopHost.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = "Пока пусто",
				Foreground = Co.Dim,
				FontSize = 12.0
			});
		}
		else
		{
			int num5 = 1;
			foreach (var item6 in list2)
			{
				string item = item6.Item1;
				double item2 = item6.Item2;
				DockPanel dockPanel = new DockPanel
				{
					Margin = new Thickness(0.0, 3.0, 0.0, 3.0)
				};
				System.Windows.Controls.TextBlock element = new System.Windows.Controls.TextBlock
				{
					Text = $"{item2:0.##}$",
					Foreground = Co.Lime,
					FontSize = 12.0,
					FontWeight = FontWeights.SemiBold
				};
				DockPanel.SetDock(element, Dock.Right);
				dockPanel.Children.Add(element);
				dockPanel.Children.Add(new System.Windows.Controls.TextBlock
				{
					Text = $"{num5}. {item}",
					Foreground = Co.Txt,
					FontSize = 12.0,
					TextTrimming = TextTrimming.CharacterEllipsis
				});
				TopHost.Children.Add(dockPanel);
				num5++;
			}
		}
		AccHost.Children.Clear();
		List<(string, int, double)> list3 = statsStore.TopAccounts(_game, 12);
		if (list3.Count == 0)
		{
			AccHost.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = "Пока пусто",
				Foreground = Co.Dim,
				FontSize = 12.0
			});
			return;
		}
		int num6 = 1;
		foreach (var item7 in list3)
		{
			string item3 = item7.Item1;
			double item4 = item7.Item3;
			DockPanel dockPanel2 = new DockPanel
			{
				Margin = new Thickness(0.0, 3.0, 0.0, 3.0)
			};
			System.Windows.Controls.TextBlock element2 = new System.Windows.Controls.TextBlock
			{
				Text = $"{item4:0.##}$",
				Foreground = Co.Lime,
				FontSize = 12.0,
				FontWeight = FontWeights.SemiBold
			};
			DockPanel.SetDock(element2, Dock.Right);
			dockPanel2.Children.Add(element2);
			dockPanel2.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = $"{num6}. {item3}",
				Foreground = Co.Txt,
				FontSize = 12.0,
				TextTrimming = TextTrimming.CharacterEllipsis
			});
			AccHost.Children.Add(dockPanel2);
			num6++;
		}
		double Val((DateTime date, int items, double usd) d)
		{
			if (!usd)
			{
				return d.items;
			}
			return d.usd;
		}
	}

	private void OnAutoDetect(object sender, RoutedEventArgs e)
	{
		AutoexecBox.Text = Paths.DetectExecutorRoot();
		if (!_loading)
		{
			SaveToSettings();
		}
	}

	private void OnBrowseExecutor(object sender, RoutedEventArgs e)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = "Папка экзекьютора (внутри autoexec и workspace)"
		};
		try
		{
			if (Directory.Exists(AutoexecBox.Text))
			{
				openFolderDialog.InitialDirectory = AutoexecBox.Text;
			}
		}
		catch
		{
		}
		if (openFolderDialog.ShowDialog().GetValueOrDefault())
		{
			AutoexecBox.Text = openFolderDialog.FolderName;
			SaveToSettings();
		}
	}

	private void RefreshAccountsList()
	{
		if (AccountsList != null)
		{
			List<string> list = _store.Usernames();
			if (AccountsEmpty != null)
			{
				AccountsEmpty.Visibility = ((list.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
			}
			HashSet<string> doneMm2 = new HashSet<string>(Progress.Load("mm2").done, StringComparer.OrdinalIgnoreCase);
			HashSet<string> doneAdopt = new HashSet<string>(Progress.Load("adoptme").done, StringComparer.OrdinalIgnoreCase);
			AccountsList.ItemsSource = list.Select((string n) => new AccountRow
			{
				Name = n,
				DoneMm2 = doneMm2.Contains(n),
				DoneAdopt = doneAdopt.Contains(n)
			}).ToList();
		}
	}

	private void OnDeleteRow(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.Tag is string text && !string.IsNullOrEmpty(text))
		{
			_store.Remove(text);
			AppendLog("[удалён: " + text + "]");
			RefreshAccountsList();
			RefreshAccounts();
		}
	}

	private void OnCopyNick(object sender, MouseButtonEventArgs e)
	{
		System.Windows.Controls.TextBlock tb = sender as System.Windows.Controls.TextBlock;
		if (tb == null || !(tb.Tag is string text) || string.IsNullOrEmpty(text))
		{
			return;
		}
		e.Handled = true;
		try
		{
			Clipboard.SetDataObject(text, copy: true);
		}
		catch
		{
			try
			{
				Clipboard.SetText(text);
			}
			catch
			{
			}
		}
		tb.Text = "✓";
		tb.Foreground = LogGreen;
		DispatcherTimer timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(1100.0)
		};
		timer.Tick += delegate
		{
			timer.Stop();
			tb.Text = "⧉";
			tb.Foreground = Co.Dim;
		};
		timer.Start();
	}

	private void OnRemoveMm2Status(object sender, MouseButtonEventArgs e)
	{
		RemoveStatusRow(sender, "mm2", "MM2");
	}

	private void OnRemoveAdoptStatus(object sender, MouseButtonEventArgs e)
	{
		RemoveStatusRow(sender, "adoptme", "Adopt");
	}

	private void RemoveStatusRow(object sender, string game, string label)
	{
		if ((sender as FrameworkElement)?.Tag is string text && !string.IsNullOrEmpty(text))
		{
			Progress.ClearStatus(game, text);
			AppendLog($"[снят статус «сдал {label}»: {text}]");
			RefreshAccountsList();
		}
	}

	private void OnRemoveDone(object sender, RoutedEventArgs e)
	{
		HashSet<string> mains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string value in _settings.Main.Values)
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				mains.Add(value);
			}
		}
		foreach (List<string> value2 in _settings.ExtraMains.Values)
		{
			foreach (string item in value2)
			{
				if (!string.IsNullOrWhiteSpace(item))
				{
					mains.Add(item);
				}
			}
		}
		foreach (string item2 in MainsForRun())
		{
			mains.Add(item2);
		}
		HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		(string, string)[] statusGames = StatusGames;
		for (int i = 0; i < statusGames.Length; i++)
		{
			foreach (string item3 in Progress.Load(statusGames[i].Item1).done)
			{
				done.Add(item3);
			}
		}
		List<string> list = (from n in _store.Usernames()
			where done.Contains(n) && !mains.Contains(n)
			select n).ToList();
		if (list.Count == 0)
		{
			AppendLog("[нет сданных акков для удаления (или это главные)]");
		}
		else if (System.Windows.MessageBox.Show($"Удалить {list.Count} сданных акков из списка? (главные не трогаются)", "Убрать сданные", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == System.Windows.MessageBoxResult.Yes)
		{
			_store.RemoveMany(list);
			statusGames = StatusGames;
			for (int i = 0; i < statusGames.Length; i++)
			{
				Progress.ClearStatuses(statusGames[i].Item1, list);
			}
			AppendLog($"[убрано {list.Count} сданных акков]");
			RefreshAccountsList();
			RefreshAccounts();
		}
	}

	private void AppendLog(string s)
	{
		if (!base.Dispatcher.CheckAccess())
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				AppendLog(s);
			});
			return;
		}
		Log.Document.Blocks.Add(new Paragraph(new Run(s)
		{
			Foreground = LogColor(s)
		})
		{
			Margin = new Thickness(0.0)
		});
		Log.ScrollToEnd();
	}

	private static Brush LogColor(string s)
	{
		string text = s.ToLowerInvariant();
		if (s.Contains("✅") || text.Contains("сдал") || text.Contains("получил") || text.Contains("добавлено") || text.Contains("импортирован") || text.Contains("готов"))
		{
			return LogGreen;
		}
		if (s.Contains("❌") || s.Contains("⛔") || text.Contains("ошибка") || text.Contains("провал") || text.Contains("не удалось") || text.Contains("не задан") || text.Contains("не попал"))
		{
			return LogRed;
		}
		if (s.Contains("⚠") || s.Contains("\ud83d\udd01") || s.Contains("\ud83d\udd1e") || s.Contains("\ud83d\udced") || text.Contains("пуст") || text.Contains("ретрай") || text.Contains("пропус") || text.Contains("останов") || text.Contains("реджоин"))
		{
			return LogYellow;
		}
		return LogDim;
	}

	private void OnStart(object sender, RoutedEventArgs e)
	{
		Run("run");
	}

	private void OnDistribute(object sender, RoutedEventArgs e)
	{
		Run("distribute");
	}

	private void Run(string cmd)
	{
		Task task = _task;
		if (task != null && !task.IsCompleted)
		{
			return;
		}
		if (string.IsNullOrEmpty(MainCombo.SelectedItem as string))
		{
			AppendLog("[!] выбери главного в Settings (sidebar) → Обновить");
			return;
		}
		SaveToSettings();
		try
		{
			LuaDeployer.Deploy(Games.Get(_game).Autoexec, _settings.AutoexecDir);
			AppendLog("[autoexec: " + Games.Get(_game).Autoexec + "]");
		}
		catch (Exception ex)
		{
			AppendLog("(не удалось поставить autoexec: " + ex.Message + ")");
		}
		int threads = ThreadsCount();
		string text = Paths.NoMultiThreadExecutor(_settings.AutoexecDir);
		if (cmd == "run" && threads >= 2 && text != null)
		{
			AppendLog("[!] " + text + " не поддерживает мультипоток (2+ потока) — запуск отменён. Поставь 1 поток. Для нескольких потоков нужен Potassium.");
			return;
		}
		List<string> mains = MainsForRun();
		if (cmd == "run" && threads >= 2 && mains.Count < threads)
		{
			AppendLog($"[!] Потоков {threads}, но выбрано главных: {mains.Count} — пойдёт {Math.Max(1, mains.Count)}. Доп. главных выбери в Settings.");
		}
		AppendLog($"\n──────── {cmd} ({_game}{((cmd == "run" && threads >= 2) ? $", {threads} потока" : "")}) ────────");
		SetRunning(running: true);
		Reporter.ReportAsync("running");
		_cts = new CancellationTokenSource();
		GameDef game = Games.Get(_game);
		Settings st = _settings;
		AccountStore store = _store;
		CancellationToken tok = _cts.Token;
		_task = Task.Run(delegate
		{
			try
			{
				if (cmd == "distribute")
				{
					new Orchestrator(game, st, store, AppendLog, tok).Distribute();
				}
				else if (threads >= 2 && mains.Count >= 2)
				{
					Orchestrator.SerializeSpawnFocus = true;
					Orchestrator.RunStreams(game, st, store, AppendLog, tok, mains, consolidate: true);
				}
				else
				{
					Orchestrator.SerializeSpawnFocus = false;
					new Orchestrator(game, st, store, AppendLog, tok, null, null, null, null, skipClean: false, keepMainAlive: false, mainAlreadyUp: false, reportFullInventory: true).Run();
				}
			}
			catch (OperationCanceledException)
			{
				AppendLog("[остановлено]");
			}
			catch (Exception ex3)
			{
				AppendLog("ОШИБКА: " + ex3.Message);
			}
			finally
			{
				Reporter.ReportAsync();
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					SetRunning(running: false);
				});
			}
		});
	}

	private void OnStop(object sender, RoutedEventArgs e)
	{
		_cts?.Cancel();
		AppendLog("[останавливаю…]");
	}

	private void SetRunning(bool running)
	{
		Control[] array = new Control[5] { CookiesBtn, DistBtn, StartBtn, GameCombo, MainCombo };
		for (int i = 0; i < array.Length; i++)
		{
			array[i].IsEnabled = !running;
		}
		foreach (ToggleButton item in _weap.Values.Concat(_pet.Values).Concat(_adoptCat.Values))
		{
			item.IsEnabled = !running;
		}
		StopBtn.IsEnabled = running;
		Ring.Visibility = ((!running) ? Visibility.Collapsed : Visibility.Visible);
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

	private static void DarkTitleBar(Window w)
	{
		w.SourceInitialized += delegate
		{
			try
			{
				nint handle = new WindowInteropHelper(w).Handle;
				int value = 1;
				DwmSetWindowAttribute(handle, 20, ref value, 4);
			}
			catch
			{
			}
		};
	}

	private void OnCookies(object sender, RoutedEventArgs e)
	{
		Window win = new Window
		{
			Title = "Загрузить куки",
			Width = 680.0,
			Height = 460.0,
			Owner = this,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = (Brush)FindResource("Sidebar")
		};
		Grid grid = new Grid
		{
			Margin = new Thickness(14.0)
		};
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.Children.Add(new System.Windows.Controls.TextBlock
		{
			Text = "Куки .ROBLOSECURITY — по одной на строку:",
			Foreground = Co.Txt,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		});
		System.Windows.Controls.TextBox box = new System.Windows.Controls.TextBox
		{
			AcceptsReturn = true,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			FontFamily = new FontFamily("Consolas"),
			Foreground = Co.Txt,
			Background = (Brush)FindResource("LogBg")
		};
		Grid.SetRow(box, 1);
		grid.Children.Add(box);
		Wpf.Ui.Controls.Button button = new Wpf.Ui.Controls.Button
		{
			Content = "Импортировать",
			Appearance = ControlAppearance.Secondary,
			Background = Co.Lime,
			Foreground = Brushes.Black,
			BorderBrush = Co.Lime,
			FontWeight = FontWeights.Bold,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0),
			HorizontalAlignment = HorizontalAlignment.Right,
			Padding = new Thickness(22.0, 8.0, 22.0, 8.0)
		};
		Grid.SetRow(button, 2);
		button.Click += delegate
		{
			List<string> list = (from l in box.Text.Split('\n')
				select l.Trim() into l
				where l.Length > 0
				select l).ToList();
			win.Close();
			if (list.Count > 0)
			{
				ImportCookies(list);
			}
		};
		grid.Children.Add(button);
		win.Content = grid;
		DarkTitleBar(win);
		win.ShowDialog();
	}

	private void ImportCookies(List<string> cookies)
	{
		AppendLog($"\n=== импорт {cookies.Count} кук ===");
		Task.Run(async delegate
		{
			if (cookies.Count > 100)
			{
				AppendLog("[много кук — добавляю с паузами, чтобы Roblox не выдал рейт-лимит (429). Это дольше, но без потерь.]");
			}
			int before = _store.Usernames().Count;
			int i = 0;
			foreach (string c in cookies)
			{
				i++;
				try
				{
					RobloxLauncher.AccountInfo accountInfo = await RobloxLauncher.GetAccountInfoAsync(c);
					_store.AddOrUpdate(accountInfo.Name, accountInfo.UserId, c);
					AppendLog($"  [{i}/{cookies.Count}] импортирован: {accountInfo.Name}");
				}
				catch (Exception ex)
				{
					AppendLog($"  [{i}/{cookies.Count}] ошибка: {ex.Message}");
				}
				await Task.Delay(120);
			}
			int count = _store.Usernames().Count;
			AppendLog($"[готово: добавлено {count - before} новых акков (всего: {count})]");
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				RefreshAccounts();
				RefreshAccountsList();
			});
		});
	}

	private void OnResetStatuses(object sender, RoutedEventArgs e)
	{
		Progress.ClearAll("mm2");
		Progress.ClearAll("adoptme");
		AppendLog("[статусы «сдал» сброшены у всех аккаунтов (MM2 и Adopt)]");
		RefreshAccountsList();
	}

	private void OnRemoveExceptMain(object sender, RoutedEventArgs e)
	{
		HashSet<string> mains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string value2 in _settings.Main.Values)
		{
			if (!string.IsNullOrWhiteSpace(value2))
			{
				mains.Add(value2);
			}
		}
		foreach (List<string> value3 in _settings.ExtraMains.Values)
		{
			foreach (string item in value3)
			{
				if (!string.IsNullOrWhiteSpace(item))
				{
					mains.Add(item);
				}
			}
		}
		foreach (string item2 in MainsForRun())
		{
			mains.Add(item2);
		}
		List<string> list = (from n in _store.Usernames()
			where !mains.Contains(n)
			select n).ToList();
		if (list.Count == 0)
		{
			AppendLog("[нечего убирать — остались только главные]");
			return;
		}
		string value = ((mains.Count > 0) ? string.Join(", ", mains) : "(главный не задан!)");
		if (System.Windows.MessageBox.Show($"Удалить {list.Count} акков? Останутся главные (включая доп. для потоков): {value}", "Убрать всех, кроме главного", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != System.Windows.MessageBoxResult.Yes)
		{
			return;
		}
		foreach (string item3 in list)
		{
			_store.Remove(item3);
		}
		AppendLog($"[убрано {list.Count} акков, оставлены главные: {value}]");
		RefreshAccountsList();
		RefreshAccounts();
	}

	private void OnDelete(object sender, RoutedEventArgs e)
	{
		Window window = new Window
		{
			Title = "Удалить акки",
			Width = 360.0,
			Height = 480.0,
			Owner = this,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = (Brush)FindResource("Sidebar")
		};
		Grid grid = new Grid
		{
			Margin = new Thickness(14.0)
		};
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		ListBox list = new ListBox
		{
			Background = (Brush)FindResource("LogBg"),
			Foreground = Co.Txt
		};
		foreach (string item in _store.Usernames())
		{
			list.Items.Add(item);
		}
		grid.Children.Add(list);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		Grid.SetRow(stackPanel, 1);
		Wpf.Ui.Controls.Button button = new Wpf.Ui.Controls.Button
		{
			Content = "Удалить выбранный",
			Appearance = ControlAppearance.Danger,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Padding = new Thickness(14.0, 8.0, 14.0, 8.0)
		};
		button.Click += delegate
		{
			if (list.SelectedItem is string text)
			{
				_store.Remove(text);
				list.Items.Remove(text);
				AppendLog("[удалён: " + text + "]");
				RefreshAccounts();
			}
		};
		Wpf.Ui.Controls.Button button2 = new Wpf.Ui.Controls.Button
		{
			Content = "Удалить ВСЕ",
			Appearance = ControlAppearance.Danger,
			Padding = new Thickness(14.0, 8.0, 14.0, 8.0)
		};
		button2.Click += delegate
		{
			foreach (string item2 in _store.Usernames().ToList())
			{
				_store.Remove(item2);
			}
			list.Items.Clear();
			AppendLog("[удалены все акки]");
			RefreshAccounts();
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		grid.Children.Add(stackPanel);
		window.Content = grid;
		window.ShowDialog();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.28.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/LuxLooter;component/ui/mainwindow.xaml", UriKind.Relative);
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
			VersionLbl = (System.Windows.Controls.TextBlock)target;
			break;
		case 2:
			NavBottom = (StackPanel)target;
			break;
		case 3:
			NavTop = (StackPanel)target;
			break;
		case 4:
			HeaderTitle = (System.Windows.Controls.TextBlock)target;
			break;
		case 5:
			GameCombo = (ComboBox)target;
			GameCombo.SelectionChanged += OnGameChanged;
			break;
		case 6:
			BellBtn = (System.Windows.Controls.TextBlock)target;
			BellBtn.MouseLeftButtonUp += OnBell;
			break;
		case 7:
			ProfileBtn = (System.Windows.Controls.TextBlock)target;
			ProfileBtn.MouseLeftButtonUp += OnProfile;
			break;
		case 8:
			ProfilePopup = (Popup)target;
			break;
		case 9:
			ProfKey = (System.Windows.Controls.TextBlock)target;
			break;
		case 10:
			ProfDays = (System.Windows.Controls.TextBlock)target;
			break;
		case 11:
			ProfExp = (System.Windows.Controls.TextBlock)target;
			break;
		case 12:
			BellPopup = (Popup)target;
			break;
		case 13:
			BellText = (System.Windows.Controls.TextBlock)target;
			break;
		case 14:
			UpdateBtn = (System.Windows.Controls.Button)target;
			UpdateBtn.Click += OnUpdateNow;
			break;
		case 15:
			Body = (Grid)target;
			break;
		case 16:
			DashView = (Grid)target;
			break;
		case 17:
			FilterTitle = (System.Windows.Controls.TextBlock)target;
			break;
		case 18:
			FilterHost = (StackPanel)target;
			break;
		case 19:
			Log = (System.Windows.Controls.RichTextBox)target;
			break;
		case 20:
			Ring = (ProgressRing)target;
			break;
		case 21:
			StopBtn = (System.Windows.Controls.Button)target;
			StopBtn.Click += OnStop;
			break;
		case 22:
			StartBtn = (System.Windows.Controls.Button)target;
			StartBtn.Click += OnStart;
			break;
		case 23:
			SettingsView = (Grid)target;
			break;
		case 24:
			MainCombo = (ComboBox)target;
			break;
		case 25:
			((Wpf.Ui.Controls.Button)target).Click += OnRefresh;
			break;
		case 26:
			AutoexecBox = (Wpf.Ui.Controls.TextBox)target;
			break;
		case 27:
			((Wpf.Ui.Controls.Button)target).Click += OnBrowseExecutor;
			break;
		case 28:
			((Wpf.Ui.Controls.Button)target).Click += OnAutoDetect;
			break;
		case 29:
			ThreadsCombo = (ComboBox)target;
			ThreadsCombo.SelectionChanged += OnThreadsChanged;
			break;
		case 30:
			ExtraMainsHost = (StackPanel)target;
			break;
		case 31:
			LightFpsCombo = (ComboBox)target;
			LightFpsCombo.SelectionChanged += OnLightFpsChanged;
			break;
		case 32:
			WinModeCombo = (ComboBox)target;
			WinModeCombo.SelectionChanged += OnWinModeChanged;
			break;
		case 33:
			AccountsView = (Grid)target;
			break;
		case 34:
			CookiesBtn = (Wpf.Ui.Controls.Button)target;
			CookiesBtn.Click += OnCookies;
			break;
		case 35:
			DistBtn = (Wpf.Ui.Controls.Button)target;
			DistBtn.Click += OnDistribute;
			break;
		case 36:
			ResetStatusBtn = (Wpf.Ui.Controls.Button)target;
			ResetStatusBtn.Click += OnResetStatuses;
			break;
		case 37:
			RemoveDoneBtn = (Wpf.Ui.Controls.Button)target;
			RemoveDoneBtn.Click += OnRemoveDone;
			break;
		case 38:
			ClearExceptMainBtn = (Wpf.Ui.Controls.Button)target;
			ClearExceptMainBtn.Click += OnRemoveExceptMain;
			break;
		case 39:
			AccountsList = (ItemsControl)target;
			break;
		case 44:
			AccountsEmpty = (System.Windows.Controls.TextBlock)target;
			break;
		case 45:
			AnalyticsView = (Grid)target;
			break;
		case 46:
			ItemsCard = (Border)target;
			ItemsCard.MouseLeftButtonUp += OnItemsBreakdown;
			break;
		case 47:
			TotalItemsLbl = (System.Windows.Controls.TextBlock)target;
			break;
		case 48:
			TotalUsdLbl = (System.Windows.Controls.TextBlock)target;
			break;
		case 49:
			AvgLbl = (System.Windows.Controls.TextBlock)target;
			break;
		case 50:
			BreakdownPopup = (Popup)target;
			break;
		case 51:
			BreakdownHost = (StackPanel)target;
			break;
		case 52:
			ChartTitle = (System.Windows.Controls.TextBlock)target;
			break;
		case 53:
			MetricUsdBtn = (Wpf.Ui.Controls.Button)target;
			MetricUsdBtn.Click += OnMetricUsd;
			break;
		case 54:
			MetricItemsBtn = (Wpf.Ui.Controls.Button)target;
			MetricItemsBtn.Click += OnMetricItems;
			break;
		case 55:
			ChartHost = (Grid)target;
			break;
		case 56:
			TopDaysCombo = (ComboBox)target;
			TopDaysCombo.SelectionChanged += OnTopDaysChanged;
			break;
		case 57:
			TopHost = (StackPanel)target;
			break;
		case 58:
			AccHost = (StackPanel)target;
			break;
		case 59:
			AdminView = (Grid)target;
			break;
		case 60:
			AdminRefreshBtn = (Wpf.Ui.Controls.Button)target;
			AdminRefreshBtn.Click += OnAdminRefresh;
			break;
		case 61:
			AdmSpinner = (ProgressRing)target;
			break;
		case 62:
			AdmClients = (System.Windows.Controls.TextBlock)target;
			break;
		case 63:
			AdmOnline = (System.Windows.Controls.TextBlock)target;
			break;
		case 64:
			AdmItems = (System.Windows.Controls.TextBlock)target;
			break;
		case 65:
			AdmUsd = (System.Windows.Controls.TextBlock)target;
			break;
		case 66:
			AdmGames = (System.Windows.Controls.TextBlock)target;
			break;
		case 67:
			AdminList = (StackPanel)target;
			break;
		case 68:
			HelpView = (Grid)target;
			break;
		case 69:
			HelpScroll = (ScrollViewer)target;
			break;
		case 70:
			HelpContent = (StackPanel)target;
			break;
		case 71:
			ChangelogView = (Grid)target;
			break;
		case 72:
			ChangelogScroll = (ScrollViewer)target;
			break;
		case 73:
			ChangelogContent = (StackPanel)target;
			break;
		case 74:
			StubView = (Grid)target;
			break;
		case 75:
			StubText = (System.Windows.Controls.TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.28.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 40:
			((Wpf.Ui.Controls.Button)target).Click += OnDeleteRow;
			break;
		case 41:
			((Border)target).MouseLeftButtonUp += OnRemoveAdoptStatus;
			break;
		case 42:
			((Border)target).MouseLeftButtonUp += OnRemoveMm2Status;
			break;
		case 43:
			((System.Windows.Controls.TextBlock)target).MouseLeftButtonUp += OnCopyNick;
			break;
		}
	}
}
