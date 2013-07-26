namespace DebugStrings
{
    using System;
    using System.Runtime.InteropServices;
using System.Security;

    /// <summary>
    /// Provides static methods for writing debug strings.
    /// </summary>
    public static class DebugString
    {
        /// <summary>
        /// The value indicating whether operations performed by the <see cref="DebugString"/> class are supported
        /// on the current platform.
        /// </summary>
        public static readonly bool IsPlatformSupported = CheckIsPlatformSupported();

        /// <summary>
        /// Sends a string to the debugger for display.
        /// </summary>
        /// <param name="lpOutputString">
        /// The null-terminated string to be displayed.
        /// </param>
        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private extern static void OutputDebugString(string lpOutputString);

        /// <summary>
        /// Writes the specified text to the debug output.
        /// </summary>
        /// <param name="text">
        /// The text to write to the debug output.
        /// </param>
        /// <exception cref="PlatformNotSupportedException">
        /// This operation is not supported on the current platform.
        /// </exception>
        [SecurityCritical]
        public static void Write(string text)
        {
            if (!IsPlatformSupported)
            {
                throw new PlatformNotSupportedException();
            }

            OutputDebugString(text);
        }

        /// <summary>
        /// Determines whether the operations performed by the <see cref="DebugString"/> class are supported
        /// on the current platform.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the operations performed by the <see cref="DebugString"/> class are supported
        /// on the current platform; otherwise, <c>false</c>.
        /// </returns>
        [SecurityCritical]
        private static bool CheckIsPlatformSupported()
        {
            try
            {
                OutputDebugString(null);
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }

            return true;
        }
    }
}
