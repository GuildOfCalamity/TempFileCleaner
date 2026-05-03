using System;
using System.Diagnostics;
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

    /// <summary><code>
    ///  // 1. Synchronous timing
    ///  var elapsedSync = MethodTimer.Time(() => Thread.Sleep(100));
    ///  Debug.WriteLine($"Sync operation took: {elapsedSync.TotalMilliseconds}ms");
    ///  
    ///  // 2. Asynchronous timing
    ///  var elapsedAsync = await MethodTimer.TimeAsync(async () => await Task.Delay(100));
    ///  Debug.WriteLine($"Async operation took: {elapsedAsync.TotalMilliseconds}ms");
    ///
    ///  // 3. Timing with a return value
    ///  var(data, time) = await MethodTimer.TimeAsync(async () => {
    ///        await Task.Delay(50);
    ///        return "Operation Complete";
    ///  });
    ///  Debug.WriteLine($"Result: {data} (Took: {time.TotalMilliseconds}ms)");
    /// </code></summary>
    public static class MethodTimerAsync
    {
        /// <summary>
        /// Times a synchronous action.
        /// </summary>
        public static TimeSpan Time(Action action)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            return sw.Elapsed;
        }

        /// <summary>
        /// Times a synchronous function that returns a value.
        /// </summary>
        public static (T Result, TimeSpan Elapsed) Time<T>(Func<T> func)
        {
            var sw = Stopwatch.StartNew();
            var result = func();
            sw.Stop();
            return (result, sw.Elapsed);
        }

        /// <summary>
        /// Times an asynchronous task.
        /// </summary>
        public static async Task<TimeSpan> TimeAsync(Func<Task> taskFunc)
        {
            var sw = Stopwatch.StartNew();
            await taskFunc();
            sw.Stop();
            return sw.Elapsed;
        }

        /// <summary>
        /// Times an asynchronous task that returns a value.
        /// </summary>
        public static async Task<(T Result, TimeSpan Elapsed)> TimeAsync<T>(Func<Task<T>> taskFunc)
        {
            var sw = Stopwatch.StartNew();
            var result = await taskFunc();
            sw.Stop();
            return (result, sw.Elapsed);
        }
    }

}