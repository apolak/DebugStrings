﻿namespace DebugStrings
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Text;

    /// <summary>
    /// Defines the unique identifier of the process that has sent data to the debug output
    /// and the text representation of the object that has been sent.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public struct DebugString : IEquatable<DebugString>
    {
        /// <summary>
        /// The value indicating whether operations performed by the <see cref="DebugString"/> class
        /// are supported on the current platform.
        /// </summary>
        public static readonly bool IsPlatformSupported = CheckIsPlatformSupported();

        /// <summary>
        /// The unique identifier of the process that has sent data to the debug output.
        /// </summary>
        private readonly int processId;

        /// <summary>
        /// The text representation of the object that has been sent to the debug output.
        /// </summary>
        private readonly string value;

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugString"/> struct with
        /// the specified unique identifier of the process and the string value.
        /// </summary>
        /// <param name="processId">
        /// The unique identifier of the process that has sent the data.
        /// </param>
        /// <param name="value">
        /// The text representation of the sent object.
        /// </param>
        public DebugString(int processId, string value)
        {
            this.processId = processId;
            this.value = value;
        }

        /// <summary>
        /// Gets the unique identifier of the process that has sent the data
        /// to the debug output.
        /// </summary>
        public int ProcessId
        {
            get { return this.processId; }
        }

        /// <summary>
        /// Gets the text representation of the object sent to the debug output.
        /// </summary>
        public string Value
        {
            get { return this.value; }
        }

        /// <summary>
        /// Gets the text representation of this <see cref="DebugData"/> instance for displaying
        /// in the debugger.
        /// </summary>
        private string DebuggerDisplay
        {
            get { return string.Format("{{ProcessId:{0}, Value:\"{1}\"}", this.processId, this.value); }
        }

        /// <summary>
        /// Converts the <see cref="DebugString"/> to a string.
        /// </summary>
        /// <param name="debugString">
        /// The <see cref="DebugString"/> to convert.
        /// </param>
        /// <returns>
        /// The string constructed from <paramref cref="value" />.
        /// </returns>
        public static explicit operator string(DebugString debugString)
        {
            return debugString.value;
        }

        /// <summary>
        /// Determines whether two <see cref="DebugString"/>s are equal.
        /// </summary>
        /// <param name="left">
        /// The first value to compare.
        /// </param>
        /// <param name="right">
        /// The second value to compare.
        /// </param>
        /// <returns>
        /// <c>true</c> if <paramref name="left"/> is equal to <paramref name="right"/>; otherwise,
        /// <c>false</c>.
        /// </returns>
        public static bool operator ==(DebugString left, DebugString right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two <see cref="DebugString"/>s are different.
        /// </summary>
        /// <param name="left">
        /// The first value to compare.
        /// </param>
        /// <param name="right">
        /// The second value to compare.
        /// </param>
        /// <returns>
        /// <c>true</c> if <paramref name="left"/> is different from <paramref name="right"/>;
        /// otherwise, <c>false</c>.
        /// </returns>
        public static bool operator !=(DebugString left, DebugString right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Returns a <see cref="DebugString"/> converted from the specified range of bytes within
        /// the specified array.
        /// </summary>
        /// <param name="array">
        /// The array of bytes.
        /// </param>
        /// <param name="offset">
        /// The starting position within <paramref name="array"/>.
        /// </param>
        /// <param name="count">
        /// The number of bytes within <paramref name="array"/> to convert.
        /// </param>
        /// <returns>
        /// The <see cref="DebugString"/> formed by <paramref name="count"/> bytes beginning
        /// at <paramref name="offset"/> within <paramref name="array"/>.
        /// </returns>
        public static DebugString ToDebugString(byte[] array, int offset, int count)
        {
            if (count < sizeof(int))
            {
                throw new InvalidDataException("Data length in bytes must greater than or equal to 4, actual data length is " + count + ".");
            }

            // CORRECTNESS Skip the first 4 bytes in the specified range that indicate the process ID when searching for the null terminator.
            int terminator = Array.IndexOf(array, (byte)0, offset + sizeof(int), count - sizeof(int));
            Debug.Assert((terminator < 0) || (terminator >= sizeof(int)), "terminator is between 0 and 3");

            if (terminator < 0)
            {
                // ROBUSTNESS Null terminator not found, assume it is placed after the last byte in the specified range.
                terminator = offset + count;
            }

            int processId = BitConverter.ToInt32(array, 0);
            string value = Encoding.Default.GetString(array, offset + sizeof(int), terminator - offset - sizeof(int));

            return new DebugString(processId, value);
        }

        /// <summary>
        /// Sends the specified string value to the debug output.
        /// </summary>
        /// <param name="value">
        /// The value to send.
        /// </param>
        /// <exception cref="PlatformNotSupportedException">
        /// This operation is not supported on the current platform.
        /// </exception>
        [SecurityCritical]
        public static void Send(string value)
        {
            if (!IsPlatformSupported)
            {
                throw new PlatformNotSupportedException();
            }

            OutputDebugString(value);
        }

        /// <summary>
        /// Sends the text representation of the specified object to the debug output.
        /// </summary>
        /// <param name="value">
        /// The value to send.
        /// </param>
        /// <exception cref="PlatformNotSupportedException">
        /// This operation is not supported on the current platform.
        /// </exception>
        public static void Send<TValue>(TValue value)
        {
            if (value == null)
            {
                Send(null);
            }
            else
            {
                Send(value.ToString());
            }
        }

        /// <summary>
        /// Sends the text representation of the specified object to the debug output using the specified
        /// format information.
        /// </summary>
        /// <param name="format">
        /// The composite format string.
        /// </param>
        /// <param name="arg0">
        /// The object to send using <paramref name="format"/>.
        /// </param>
        /// <exception cref="PlatformNotSupportedException">
        /// This operation is not supported on the current platform.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="format"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="FormatException">
        /// <paramref name="format"/> is invalid.-or- The index of a format item is not zero.
        /// </exception>
        public static void Send(string format, object arg0)
        {
            string value = string.Format(format, arg0);
            Send(value);
        }

        /// <summary>
        /// Sends the text representation of the specified objects to the debug output using the specified
        /// format information.
        /// </summary>
        /// <param name="format">
        /// The composite format string.
        /// </param>
        /// <param name="arg0">
        /// The first object to send using <paramref name="format"/>.
        /// </param>
        /// <param name="arg1">
        /// The second object to send using <paramref name="format"/>.
        /// </param>
        /// <exception cref="PlatformNotSupportedException">
        /// This operation is not supported on the current platform.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="format"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="FormatException">
        /// <paramref name="format"/> is invalid.-or- The index of a format item is not zero or one.
        /// </exception>
        public static void Send(string format, object arg0, object arg1)
        {
            string value = string.Format(format, arg0, arg1);
            Send(value);
        }

        /// <summary>
        /// Sends the text representation of the specified objects to the debug output using the specified
        /// format information.
        /// </summary>
        /// <param name="format">
        /// The composite format string.
        /// </param>
        /// <param name="arg0">
        /// The first object to send using <paramref name="format"/>.
        /// </param>
        /// <param name="arg1">
        /// The second object to send using <paramref name="format"/>.
        /// </param>
        /// <param name="arg2">
        /// The third object to send using <paramref name="format"/>.
        /// </param>
        /// <exception cref="PlatformNotSupportedException">
        /// This operation is not supported on the current platform.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="format"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="FormatException">
        /// <paramref name="format"/> is invalid.-or- The index of a format item is less than zero,
        /// or greater than two.
        /// </exception>
        public static void Send(string format, object arg0, object arg1, object arg2)
        {
            string value = string.Format(format, arg0, arg1, arg2);
            Send(value);
        }

        /// <summary>
        /// Sends the text representation of the specified array of objects to the debug output using
        /// the specified format information.
        /// </summary>
        /// <param name="format">
        /// The composite format string.
        /// </param>
        /// <param name="arg">
        /// The array of objects to send using <paramref name="format"/>.
        /// </param>
        /// <exception cref="PlatformNotSupportedException">
        /// This operation is not supported on the current platform.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="format"/> or <paramref name="arg"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="FormatException">
        /// <paramref name="format"/> is invalid.-or- The index of a format item is less than zero,
        /// or greater than or equal to the length of the <paramref name="arg" /> array.
        /// </exception>
        public static void Send(string format, params object[] arg)
        {
            string value = string.Format(format, arg);
            Send(value);
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

        /// <summary>
        /// Sends a string to the debugger for display.
        /// </summary>
        /// <param name="lpOutputString">
        /// The null-terminated string to be displayed.
        /// </param>
        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private extern static void OutputDebugString(string lpOutputString);

        /// <summary>
        /// Determines whether this <see cref="DebugString" /> is equal to the specified
        /// <see cref="DebugString" />.
        /// </summary>
        /// <param name="obj">
        /// The <see cref="DebugString" /> to compare with this <see cref="DebugString" />.
        /// </param>
        /// <returns>
        /// <c>true</c> if this <see cref="DebugString"/> is equal to <paramref name="obj" />;
        /// otherwise, false.
        /// </returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            return (obj is DebugString) && this.Equals((DebugString)obj);
        }

        /// <summary>
        /// Determines whether this <see cref="DebugString" /> is equal to the specified
        /// <see cref="DebugString" />.
        /// </summary>
        /// <param name="other">
        /// The <see cref="DebugString" /> to compare with this <see cref="DebugString" />.
        /// </param>
        /// <returns>
        /// <c>true</c> if this <see cref="DebugString"/> is equal to <paramref name="other" />;
        /// otherwise, false.
        /// </returns>
        public bool Equals(DebugString other)
        {
            return (this.processId == other.processId) && string.Equals(this.value, other.value);
        }

        /// <summary>
        /// Gets the hash code for this <see cref="DebugString"/>.
        /// </summary>
        /// <returns>
        /// The hash code for this <see cref="DebugString"/>.
        /// </returns>
        public override int GetHashCode()
        {
            return (this.value != null) ? this.processId ^ this.value.GetHashCode() : this.processId;
        }

        /// <summary>
        /// Gets the text representation of the object that has been sent to the debug output.
        /// </summary>
        /// <returns>
        /// The text representation of the sent object.
        /// </returns>
        public override string ToString()
        {
            return "[" + this.processId + "] " + this.value;
        }
    }
}
