namespace DebugStrings
{
    using System;
    using System.Diagnostics;
    using System.IO.MemoryMappedFiles;
    using System.Security.AccessControl;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines interface for receiving data sent to the debug output.
    /// </summary>
    public sealed class DebugBuffer : IDebugBuffer
    {
        /// <summary>
        /// The memory-mapped file to which the data is written to.
        /// </summary>
        private MemoryMappedFile bufferFile;

        /// <summary>
        /// The event wait handle used to notify when the memory-mapped file is ready to receive data.
        /// </summary>
        private EventWaitHandle bufferReadyEventHandle;

        /// <summary>
        /// The wait handle used to notify when the data has been written to the memory-mapped file.
        /// </summary>
        private WaitHandle dataReadyEventHandle;

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugBuffer"/> class.
        /// </summary>
        /// <param name="bufferFile">
        /// The memory-mapped file to which the data is written to.
        /// </param>
        /// <param name="bufferReadyEventHandle">
        /// The event wait handle used to notify when the memory-mapped file is ready to receive data.
        /// </param>
        /// <param name="dataReadyEventHandle">
        /// The wait handle used to notify when the data has been written to the memory-mapped file.
        /// </param>
        private DebugBuffer(
            MemoryMappedFile bufferFile,
            EventWaitHandle bufferReadyEventHandle,
            WaitHandle dataReadyEventHandle)
        {
            Debug.Assert(bufferFile != null, "bufferFile is null");
            Debug.Assert(bufferReadyEventHandle != null, "bufferReadyEventHandle is null");
            Debug.Assert(dataReadyEventHandle != null, "dataReadyEventHandle is null");

            this.bufferFile = bufferFile;
            this.bufferReadyEventHandle = bufferReadyEventHandle;
            this.dataReadyEventHandle = dataReadyEventHandle;
        }

        /// <summary>
        /// Acquires the buffer that enables receiving data sent to the debug output using local
        /// prefix when creating named system objects for inter-process communication.
        /// </summary>
        /// <returns>
        /// The <see cref="DebugBuffer"/> that represents a newly acquired buffer that enables
        /// receiving data sent to the debug output.
        /// </returns>
        public static DebugBuffer AcquireLocalBuffer()
        {
            return AcquireBuffer(NamePrefix.Local);
        }

        /// <summary>
        /// Acquires the buffer that enables receiving data sent to the debug output using global
        /// prefix when creating named system objects for inter-process communication.
        /// </summary>
        /// <returns>
        /// The <see cref="DebugBuffer"/> that represents a newly acquired buffer that enables
        /// receiving data sent to the debug output.
        /// </returns>
        public static DebugBuffer AcquireGlobalBuffer()
        {
            return AcquireBuffer(NamePrefix.Global);
        }

        /// <summary>
        /// Acquires the buffer that enables receiving data sent to the debug output using the specified
        /// prefix when creating named system objects for inter-process communication.
        /// </summary>
        /// <param name="namePrefix">
        /// The prefix to use when creating named system objects for inter-process communication.
        /// </param>
        /// <returns>
        /// The <see cref="DebugBuffer"/> that represents a newly acquired buffer that enables
        /// receiving data sent to the debug output.
        /// </returns>
        public static DebugBuffer AcquireBuffer(NamePrefix namePrefix)
        {
            MemoryMappedFile bufferFile = null;
            EventWaitHandle bufferReadyEventHandle = null;
            WaitHandle dataReadyEventHandle = null;
            bool createdNew = false;

            var securityDescriptor = new RawSecurityDescriptor(
                ControlFlags.SelfRelative | ControlFlags.DiscretionaryAclPresent,
                null,
                null,
                null,
                null);

            var memoryMappedFileSecurity = new MemoryMappedFileSecurity();
            var eventWaitHandleSecurity = new EventWaitHandleSecurity();

            var securityDescriptorBytes = new byte[securityDescriptor.BinaryLength];
            securityDescriptor.GetBinaryForm(securityDescriptorBytes, 0);
            memoryMappedFileSecurity.SetSecurityDescriptorBinaryForm(securityDescriptorBytes);
            eventWaitHandleSecurity.SetSecurityDescriptorBinaryForm(securityDescriptorBytes);

            try
            {
                bufferFile = DebugMonitor.CreateNewBufferFile(
                    namePrefix,
                    memoryMappedFileSecurity);

                dataReadyEventHandle = DebugMonitor.CreateDataReadyEventHandle(
                    namePrefix,
                    eventWaitHandleSecurity,
                    out createdNew);

                if (!createdNew)
                {
                    throw new InvalidOperationException("The named system object already exists.");
                }

                bufferReadyEventHandle = DebugMonitor.CreateBufferReadyEventHandle(
                    NamePrefix.Local,
                    eventWaitHandleSecurity,
                    out createdNew);

                if (!createdNew)
                {
                    throw new InvalidOperationException("The named system object already exists.");
                }
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

                    if (bufferFile != null)
                    {
                        bufferFile.Dispose();
                    }
                }
            }

