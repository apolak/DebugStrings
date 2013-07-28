namespace DebugStrings
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines interface for receiving data sent to the debug output.
    /// </summary>
    public interface IDebugBuffer : IDisposable
    {
        /// <summary>
        /// Requests that the data be written to the underlying memory-mapped file.
        /// </summary>
        void RequestData();

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
        bool TryWaitForData(int timeoutMilliseconds, CancellationToken cancellationToken);

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
        Task WaitForDataAsync(CancellationToken cancellationToken);

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
        int ReadData(byte[] array, int offset, int count);
    }
}
