using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TempFileCleaner
{
    /// <summary><code>
    /// Thread-safe extension for BCL <see cref="CancellationTokenSource"/> with additional 
    /// functionality of exposing a <see cref="TimeRemaining"/> property 
    /// so a token can be re-evaluated during runtime.
    /// 
    /// This enhanced version offers the following:
    /// - [Atomic Updates] 
    ///     The _expirationTime and the _cts.CancelAfter call happen together. 
    ///     This prevents race conditions where the UI could show n minutes left 
    ///     but the token has actually already expired (or vice versa).
    /// - [Zero-Floor Logic] 
    ///     TimeRemaining will never return a negative value if the system clock 
    ///     drifts slightly; it will simply return TimeSpan.Zero.
    /// - [State Consistency] 
    ///     If Cancel() is called manually, we force the expiration time to UtcNow 
    ///     so the UI countdown immediately hits zero.
    /// - [CancellationTokenRegistration Support] 
    ///     The <see cref="TimerExpired"/> event can be used to register a 
    ///     callback that will be invoked when the token is canceled.
    /// </code></summary>
    public class CancellationTokenSourceEnhanced : IDisposable
    {
        DateTime? _expirationTime;
        bool _isDisposed = false;
        readonly double _millisecondMargin = 20;
        readonly object _lock = new object();
        readonly CancellationTokenSource _cts;
        CancellationTokenRegistration _registration;
        // NOTE: CancellationTokenRegistration objects can stay in memory as long 
        //       as the CancellationTokenSource exists unless explicitly disposed.
        //       We're storing this registration for later disposal.
        public event EventHandler TimerExpired;

        public CancellationTokenSourceEnhanced()
        {
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Additional constructor for <see cref="TimeSpan"/> compatibility.
        /// </summary>
        public CancellationTokenSourceEnhanced(TimeSpan timeout)
        {
            _cts = new CancellationTokenSource(timeout);
            _expirationTime = DateTime.UtcNow.Add(timeout);
            if (_expirationTime != DateTime.MinValue && _expirationTime != DateTime.MaxValue)
            {
                RegisterExpirationCallback();
            }
        }

        /// <summary>
        /// Additional constructor for millisecond compatibility
        /// </summary>
        public CancellationTokenSourceEnhanced(int millisecondsDelay)
        {
            if (millisecondsDelay < -1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsDelay), "Milliseconds delay must be -1 (infinite) or non-negative.");

            if (millisecondsDelay == Timeout.Infinite) // -1
            {
                _cts = new CancellationTokenSource();
                _expirationTime = DateTime.MaxValue; // Infinite means no expiration
            }
            else
            {
                var timeout = TimeSpan.FromMilliseconds(millisecondsDelay);
                _cts = new CancellationTokenSource(timeout);
                _expirationTime = DateTime.UtcNow.Add(timeout);
                RegisterExpirationCallback();
            }
        }

        /// <summary>
        /// Additional constructor for supporting <see cref="DateTime"/> as end time.
        /// </summary>
        /// <param name="expirationTime"><see cref="DateTime"/></param>
        public CancellationTokenSourceEnhanced(DateTime expirationTime)
        {
            var timeout = expirationTime - DateTime.UtcNow;
            if (timeout <= TimeSpan.Zero) // If the time has already passed, trigger token now.
            {
                _cts = new CancellationTokenSource();
                _cts.Cancel();
                _expirationTime = DateTime.UtcNow;
            }
            else
            {
                _cts = new CancellationTokenSource(timeout);
                _expirationTime = expirationTime;
                if (_expirationTime != DateTime.MinValue && _expirationTime != DateTime.MaxValue)
                {
                    RegisterExpirationCallback();
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="CancellationToken"/> associated with this instance.
        /// </summary>
        public CancellationToken Token
        {
            get
            {
                lock (_lock)
                {
                    if (_isDisposed)
                        throw new ObjectDisposedException(nameof(CancellationTokenSourceEnhanced));

                    return _cts.Token;
                }
            }
        }

        /// <summary>
        /// Returns the IsCancellationRequested property for the <see cref="CancellationToken"/>.
        /// </summary>
        public bool IsCancellationRequested => _cts?.IsCancellationRequested ?? false;

        /// <summary>
        /// Returns the time remaining for the <see cref="CancellationToken"/>.
        /// </summary>
        public TimeSpan TimeRemaining
        {
            get
            {
                lock (_lock)
                {
                    if (_isDisposed || !_expirationTime.HasValue || _cts.IsCancellationRequested)
                        return TimeSpan.Zero;

                    var remaining = _expirationTime.Value - DateTime.UtcNow;
                    return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                }
            }
        }

        /// <summary>
        /// Manual Trigger
        /// </summary>
        public void Cancel()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return;

                _expirationTime = DateTime.UtcNow; // Set expiration to 'now'
                _cts.Cancel();
            }
        }

        /// <summary>
        /// Reset or Set a new timeout
        /// </summary>
        public void CancelAfter(TimeSpan delay)
        {
            lock (_lock)
            {
                // Ensure we don't update time if already cancelled
                if (_isDisposed || _cts.IsCancellationRequested)
                    return;

                _expirationTime = DateTime.UtcNow.Add(delay);
                _cts.CancelAfter(delay);
            }
        }

        /// <summary>
        /// Public check for disposal status
        /// </summary>
        /// <returns></returns>
        public bool IsDisposed()
        {
            lock (_lock)
            {
                return _isDisposed;
            }
        }

        /// <summary>
        /// Disposes the <see cref="CancellationTokenSource"/> associated with this instance.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;
                _registration.Dispose(); // be sure no registered tokens hang around in memory
                _cts.Dispose();
            }
        }

        void RegisterExpirationCallback()
        {
            // Register a callback on the token itself
            _registration = this.Token.Register(() =>
            {
                // If we are disposed or time is still left, it wasn't a timeout
                if (IsDisposed())
                    return;

                // Check if the time has actually run out (within a small margin of error)
                if (TimeRemaining <= TimeSpan.FromMilliseconds(_millisecondMargin))
                    TimerExpired?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    /// <summary><code>
    /// 
    /// Interlocked Version (Technical Improvements)
    /// 
    /// [Atomic 64-bit Reads]
    ///   Using Interlocked.Read on _expirationTicks ensures that reading the long 
    ///   value is thread-safe even on 32-bit systems.
    /// [Volatile Reads]
    ///   Volatile.Read ensures the _disposed flag is always read directly from memory 
    ///   rather than a CPU cache, guaranteeing visibility across threads.
    /// [Lock-Free Disposal]
    ///   The Interlocked.Exchange pattern in Dispose() ensures that the internal 
    ///   _cts.Dispose() is called exactly once, even if multiple threads call it simultaneously.
    /// [Reduced Contention]
    ///   Unlike a lock, which forces threads to wait in a queue, these operations execute 
    ///   immediately at the hardware level, which is ideal for high-frequency UI updates 
    ///   or rapid-fire "Cancel/Restart" scenarios.
    /// [CancellationTokenRegistration Support] 
    ///   The <see cref="TimerExpired"/> event can be used to register a 
    ///   callback that will be invoked when the token is canceled.
    /// </code></summary>
    public class InterlockedCancellationTokenSource : IDisposable
    {
        readonly CancellationTokenSource _cts;
        long _expirationTicks; // Store as ticks for atomic Interlocked operations
        int _disposed;         // 0 = active, 1 = disposed
        CancellationTokenRegistration _registration;
        // NOTE: CancellationTokenRegistration objects can stay in memory as long 
        //       as the CancellationTokenSource exists unless explicitly disposed.
        //       We're storing the registration for later disposal.
        public event EventHandler TimerExpired;

        public InterlockedCancellationTokenSource()
        {
            _cts = new CancellationTokenSource();
        }

        public InterlockedCancellationTokenSource(TimeSpan timeout)
        {
            _cts = new CancellationTokenSource(timeout);
            _expirationTicks = DateTime.UtcNow.Add(timeout).Ticks;
            if (timeout > TimeSpan.Zero)
                RegisterExpirationCallback();
        }


        public InterlockedCancellationTokenSource(DateTime expirationTime)
        {
            var timeout = expirationTime - DateTime.UtcNow;
            if (timeout <= TimeSpan.Zero)
            {
                _cts = new CancellationTokenSource();
                _cts.Cancel();
                _expirationTicks = DateTime.UtcNow.Ticks;
            }
            else
            {
                _cts = new CancellationTokenSource(timeout);
                _expirationTicks = expirationTime.Ticks;
                RegisterExpirationCallback();
            }
        }

        public InterlockedCancellationTokenSource(int millisecondsDelay)
        {
            if (millisecondsDelay < -1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsDelay), "Milliseconds delay must be -1 (infinite) or non-negative.");

            if (millisecondsDelay == Timeout.Infinite) // -1
            {
                _cts = new CancellationTokenSource();
                _expirationTicks = 0; // Infinite means no expiration
            }
            else
            {
                var timeout = TimeSpan.FromMilliseconds(millisecondsDelay);
                _cts = new CancellationTokenSource(timeout);
                _expirationTicks = DateTime.UtcNow.Add(timeout).Ticks;
                RegisterExpirationCallback();
            }
        }

        /// <summary>
        /// High-performance check for disposal without a lock
        /// </summary>
        public bool IsDisposed() => Volatile.Read(ref _disposed) == 1;

        public CancellationToken Token
        {
            get
            {
                if (IsDisposed())
                    throw new ObjectDisposedException(nameof(InterlockedCancellationTokenSource));

                return _cts.Token;
            }
        }

        /// <summary>
        /// Returns the IsCancellationRequested property for the <see cref="CancellationToken"/>.
        /// </summary>
        public bool IsCancellationRequested
        {
            get
            {
                if (IsDisposed())
                    return false;

                return _cts.IsCancellationRequested;
            }
        }


        public TimeSpan TimeRemaining
        {
            get
            {
                if (IsDisposed() || _cts.IsCancellationRequested)
                    return TimeSpan.Zero;

                long ticks = Interlocked.Read(ref _expirationTicks);
                if (ticks == 0)
                    return TimeSpan.Zero;

                // Create the target DateTime from ticks first
                var expirationDateTime = new DateTime(ticks);
                var remaining = expirationDateTime - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public void Cancel()
        {
            if (IsDisposed())
                return;

            // Atomically set time to now and trigger cancellation
            Interlocked.Exchange(ref _expirationTicks, DateTime.UtcNow.Ticks);
            _cts.Cancel();
        }

        public void CancelAfter(TimeSpan delay)
        {
            if (IsDisposed() || _cts.IsCancellationRequested)
                return;

            var newTicks = DateTime.UtcNow.Add(delay).Ticks;
            Interlocked.Exchange(ref _expirationTicks, newTicks);
            _cts.CancelAfter(delay);
        }

        public void Dispose()
        {
            // Interlocked.Exchange returns the original value. 
            // If it was 0, we are the first to dispose.
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _registration.Dispose();
                _cts.Dispose();
            }
        }

        void RegisterExpirationCallback()
        {
            // Register a callback on the token itself
            _registration = this.Token.Register(() =>
            {
                // If we are disposed or time is still left, it wasn't a timeout
                if (IsDisposed())
                    return;

                // Check if the time has actually run out (within a small margin of error)
                if (TimeRemaining <= TimeSpan.FromMilliseconds(20))
                {
                    TimerExpired?.Invoke(this, EventArgs.Empty);
                }
            });
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
    public static class MethodTimerEx
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
