using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace TempFileCleaner
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool DebugMode { get; set; } = false;
        public static bool DemoMode { get; set; } = false;

        /// <summary>
        /// Any outside calling threads must use the <see cref="App.SyncContext"/>
        /// or the <see cref="App.UiContext"/> when updating notifiable properties 
        /// from inside the view models.
        /// </summary>
        public static TaskScheduler SyncContext { get; private set; }
        public static SynchronizationContext UiContext { get; private set; }
        public static Dispatcher MainDispatcher { get; private set; }
        public static Version Version { get; private set; } = System.Reflection.Assembly.GetExecutingAssembly()?.GetName()?.Version ?? new Version();
        public static string Title { get; private set; } = System.Reflection.Assembly.GetExecutingAssembly()?.GetName()?.Name ?? "App";

        /// <summary>
        /// WPF entry with <see cref="System.Windows.StartupEventArgs"/>.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomainFirstChanceException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            var args = e.Args.ToList();
            DebugMode = args.Any(a => a.Equals("-debug", StringComparison.InvariantCultureIgnoreCase) || a.Equals("debug", StringComparison.InvariantCultureIgnoreCase));

            base.OnStartup(e);

            UiContext = SynchronizationContext.Current;
            SyncContext = TaskScheduler.FromCurrentSynchronizationContext();
            MainDispatcher = Dispatcher.CurrentDispatcher;
        }

        #region [Domain Events]
        void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                Debug.WriteLine($"Unhandled exception thrown from Dispatcher {e.Dispatcher}: {e.Exception}");
                Debug.WriteLine($"Unhandled exception StackTrace: {Environment.StackTrace}");
                e.Handled = true;
            }
            catch { }
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Debug.WriteLine($"Unhandled exception thrown: {((Exception)e.ExceptionObject).Message}");
            }
            catch { }
        }

        void CurrentDomainFirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            if (e?.Exception != null && (e?.Exception.GetType() == typeof(SocketException) || (bool)e?.Exception?.Message.Contains("System.Net.Sockets.Socket")))
            {
                Debug.WriteLine($"[SocketException] {e?.Exception?.Message}");
            }
            else
            {
                if ((bool)e?.Exception?.Message?.Contains($"{GetCurrentNamespace()}.XmlSerializers"))
                {
                    // Ignore the fake System.Xml.Serialization warning.
                    Debug.WriteLine($"[INFO] AppDomain is looking for \"{GetCurrentNamespace()}.XmlSerializers\".");
                }
                else
                {
                    Debug.WriteLine($"First chance exception from {sender?.GetType()}: {e?.Exception?.Message}");
                    if (e?.Exception?.InnerException != null)
                        Debug.WriteLine($"InnerException: {e?.Exception?.InnerException.Message}");
                }
            }
        }

        void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e?.Exception is AggregateException aex)
            {
                aex?.Flatten().Handle(ex =>
                {
                    Debug.WriteLine($"[WARNING] Unobserved task exception: {ex?.Message}");
                    return true;
                });
            }
            e?.SetObserved(); // suppress and handle manually
        }
        #endregion

        #region [Reflection Helpers]
        public static string GetCurrentNamespace() => System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace ?? "ClubAccessApp";

        public static string GetCurrentFullName() => System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Assembly?.FullName ?? "ClubAccessApp";

        public static string GetCurrentAssemblyName() => System.Reflection.Assembly.GetExecutingAssembly()?.GetName()?.Name ?? "ClubAccessApp";

        public static Version GetCurrentAssemblyVersion() => System.Reflection.Assembly.GetExecutingAssembly()?.GetName()?.Version ?? new Version();
        #endregion

        /// <summary>
        /// Asynchronously marshals a delegate to the UI thread using the captured
        /// <see cref="SynchronizationContext"/> from <see cref="App.UiContext"/>.
        /// </summary>
        public static void PostOnUiThread(Action action, Action onException = null)
        {
            App.UiContext?.Post(_ =>
            {
                try { action(); }
                catch (Exception) { onException?.Invoke(); }

            }, null);
        }

        /// <summary>
        /// Synchronously marshals a delegate to the UI thread using the captured 
        /// <see cref="SynchronizationContext"/> from <see cref="App.UiContext"/>.
        /// </summary>
        public static void SendOnUiThread(Action action, Action onException = null)
        {
            App.UiContext?.Send(_ =>
            {
                try { action(); }
                catch (Exception) { onException?.Invoke(); }

            }, null);
        }
    }
}
