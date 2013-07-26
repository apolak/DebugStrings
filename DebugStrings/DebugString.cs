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
        /// Writes the specified string value to the debug output.
        /// </summary>
        /// <param name="value">
        /// The value to write.
        /// </param>
        /// <exception cref="PlatformNotSupportedException">
        /// This operation is not supported on the current platform.
        /// </exception>
        [SecurityCritical]
        public static void Write(string value)
        {
            if (!IsPlatformSupported)
            {
                throw new PlatformNotSupportedException();
            }

            OutputDebugString(value);
        }

        /// <summary>
        /// Writes the text representation of the specified object to the debug output using the specified
        /// format information.
        /// </summary>
        /// <param name="format">
        /// The composite format string.
        /// </param>
        /// <param name="arg0">
        /// The object to write using <paramref name="format"/>.
        /// </param>
        [SecurityCritical]
        public static void Write(string format, object arg0)
        {
            string value = string.Format(format, arg0);
            Write(value);
        }

        /// <summary>
        /// Writes the text representation of the specified objects to the debug output using the specified
        /// format information.
        /// </summary>
        /// <param name="format">
        /// The composite format string.
        /// </param>
        /// <param name="arg0">
        /// The first object to write using <paramref name="format"/>.
        /// </param>
        /// <param name="arg1">
        /// The second object to write using <paramref name="format"/>.
        /// </param>
        [SecurityCritical]
        public static void Write(string format, object arg0, object arg1)
        {
            string value = string.Format(format, arg0, arg1);
            Write(value);
        }

        /// <summary>
        /// Writes the text representation of the specified objects to the debug output using the specified
        /// format information.
        /// </summary>
        /// <param name="format">
        /// The composite format string.
        /// </param>
        /// <param name="arg0">
        /// The first object to write using <paramref name="format"/>.
        /// </param>
        /// <param name="arg1">
        /// The second object to write using <paramref name="format"/>.
        /// </param>
        /// <param name="arg2">
        /// The third object to write using <paramref name="format"/>.
        /// </param>
        [SecurityCritical]
        public static void Write(string format, object arg0, object arg1, object arg2)
        {
            string value = string.Format(format, arg0, arg1, arg2);
            Write(value);
        }

        /// <summary>
        /// Writes the text representation of the specified array of objects to the debug output using
        /// the specified format information.
        /// </summary>
        /// <param name="format">
        /// The composite format string.
        /// </param>
        /// <param name="args">
        /// The array of objects to write using <paramref name="format"/>.
        /// </param>
        [SecurityCritical]
        public static void Write(string format, params object[] args)
        {
            string value = string.Format(format, args);
            Write(value);
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
