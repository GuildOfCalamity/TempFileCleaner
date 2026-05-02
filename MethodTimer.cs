using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace TempFileCleaner
{
    /// <summary>
    /// <code>
    /// Timing a standard method:
    /// MethodTimer.Time(() => SomeHeavyWork(), "ProcessData");
    /// 
    /// Timing a method with a return value:
    /// var result = MethodTimer.Time(() => CalculateTotal(), "CalculateTotal");
    /// 
    /// Timing an async operation (Most common in WPF):
    /// await MethodTimer.TimeAsync(async () => {
    ///     await Task.Delay(500); // Simulate work
    ///     UpdateUI();
    /// }, "AsyncUpdate");
    /// </code>
    /// </summary>
    public static class MethodTimer
    {
        // You could point this to a debugger, logger, status bar, or a file.
        public static Action<string> LogHandler { get; set; } = (message) => Debug.WriteLine(message);

        /// <summary>
        /// Times a void method.
        /// </summary>
        public static void Time(Action action, string methodName)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                action();
            }
            finally
            {
                sw.Stop();
                Report(methodName, sw.Elapsed);
            }
        }

        /// <summary>
        /// Times a method that returns a value.
        /// </summary>
        public static T Time<T>(Func<T> func, string methodName)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                return func();
            }
            finally
            {
                sw.Stop();
                Report(methodName, sw.Elapsed);
            }
        }

        /// <summary>
        /// Times an asynchronous Task.
        /// </summary>
        public static async Task TimeAsync(Func<Task> taskFunc, string methodName)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await taskFunc();
            }
            finally
            {
                sw.Stop();
                Report(methodName, sw.Elapsed);
            }
        }

        /// <summary>
        /// Times an asynchronous Task that returns a value.
        /// </summary>
        public static async Task<T> TimeAsync<T>(Func<Task<T>> taskFunc, string methodName)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                return await taskFunc();
            }
            finally
            {
                sw.Stop();
                Report(methodName, sw.Elapsed);
            }
        }

        static void Report(string methodName, TimeSpan elapsed)
        {
            // Formats output like: [TIMER] LoadSettings took 42ms
            LogHandler?.Invoke($"{methodName} took {elapsed.TotalMilliseconds:N2}ms");
        }
    }
}