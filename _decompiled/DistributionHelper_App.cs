using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DistributionHelper.Models;
using DistributionHelper.Services;

namespace DistributionHelper;

public class App : Application
{
	[Serializable]
	[CompilerGenerated]
	private sealed class _003C_003Ec
	{
		public static readonly _003C_003Ec _003C_003E9 = new _003C_003Ec();

		public static DispatcherUnhandledExceptionEventHandler _003C_003E9__5_1;

		internal void _003COnStartup_003Eb__5_1(object _, DispatcherUnhandledExceptionEventArgs args)
		{
			ErrorLog.Write(args.Exception);
			MessageBox.Show("DistroClip hit an unexpected problem. A log was saved under Local AppData\\DistroClip.", "DistroClip", MessageBoxButton.OK, MessageBoxImage.Hand);
			args.Handled = true;
		}
	}

	[Serializable]
	[CompilerGenerated]
	private sealed class <>c
	{
		public static readonly <>c <>9 = new <>c();

		public static DispatcherUnhandledExceptionEventHandler <>9__7_2;

		internal void <OnStartup>b__7_2(object _, DispatcherUnhandledExceptionEventArgs args)
		{
			ErrorLog.Write(args.Exception);
			MessageBox.Show("DistroClip hit an unexpected problem. A log was saved under Local AppData\\DistroClip.", "DistroClip", MessageBoxButton.OK, MessageBoxImage.Hand);
			args.Handled = true;
		}
	}

	private const string MutexName = "Local\\Gahara.DistroClip.Singleton";

	private const string ToggleEventName = "Local\\Gahara.DistroClip.Toggle";

	private Mutex? _singleInstanceMutex;

	private EventWaitHandle? _toggleEvent;

	private bool _ownsMutex;

	private bool _contentLoaded;

	protected override void OnStartup(StartupEventArgs e)
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Expected O, but got Unknown
		base.OnStartup(e);
		_toggleEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, "Local\\Gahara.DistroClip.Toggle");
		_singleInstanceMutex = new Mutex(initiallyOwned: true, "Local\\Gahara.DistroClip.Singleton", out var createdNew);
		_ownsMutex = createdNew;
		if (!createdNew)
		{
			_toggleEvent.Set();
			Shutdown();
			return;
		}
		Task.Run(delegate
		{
			_toggleEvent.WaitOne();
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				Shutdown();
			}, Array.Empty<object>());
		});
		object obj = _003C_003Ec._003C_003E9__5_1;
		if (obj == null)
		{
			object obj2 = <>c.<>9__7_2;
			if (obj2 == null)
			{
				DispatcherUnhandledExceptionEventHandler val = delegate(object _, DispatcherUnhandledExceptionEventArgs args)
				{
					ErrorLog.Write(args.Exception);
					MessageBox.Show("DistroClip hit an unexpected problem. A log was saved under Local AppData\\DistroClip.", "DistroClip", MessageBoxButton.OK, MessageBoxImage.Hand);
					args.Handled = true;
				};
				<>c.<>9__7_2 = val;
				obj2 = (object)val;
			}
			_003C_003Ec._003C_003E9__5_1 = (DispatcherUnhandledExceptionEventHandler)obj2;
			obj = obj2;
		}
		base.DispatcherUnhandledException += (DispatcherUnhandledExceptionEventHandler)obj;
		SettingsService settingsService = new SettingsService();
		AppSettings appSettings = settingsService.Load();
		GelInteraction.Attach();
		ThemeManager.Apply(ThemeCatalog.Parse(appSettings.Theme));
		Window window = (base.MainWindow = new MainWindow(settingsService, appSettings));
		((MainWindow)window).Show();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		try
		{
			_toggleEvent?.Set();
			if (_ownsMutex)
			{
				_singleInstanceMutex?.ReleaseMutex();
			}
		}
		catch
		{
		}
		finally
		{
			_toggleEvent?.Dispose();
			_singleInstanceMutex?.Dispose();
		}
		base.OnExit(e);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/DistroClip;component/app.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public static void Main()
	{
		App app = new App();
		app.InitializeComponent();
		app.Run();
	}
}