            var buffer = new DebugBuffer(bufferFile, bufferReadyEventHandle, dataReadyEventHandle);
            return buffer;
        }

        /// <summary>
        /// Requests that the data be written to the underlying memory-mapped file.
        /// </summary>
        public void RequestData()
        {
            this.bufferReadyEventHandle.Set();
        }

        /// <summary>
        /// Attempts to wait until data is ready within the specified time while monitoring cancellation
        /// requests.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <see cref="Timeout.Infinite"/> (-1) to wait
        /// indefinitely.
        /// </param>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// <c>true</c> if data is ready; otherwise, <c>false</c>.
        /// </returns>
        public bool TryWaitForData(int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            if (cancellationToken != CancellationToken.None)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int waitResult = WaitHandle.WaitAny(
                    new[]
                    {
                        this.dataReadyEventHandle,
                        cancellationToken.WaitHandle
                    },
                    timeoutMilliseconds);

                if (waitResult == 1)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return waitResult == 0;
            }

            return this.dataReadyEventHandle.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// Asynchronously waits until data is ready while monitoring cancellation requests.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <see cref="Timeout.Infinite"/> (-1) to wait
        /// indefinitely.
        /// </param>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// <c>true</c> if data is ready; otherwise, <c>false</c>.
        /// </returns>
        public async Task WaitForDataAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object>();

            RegisteredWaitHandle rwh = ThreadPool.RegisterWaitForSingleObject(
                this.dataReadyEventHandle,
                (state, timedOut) => ((TaskCompletionSource<object>)state).SetResult(null),
                tcs,
                Timeout.Infinite,
                executeOnlyOnce: true);

            CancellationTokenRegistration ctr = cancellationToken.Register(() =>
            {
                rwh.Unregister(null);
                tcs.TrySetCanceled();
            });

            try
            {
                await tcs.Task.ConfigureAwait(continueOnCapturedContext: false);
            }
            finally
            {
                if (!tcs.Task.IsCanceled)
                {
                    rwh.Unregister(null);
                }

                ctr.Dispose();
            }
        }

        /// <summary>
        /// Reads a block of bytes from the underlying memory-mapped file and writes the data
        /// into the specified array.
        /// </summary>
        /// <param name="buffer">
        /// When <see cref="ReadData"/> returns, contains the specified byte array with the values
        /// between <paramref name="offet"/> and (<paramref name="offset"/> + <param name="count"/> - 1)
        /// replaced by the bytes read from the underlying memory-mapped file.
        /// </param>
        /// <param name="offset">
        /// The byte offset in <param name="array"/> at which the read bytes will be placed.
        /// </param>
        /// <param name="length">
        /// The maximum number of bytes to read.
        /// </param>
        /// <returns>
        /// The total number of bytes read into <paramref name="array"/>. This might be less than
        /// the number of bytes requested if that number of bytes are not currently available, or zero
        /// if the end of the stream is reached.
        /// </returns>
        public int ReadData(byte[] array, int offset, int count)
        {
            using (var viewStream = this.bufferFile.CreateViewStream())
            {
                int bytesRead = viewStream.Read(array, offset, count);
                return bytesRead;
            }
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

            d = this.bufferFile;

            if (d != null)
            {
                this.bufferFile = null;
                d.Dispose();
            }
        }
    }
}
