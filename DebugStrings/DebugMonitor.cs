namespace DebugStrings
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.IO.MemoryMappedFiles;
    using System.Security;
    using System.Security.AccessControl;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Name prefix to use when creating named system objects.
    /// </summary>
    public enum NamePrefix
    {
        /// <summary>
        /// The <c>Local\</c> prefix.
        /// </summary>
        Local = 0,

        /// <summary>
        /// The <c>Global\</c> prefix.
        /// </summary>
        Global
    }

    /// <summary>
    /// Monitors values sent to the debug output.
    /// </summary>
    public sealed class DebugMonitor : IDisposable
    {
        /// <summary>
        /// The length of the memory-mapped file to which the data is written to.
        /// </summary>
        public const int BufferLength = 4096;

        /// <summary>
        /// The <see cref="IDebugBuffer"/> that defines interface for receiving data sent
        /// to the debug output.
        /// </summary>
        private IDebugBuffer buffer;
        
        /// <summary>
        /// The memory buffer to which to read data from the memory-mapped file.
        /// </summary>
        private byte[] data = new byte[BufferLength];

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugMonitor"/> class.
        /// </summary>
        /// <param name="buffer">
        /// The <see cref="IDebugBuffer"/> that defines interface for receiving data sent
        /// to the debug output.
        /// </param>
        public DebugMonitor(IDebugBuffer buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            this.buffer = buffer;
        }

        /// <summary>
        /// Creates the new memory-mapped file to which the data is written to.
        /// </summary>
        /// <param name="namePrefix">
        /// The prefix for the name of the memory-mapped file.
        /// </param>
        /// <param name="eventSecurity">
        /// The <see cref="EventWaitHandleSecurity"/> that represents the access control security
        /// to be applied to the named system memory-mapped file.
        /// </param>
        public static MemoryMappedFile CreateNewBufferFile(
            NamePrefix namePrefix, MemoryMappedFileSecurity memoryMappedFileSecurity)
        {
            if ((namePrefix != NamePrefix.Local) && (namePrefix != NamePrefix.Global))
            {
                throw new InvalidEnumArgumentException(
                    "namePrefix", (int)namePrefix, typeof(NamePrefix));
            }

            const string BufferFileName = "DBWIN_BUFFER";

            var bufferFile = MemoryMappedFile.CreateNew(
                namePrefix + @"\" + BufferFileName,
                BufferLength,
                MemoryMappedFileAccess.ReadWrite,
                MemoryMappedFileOptions.None,
                memoryMappedFileSecurity,
                HandleInheritability.None);

            return bufferFile;
        }

        /// <summary>
        /// Creates or opens the memory-mapped file to which the data is written to.
        /// </summary>
        /// <param name="namePrefix">
        /// The prefix for the name of the memory-mapped file.
        /// </param>
        /// <param name="eventSecurity">
        /// The <see cref="EventWaitHandleSecurity"/> that represents the access control security
        /// to be applied to the named system memory-mapped file.
        /// </param>
        public static MemoryMappedFile CreateOrOpenBufferFile(
            NamePrefix namePrefix, MemoryMappedFileSecurity memoryMappedFileSecurity)
        {
            if ((namePrefix != NamePrefix.Local) && (namePrefix != NamePrefix.Global))
            {
                throw new InvalidEnumArgumentException(
                    "namePrefix", (int)namePrefix, typeof(NamePrefix));
            }

            const string BufferFileName = "DBWIN_BUFFER";

            var bufferFile = MemoryMappedFile.CreateOrOpen(
                namePrefix + @"\" + BufferFileName,
                BufferLength,
                MemoryMappedFileAccess.ReadWrite,
                MemoryMappedFileOptions.None,
                memoryMappedFileSecurity,
                HandleInheritability.None);

            return bufferFile;
        }

        /// <summary>
        /// Creates the event wait handle that is set to signaled when the memory-mapped file
        /// is ready to receive data.
        /// </summary>
        /// <param name="namePrefix">
        /// The prefix for the name of the event wait handle.
        /// </param>
        /// <param name="eventSecurity">
        /// The <see cref="EventWaitHandleSecurity"/> that represents the access control security
        /// to be applied to the named system event.
        /// </param>
        /// <param name="createdNew">
        /// When <see cref="CreateBufferReadyEventHandle(NamePrefix,EventWaitHandleSecurity,bool)"/>
        /// returns, contains the value indicating whether the specified named system event has been
        /// created.
        /// </param>
        public static EventWaitHandle CreateBufferReadyEventHandle(
            NamePrefix namePrefix, EventWaitHandleSecurity eventSecurity, out bool createdNew)
        {
            if ((namePrefix != NamePrefix.Local) && (namePrefix != NamePrefix.Global))
            {
                throw new InvalidEnumArgumentException(
                    "namePrefix", (int)namePrefix, typeof(NamePrefix));
            }

            const string BufferReadyEventName = "DBWIN_BUFFER_READY";

            var eventHandle = new EventWaitHandle(
                true,
                EventResetMode.AutoReset,
                namePrefix + @"\" + BufferReadyEventName,
                out createdNew,
                eventSecurity);

            return eventHandle;
        }

        /// <summary>
        /// Creates the event wait handle that is set to signaled when when data has been written
        /// to the memory-mapped file.
        /// </summary>
        /// <param name="namePrefix">
        /// The prefix for the name of the event wait handle.
        /// </param>
        /// <param name="eventSecurity">
        /// The <see cref="EventWaitHandleSecurity"/> that represents the access control security
        /// to be applied to the named system event.
        /// </param>
        /// <param name="createdNew">
        /// When <see cref="CreateBufferReadyEventHandle(NamePrefix,EventWaitHandleSecurity,bool)"/>
        /// returns, contains the value indicating whether the specified named system event has been
        /// created.
        /// </param>
        public static EventWaitHandle CreateDataReadyEventHandle(
            NamePrefix namePrefix, EventWaitHandleSecurity eventSecurity, out bool createdNew)
        {
            if ((namePrefix != NamePrefix.Local) && (namePrefix != NamePrefix.Global))
            {
                throw new InvalidEnumArgumentException(
                    "namePrefix", (int)namePrefix, typeof(NamePrefix));
            }

            const string DataReadyEventName = "DBWIN_DATA_READY";

            var eventHandle = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                namePrefix + @"\" + DataReadyEventName,
                out createdNew,
                eventSecurity);

            return eventHandle;
        }

        /// <summary>
        /// Creates a new <see cref="DebugMonitor"/>, acquiring the buffer that enables receiving data
        /// sent to the debug output using local prefix when creating named system objects
        /// for inter-process communication.
        /// </summary>
        /// <returns>
        /// The newly created <see cref="DebugMonitor"/>.
        /// </returns>
        public static DebugMonitor CreateLocalMonitor()
        {
            return CreateMonitor(NamePrefix.Local);
        }

        /// <summary>
        /// Creates a new <see cref="DebugMonitor"/>, acquiring the buffer that enables receiving data
        /// sent to the debug output using global prefix when creating named system objects
        /// for inter-process communication.
        /// </summary>
        /// <returns>
        /// The newly created <see cref="DebugMonitor"/>.
        /// </returns>
        public static DebugMonitor CreateGlobalMonitor()
        {
            return CreateMonitor(NamePrefix.Global);
        }

        /// <summary>
        /// Creates a new <see cref="DebugMonitor"/>, acquiring the buffer that enables receiving data
        /// sent to the debug output using the specified prefix when creating named system objects
        /// for inter-process communication.
        /// </summary>
        /// <returns>
        /// The newly created <see cref="DebugMonitor"/>.
        /// </returns>
        public static DebugMonitor CreateMonitor(NamePrefix namePrefix)
        {
            var buffer = DebugBuffer.AcquireBuffer(namePrefix);
            var monitor = new DebugMonitor(buffer);

            return monitor;
        }

        /// <summary>
        /// Removes a <see cref="DebugString"/> from this <see cref="DebugMonitor"/>.
        /// </summary>
        /// <returns>
        /// The <see cref="DebugString"/> removed from this <see cref="DebugMonitor"/>.
        /// </returns>
        public DebugString Take()
        {
            return this.Take(CancellationToken.None);
        }

        /// <summary>
        /// Removes a <see cref="DebugString"/> from the buffer contained by this
        /// <see cref="DebugMonitor"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> that can be used to cancel the take operation.
        /// </param>
        /// <returns>
        /// The <see cref="DebugString"/> removed from this <see cref="DebugMonitor"/>.
        /// </returns>
        public DebugString Take(CancellationToken cancellationToken)
        {
            DebugString value;
            bool taken = TryTake(out value, Timeout.Infinite, cancellationToken);
            Debug.Assert(taken, "taken is false");

            return value;
        }

        /// <summary>
        /// Attempts to remove a <see cref="DebugString" /> from the buffer contained by this
        /// <see cref="DebugMonitor"/>.
        /// </summary>
        /// <param name="value">
        /// When <see cref="TryTake(DebugString)"/> returns, contains the removed <see cref="DebugString"/>,
        /// if successful; otherwise, an empty <see cref="DebugString"/>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the <see cref="DebugString"/> has been removed; otherwise, <c>false</c>.
        /// </returns>
        public bool TryTake(out DebugString value)
        {
            return this.TryTake(out value, 0, CancellationToken.None);
        }

        /// <summary>
        /// Attempts to remove a <see cref="DebugString" /> from the buffer contained by this
        /// <see cref="DebugMonitor"/> within the specified time.
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
        public bool TryTake(out DebugString value, int timeout)
        {
            return this.TryTake(out value, timeout, CancellationToken.None);
        }

        /// <summary>
        /// Attempts to remove a <see cref="DebugString" /> from the buffer contained by this
        /// <see cref="DebugMonitor"/> within the specified time.
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
        public bool TryTake(out DebugString value, TimeSpan timeout)
        {
            return this.TryTake(out value, timeout, CancellationToken.None);
        }

        /// <summary>
        /// Attempts to remove a <see cref="DebugString" /> from the buffer contained by this
        /// <see cref="DebugMonitor"/> within the specified time while monitoring cancellation requests.
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
        /// <see cref="DebugMonitor"/> within the specified time while monitoring cancellation requests.
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
        public bool TryTake(out DebugString value, int timeout, CancellationToken cancellationToken)
        {
            if (!this.buffer.TryWaitForData(timeout, cancellationToken))
            {
                value = default(DebugString);
                return false;
            }

            int bytesRead;

            try
            {
                bytesRead = this.buffer.ReadData(this.data, 0, this.data.Length);
            }
            finally
            {
                this.buffer.RequestData();
            }

            value = DebugString.ToDebugString(this.data, 0, bytesRead);
            return true;
        }

        /// <summary>
        /// Asynchronously removes a <see cref="DebugString" /> from the buffer contained by this
        /// <see cref="DebugMonitor"/>.
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
        /// <see cref="DebugMonitor"/> while monitoring cancellation requests.
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

            await this.buffer.WaitForDataAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            int bytesRead;

            try
            {
                bytesRead = this.buffer.ReadData(this.data, 0, this.data.Length);
            }
            finally
            {
                this.buffer.RequestData();
            }

            DebugString value = DebugString.ToDebugString(this.data, 0, bytesRead);
            return value;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting
        /// unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            IDisposable d = this.buffer;

            if (d != null)
            {
                this.buffer = null;
                d.Dispose();
            }

            this.data = null;
        }
    }
}
