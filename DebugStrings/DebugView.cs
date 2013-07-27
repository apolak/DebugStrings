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
        /// The <see cref="DebugViewContext"/> that provides intrinsic kernel objects
        /// for inter-process communication.
        /// </summary>
        private DebugViewContext context;
        
        /// <summary>
        /// The memory buffer to which the data is read from the memory-mapped file.
        /// </summary>
        private byte[] buffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugView"/> class.
        /// </summary>
        /// <param name="context">
        /// The <see cref="DebugViewContext"/> that provides intrinsic kernel objects
        /// for inter-process communication.
        /// </param>
        private DebugView(DebugViewContext context)
        {
            this.context = context;
            
            if (context != null)
            {
                this.buffer = new byte[DebugViewContext.BufferLength];
            }
        }

        /// <summary>
        /// Gets a value indicating whether the buffer to which debug output values
        /// are written to has been successfully created.
        /// </summary>
        public bool IsAttached
        {
            get { return this.context != null; }
        }

        /// <summary>
        /// Creates a new <see cref="DebugView"/>.
        /// </summary>
        /// <returns>
        /// The newly created <see cref="DebugView"/>.
        /// </returns>
        public static DebugView CreateView()
        {
            DebugViewContext context;

            DebugViewContext.TryAcquireContext(out context);
            var view = new DebugView(context);

            return view;
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
            if (this.context == null)
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
                this.context.BufferReadyEventHandle.Set();
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

            if (this.context == null)
            {
                cancellationToken.Register(
                    state => ((TaskCompletionSource<DebugString>)state).SetCanceled(), tcs);

                return await tcs.Task.ConfigureAwait(continueOnCapturedContext: false);
            }

            RegisteredWaitHandle rwh = ThreadPool.RegisterWaitForSingleObject(
                this.context.DataReadyEventHandle,
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
                rwh.Unregister(this.context.DataReadyEventHandle);
                tcs.TrySetCanceled();
            });

            try
            {
                return await tcs.Task.ConfigureAwait(continueOnCapturedContext: false);
            }
            finally
            {
                rwh.Unregister(this.context.DataReadyEventHandle);
                this.context.BufferReadyEventHandle.Set();

                ctr.Dispose();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting
        /// unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            IDisposable d = this.context;

            if (d != null)
            {
                this.context = null;
                d.Dispose();
            }

            this.buffer = null;
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
                        this.context.DataReadyEventHandle,
                        cancellationToken.WaitHandle
                    },
                    timeout);

                if (waitResult == 1)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return waitResult == 0;
            }

            return this.context.DataReadyEventHandle.WaitOne(timeout);
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
            using (var viewStream = this.context.Buffer.CreateViewStream())
            {
                int bytesRead = viewStream.Read(this.buffer, 0, this.buffer.Length);

                if (bytesRead < sizeof(int))
                {
                    throw new FormatException("The format of the debug data is invalid");
                }

                // CORRECTNESS Skip the first 4 bytes in the buffer that indicate the process ID when searching for the null terminator.
                int terminator = Array.IndexOf(this.buffer, (byte)0, sizeof(int), bytesRead - sizeof(int));
                Debug.Assert((terminator < 0) || (terminator >= sizeof(int)), "terminator is between 0 and 3");

                if (terminator < 0)
                {
                    // ROBUSTNESS Null terminator not found, assume it is placed after the last byte read into the buffer.
                    terminator = bytesRead;
                }

                int processId = BitConverter.ToInt32(this.buffer, 0);
                string value = Encoding.Default.GetString(this.buffer, sizeof(int), terminator - sizeof(int));

                return new DebugString(processId, value);
            }
        }

        /// <summary>
        /// Provides intrinsic kernel objects for inter-process communication.
        /// </summary>
        private sealed class DebugViewContext : IDisposable
        {
            /// <summary>
            /// The length of the memory-mapped file to which the data is written to.
            /// </summary>
            public const int BufferLength = 4096;

            /// <summary>
            /// The memory-mapped file to which the data is written to.
            /// </summary>
            private MemoryMappedFile buffer;

            /// <summary>
            /// The event wait handle that this <see cref="DebugView"/> sets to signaled when
            /// the memory-mapped file is ready to receive data.
            /// </summary>
            private EventWaitHandle bufferReadyEventHandle;

            /// <summary>
            /// The wait handle that becomes signaled when data has been written to the memory-mapped
            /// file.
            /// </summary>
            private WaitHandle dataReadyEventHandle;

            /// <summary>
            /// Initializes a new instance of the <see cref="DebugViewContext"/> class.
            /// </summary>
            /// <param name="buffer">
            /// The memory-mapped file to which the data is written to.
            /// </param>
            /// <param name="bufferReadyEventHandle">
            /// The event wait handle that this <see cref="DebugView"/> sets to signaled when
            /// the memory-mapped file is ready to receive data.
            /// </param>
            /// <param name="dataReadyEventHandle">
            /// The wait handle that becomes signaled when data has been written to the memory-mapped
            /// file.
            /// </param>
            public DebugViewContext(
                MemoryMappedFile buffer,
                EventWaitHandle bufferReadyEventHandle,
                WaitHandle dataReadyEventHandle)
            {
                Debug.Assert(buffer != null, "file is null");
                Debug.Assert(bufferReadyEventHandle != null, "bufferReadyEventHandle is null");
                Debug.Assert(dataReadyEventHandle != null, "dataReadyEventHandle is null");

                this.buffer = buffer;
                this.bufferReadyEventHandle = bufferReadyEventHandle;
                this.dataReadyEventHandle = dataReadyEventHandle;
            }

            /// <summary>
            /// Gets the memory-mapped file to which the data is written to.
            /// </summary>
            public MemoryMappedFile Buffer
            {
                get { return this.buffer; }
            }

            /// <summary>
            /// Gets the event wait handle that this <see cref="DebugView"/> sets to signaled
            /// when the memory-mapped file is ready to receive data.
            /// </summary>
            public EventWaitHandle BufferReadyEventHandle
            {
                get { return this.bufferReadyEventHandle; }
            }

            /// <summary>
            /// Gets the wait handle that becomes signaled when data has been written
            /// to the memory-mapped file.
            /// </summary>
            public WaitHandle DataReadyEventHandle
            {
                get { return this.dataReadyEventHandle; }
            }

            /// <summary>
            /// Attempts to acquire intrinsic kernel objects for inter-process communication.
            /// </summary>
            /// <param name="context">
            /// When <see cref="TryAcquireContext"/> returns, contains the acquired
            /// <see cref="DebugViewContext"/>, is succeeded; otherwise, a <c>null</c> reference
            /// (<c>Nothing</c> in Visual Basic).
            /// </param>
            /// <returns>
            /// <c>true</c> if the objects have been successfully acquired; otherwise, <c>false</c>.
            /// </returns>
            public static bool TryAcquireContext(out DebugViewContext context)
            {
                const string MemoryMappedFileName = @"DBWIN_BUFFER";
                const string ReadyEventName = @"DBWIN_BUFFER_READY";
                const string DataReadyEventName = @"DBWIN_DATA_READY";

                context = null;

                MemoryMappedFile buffer = null;
                EventWaitHandle bufferReadyEventHandle = null;
                WaitHandle dataReadyEventHandle = null;
                bool createdNew = false;

                try
                {
                    buffer = MemoryMappedFile.CreateNew(
                        MemoryMappedFileName,
                        BufferLength,
                        MemoryMappedFileAccess.ReadWrite);

                    dataReadyEventHandle = new EventWaitHandle(
                        false,
                        EventResetMode.AutoReset,
                        DataReadyEventName,
                        out createdNew);

                    if (!createdNew)
                    {
                        return false;
                    }

                    bufferReadyEventHandle = new EventWaitHandle(
                        true,
                        EventResetMode.AutoReset,
                        ReadyEventName,
                        out createdNew);

                    if (!createdNew)
                    {
                        return false;
                    }
                }
                catch (IOException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
                finally
                {
                    if (!createdNew)
                    {
                        if (bufferReadyEventHandle != null)
                        {
                            bufferReadyEventHandle.Dispose();
                        }

                        if (dataReadyEventHandle != null)
                        {
                            dataReadyEventHandle.Dispose();
                        }

                        if (buffer != null)
                        {
                            buffer.Dispose();
                        }
                    }
                }

                context = new DebugViewContext(buffer, bufferReadyEventHandle, dataReadyEventHandle);
                return true;
            }

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting
            /// unmanaged resources.
            /// </summary>
            public void Dispose()
            {
                IDisposable d;

                d = this.bufferReadyEventHandle;

                if (d != null)
                {
                    this.bufferReadyEventHandle = null;
                    d.Dispose();
                }

                d = this.dataReadyEventHandle;

                if (d != null)
                {
                    this.dataReadyEventHandle = null;
                    d.Dispose();
                }

                d = this.buffer;

                if (d != null)
                {
                    this.buffer = null;
                    d.Dispose();
                }
            }
        }
    }
}
