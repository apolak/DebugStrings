namespace DebugStrings
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.IO.MemoryMappedFiles;
    using System.Security;
    using System.Security.AccessControl;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Monitors values written to the debug output.
    /// </summary>
    public sealed class DebugView : IDisposable
    {
        /// <summary>
        /// The length of the data buffer.
        /// </summary>
        private const int DataBufferLength = 4096;

        /// <summary>
        /// The <see cref="DebugView"/> that has not been associated with the debug output objects.
        /// </summary>
        private static readonly DebugView Detached = new DebugView();

        /// <summary>
        /// Indicates whether this <see cref="DebugView"/> has been successfully associated
        /// with the debug output objects.
        /// </summary>
        private readonly bool attached;

        /// <summary>
        /// The memory-mapped file to which the data is written to.
        /// </summary>
        private MemoryMappedFile dataBufferFile;

        /// <summary>
        /// The event that this <see cref="DebugView"/> sets to signaled when the memory-mapped file
        /// is ready to receive data.
        /// </summary>
        private EventWaitHandle bufferReadyEvent;

        /// <summary>
        /// The event that becomes signaled when data has been written to the memory-mapped file.
        /// </summary>
        private WaitHandle dataReadyEvent;

        /// <summary>
        /// The memory buffer to which the data is read from the memory-mapped file.
        /// </summary>
        private byte[] dataBuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugView"/> class.
        /// </summary>
        private DebugView()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugView"/> class.
        /// </summary>
        /// <param name="dataBufferFile">
        /// The memory-mapped file to which the data is written to.
        /// </param>
        /// <param name="bufferReadyEvent">
        /// The event that this <see cref="DebugView"/> sets to signaled when the memory-mapped file
        /// is ready to receive data.
        /// </param>
        /// <param name="dataReadyEvent">
        /// The event that becomes signaled when data has been written to the memory-mapped file.
        /// </param>
        private DebugView(
            MemoryMappedFile dataBufferFile,
            EventWaitHandle bufferReadyEvent,
            WaitHandle dataReadyEvent)
        {
            Debug.Assert(dataBufferFile != null, "dataBufferFile is null");
            Debug.Assert(bufferReadyEvent != null, "bufferReadyEvent is null");
            Debug.Assert(dataReadyEvent != null, "dataReadyEvent is null");

            this.attached = true;
            this.dataBufferFile = dataBufferFile;
            this.bufferReadyEvent = bufferReadyEvent;
            this.dataReadyEvent = dataReadyEvent;
            this.dataBuffer = new byte[DataBufferLength];

            this.bufferReadyEvent.Set();
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="DebugView"/> has been successfully
        /// associated with the debug output objects.
        /// </summary>
        public bool IsAttached
        {
            get { return this.attached; }
        }

        /// <summary>
        /// Creates a new <see cref="DebugView"/>.
        /// </summary>
        /// <returns>
        /// The newly created <see cref="DebugView"/>.
        /// </returns>
        public static DebugView CreateView()
        {
            const string MemoryMappedFileName = @"DBWIN_BUFFER";
            const string BufferReadyEventName = @"DBWIN_BUFFER_READY";
            const string DataReadyEventName = @"DBWIN_DATA_READY";

            MemoryMappedFile dataBufferFile = null;
            EventWaitHandle bufferReadyEvent = null;
            WaitHandle dataReadyEvent = null;
            bool createdNew = false;

            try
            {
                dataBufferFile = MemoryMappedFile.CreateNew(
                    MemoryMappedFileName,
                    DataBufferLength,
                    MemoryMappedFileAccess.ReadWrite);

                bufferReadyEvent = new EventWaitHandle(
                    false,
                    EventResetMode.AutoReset,
                    BufferReadyEventName,
                    out createdNew);

                if (!createdNew)
                {
                    return Detached;
                }

                dataReadyEvent = new EventWaitHandle(
                    false,
                    EventResetMode.AutoReset,
                    DataReadyEventName,
                    out createdNew);

                if (!createdNew)
                {
                    return Detached;
                }
            }
            catch (IOException)
            {
                return Detached;
            }
            catch (UnauthorizedAccessException)
            {
                return Detached;
            }
            finally
            {
                if (!createdNew)
                {
                    if (dataBufferFile != null)
                    {
                        dataBufferFile.Dispose();
                    }

                    if (bufferReadyEvent != null)
                    {
                        bufferReadyEvent.Dispose();
                    }

                    if (dataReadyEvent != null)
                    {
                        dataReadyEvent.Dispose();
                    }
                }
            }

            var buffer = new DebugView(dataBufferFile, bufferReadyEvent, dataReadyEvent);
            return buffer;
        }
        
        /// <summary>
        /// Removes a <see cref="DebugString"/> from this <see cref="DebugView"/>.
        /// </summary>
        /// <returns>
        /// The <see cref="DebugString"/> removed from this <see cref="DebugView"/>.
        /// </returns>
        [SecuritySafeCritical]
        public DebugString Take()
        {
            return this.Take(CancellationToken.None);
        }

        /// <summary>
        /// Removes a <see cref="DebugString"/> from the buffer contained by this
        /// <see cref="DebugView"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> that can be used to cancel the take operation.
        /// </param>
        /// <returns>
        /// The <see cref="DebugString"/> removed from this <see cref="DebugView"/>.
        /// </returns>
        [SecuritySafeCritical]
        public DebugString Take(CancellationToken cancellationToken)
        {
            DebugString value;
            bool taken = TryTake(out value, Timeout.Infinite, cancellationToken);
            Debug.Assert(taken, "taken is false");

            return value;
        }

        /// <summary>
        /// Attempts to remove a <see cref="DebugString" /> from the buffer contained by this
        /// <see cref="DebugView"/>.
        /// </summary>
        /// <param name="value">
        /// When <see cref="TryTake(DebugString)"/> returns, contains the removed <see cref="DebugString"/>,
        /// if successful; otherwise, an empty <see cref="DebugString"/>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the <see cref="DebugString"/> has been removed; otherwise, <c>false</c>.
        /// </returns>
        [SecuritySafeCritical]
        public bool TryTake(out DebugString value)
        {
            return this.TryTake(out value, 0, CancellationToken.None);
        }

        /// <summary>
        /// Attempts to remove a <see cref="DebugString" /> from the buffer contained by this
        /// <see cref="DebugView"/> within the specified time.
        /// </summary>
        /// <param name="value">
        /// When <see cref="TryTake(DebugString,int)"/> returns, contains the removed
        /// <see cref="DebugString"/>, if successful; otherwise, an empty <see cref="DebugString"/>.
        /// </param>
        /// <param name="timeout">
        /// The number of milliseconds to wait, or <see cref="Timeout.Infinite"/> (-1) to wait
        /// indefinitely.
        /// </param>
        /// <returns>
        /// <c>true</c> if the <see cref="DebugString"/> has been removed within the specified time;
        /// otherwise, <c>false</c>.
        /// </returns>
        [SecuritySafeCritical]
        public bool TryTake(out DebugString value, int timeout)
        {
            return this.TryTake(out value, timeout, CancellationToken.None);
        }

        /// <summary>
        /// Attempts to remove a <see cref="DebugString" /> from the buffer contained by this
        /// <see cref="DebugView"/> within the specified time.
        /// </summary>
        /// <param name="value">
        /// When <see cref="TryTake(DebugString,TimeSpan)"/> returns, contains the removed
        /// <see cref="DebugString"/>, if successful; otherwise, an empty <see cref="DebugString"/>.
        /// </param>
        /// <param name="timeout">
        /// The <see cref="TimeSpan"/> that represents the number of milliseconds to wait,
        /// or the <see cref="TimeSpan"/> that represents -1 milliseconds to wait indefinitely.
        /// </param>
        /// <returns>
        /// <c>true</c> if the <see cref="DebugString"/> has been removed within the specified time;
        /// otherwise, <c>false</c>.
        /// </returns>
        [SecuritySafeCritical]
        public bool TryTake(out DebugString value, TimeSpan timeout)
        {
            return this.TryTake(out value, timeout, CancellationToken.None);
        }

        /// <summary>
        /// Attempts to remove a <see cref="DebugString" /> from the buffer contained by this
        /// <see cref="DebugView"/> within the specified time while monitoring cancellation requests.
        /// </summary>
        /// <param name="value">
        /// When <see cref="TryTake(DebugString,TimeSpan,CancellationToken)"/> returns, contains
        /// the removed <see cref="DebugString"/>, if successful; otherwise, an empty
        /// <see cref="DebugString"/>.
        /// </param>
        /// <param name="timeout">
        /// The <see cref="TimeSpan"/> that represents the number of milliseconds to wait,
        /// or the <see cref="TimeSpan"/> that represents -1 milliseconds to wait indefinitely.
        /// </param>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// <c>true</c> if the <see cref="DebugString"/> has been removed within the specified time;
        /// otherwise, <c>false</c>.
        /// </returns>
        [SecuritySafeCritical]
        public bool TryTake(out DebugString value, TimeSpan timeout, CancellationToken cancellationToken)
        {
            long timeoutMilliseconds = (long)timeout.TotalMilliseconds;

            if ((timeoutMilliseconds < Timeout.Infinite) || (timeoutMilliseconds > int.MaxValue))
            {
                throw new ArgumentOutOfRangeException("timeout", "Timeout in milliseconds must be either non-negative and less than or equal to Int32.MaxValue or Timeout.Infinite (-1).");
            }

            return this.TryTake(out value, (int)timeoutMilliseconds, cancellationToken);
        }

        /// <summary>
        /// Attempts to remove a <see cref="DebugString" /> from the buffer contained by this
        /// <see cref="DebugView"/> within the specified time while monitoring cancellation requests.
        /// </summary>
        /// <param name="value">
        /// When <see cref="TryTake(DebugString,int,CancellationToken)"/> returns, contains the removed
        /// <see cref="DebugString"/>, if successful; otherwise, an empty <see cref="DebugString"/>.
        /// </param>
        /// <param name="timeout">
        /// The number of milliseconds to wait, or <see cref="Timeout.Infinite"/> (-1) to wait
        /// indefinitely.
        /// </param>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// <c>true</c> if the <see cref="DebugString"/> has been removed within the specified time;
        /// otherwise, <c>false</c>.
        /// </returns>
        [SecuritySafeCritical]
        public bool TryTake(out DebugString value, int timeout, CancellationToken cancellationToken)
        {
            if (!this.attached)
            {
                this.Wait(timeout, cancellationToken);

                value = default(DebugString);
                return false;
            }

            if (!this.TryWaitForDataReady(timeout, cancellationToken))
            {
                value = default(DebugString);
                return false;
            }

            try
            {
                value = this.ReadDebugString();
                return true;
            }
            finally
            {
                this.bufferReadyEvent.Set();
            }
        }

        /// <summary>
        /// Asynchronously removes a <see cref="DebugString" /> from the buffer contained by this
        /// <see cref="DebugView"/>.
        /// </summary>
        /// <returns>
        /// The task that represents the asynchronous removal operation. The <see cref="Task{T}.Result"/>
        /// contains the <see cref="DebugString"/> removed from the buffer.
        /// </returns>
        public async Task<DebugString> TakeAsync()
        {
            return await TakeAsync(CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        /// <summary>
        /// Asynchronously removes a <see cref="DebugString" /> from the buffer contained by this
        /// <see cref="DebugView"/> while monitoring cancellation requests.
        /// </summary>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// The task that represents the asynchronous removal operation. The <see cref="Task{T}.Result"/>
        /// contains the <see cref="DebugString"/> removed from the buffer.
        /// </returns>
        public async Task<DebugString> TakeAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<DebugString>();

            if (!this.attached)
            {
                cancellationToken.Register(
                    state => ((TaskCompletionSource<DebugString>)state).SetCanceled(), tcs);

                return await tcs.Task.ConfigureAwait(continueOnCapturedContext: false);
            }

            RegisteredWaitHandle rwh = ThreadPool.RegisterWaitForSingleObject(
                this.dataReadyEvent,
                (state, timedOut) =>
                {
                    Debug.Assert(!timedOut, "timedOut is true");

                    try
                    {
                        DebugString value = this.ReadDebugString();
                        ((TaskCompletionSource<DebugString>)state).SetResult(value);
                    }
                    catch (Exception ex)
                    {
                        ((TaskCompletionSource<DebugString>)state).SetException(ex);
                    }
                },
                tcs,
                Timeout.Infinite,
                executeOnlyOnce: true);

            CancellationTokenRegistration ctr = cancellationToken.Register(() =>
            {
                rwh.Unregister(this.dataReadyEvent);
                tcs.TrySetCanceled();
            });

            try
            {
                return await tcs.Task.ConfigureAwait(continueOnCapturedContext: false);
            }
            finally
            {
                rwh.Unregister(this.dataReadyEvent);
                this.bufferReadyEvent.Set();

                ctr.Dispose();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting
        /// unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            IDisposable d;
            
            d = this.dataBufferFile;

            if (d != null)
            {
                this.dataBufferFile = null;
                d.Dispose();
            }

            d = this.bufferReadyEvent;

            if (d != null)
            {
                this.bufferReadyEvent = null;
                d.Dispose();
            }

            d = this.dataReadyEvent;

            if (d != null)
            {
                this.dataReadyEvent = null;
                d.Dispose();
            }

            this.dataBuffer = null;
        }

        /// <summary>
        /// Attempts to wait until data is ready within the specified time while monitoring cancellation
        /// requests.
        /// </summary>
        /// <param name="timeout">
        /// The number of milliseconds to wait, or <see cref="Timeout.Infinite"/> (-1) to wait
        /// indefinitely.
        /// </param>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// <c>true</c> if data is ready; otherwise, <c>false</c>.
        /// </returns>
        private bool TryWaitForDataReady(int timeout, CancellationToken cancellationToken)
        {
            if (cancellationToken != CancellationToken.None)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int waitResult = WaitHandle.WaitAny(
                    new[]
                    {
                        this.dataReadyEvent,
                        cancellationToken.WaitHandle
                    },
                    timeout);

                if (waitResult == 1)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return waitResult == 0;
            }

            return this.dataReadyEvent.WaitOne(timeout);
        }

        /// <summary>
        /// Waits within the specified time while monitoring cancellation requests.
        /// </summary>
        /// <param name="timeout">
        /// The number of milliseconds to wait, or <see cref="Timeout.Infinite"/> (-1) to wait
        /// indefinitely.
        /// </param>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests.
        /// </param>
        private void Wait(int timeout, CancellationToken cancellationToken)
        {
            if (cancellationToken == CancellationToken.None)
            {
                Thread.Sleep(timeout);
            }
            else if (cancellationToken.WaitHandle.WaitOne(timeout))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
        
        /// <summary>
        /// Reads the <see cref="DebugString"/> from the underlying memory-mapped file.
        /// </summary>
        /// <returns>
        /// The <see cref="DebugString"/> read from the underlying memory-mapped file.
        /// </returns>
        private DebugString ReadDebugString()
        {
            using (var viewStream = this.dataBufferFile.CreateViewStream())
            {
                int bytesRead = viewStream.Read(this.dataBuffer, 0, this.dataBuffer.Length);
                Debug.Assert(bytesRead == DataBufferLength, "bytesRead is not 4096");

                // CORRECTNESS Skip the first 4 bytes in the buffer that indicate the process ID when searching for the null terminator.
                int terminator = Array.IndexOf(this.dataBuffer, (byte)0, sizeof(int));
                Debug.Assert((terminator < 0) || (terminator >= sizeof(int)), "terminator is between 0 and 3");

                if (terminator < 0)
                {
                    // ROBUSTNESS Null terminator not found, assume it is placed after the last byte in the buffer.
                    terminator = DataBufferLength;
                }

                int processId = BitConverter.ToInt32(this.dataBuffer, 0);
                string value = Encoding.Default.GetString(this.dataBuffer, sizeof(int), terminator - sizeof(int));

                return new DebugString(processId, value);
            }
        }
    }
}
