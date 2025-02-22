﻿//-------------------------------------------------------------------------------------------------
// <copyright file="Engine.cs" company="Outercurve Foundation">
//   Copyright (c) 2004, Outercurve Foundation.
//   This software is released under Microsoft Reciprocal License (MS-RL).
//   The license and further copyright text can be found in the file
//   LICENSE.TXT at the root directory of the distribution.
// </copyright>
// 
// <summary>
// Container class for the IBurnCore interface passed to the IBurnUserExperience.
// </summary>
//-------------------------------------------------------------------------------------------------

namespace Microsoft.Tools.WindowsInstallerXml.Bootstrapper
{
    using System;
    using System.ComponentModel;
    using System.Text;

    /// <summary>
    /// Container class for the <see cref="IBootstrapperEngine"/> interface.
    /// </summary>
    public sealed class Engine
    {
        // Burn errs on empty strings, so declare initial buffer size.
        private const int InitialBufferSize = 80;

        private IBootstrapperEngine engine;
        private Variables<long> numericVariables;
        private Variables<string> stringVariables;
        private Variables<Version> versionVariables;

        /// <summary>
        /// Creates a new instance of the <see cref="Engine"/> container class.
        /// </summary>
        /// <param name="engine">The <see cref="IBootstrapperEngine"/> to contain.</param>
        internal Engine(IBootstrapperEngine engine)
        {
            this.engine = engine;

            // Wrap the calls to get and set numeric variables.
            this.numericVariables = new Variables<long>(
                delegate(string name)
                {
                    long value;
                    int ret = this.engine.GetVariableNumeric(name, out value);
                    if (NativeMethods.S_OK != ret)
                    {
                        throw new Win32Exception(ret);
                    }

                    return value;
                },
                delegate(string name, long value)
                {
                    this.engine.SetVariableNumeric(name, value);
                },
                delegate(string name)
                {
                    long value;
                    int ret = this.engine.GetVariableNumeric(name, out value);

                    return NativeMethods.E_NOTFOUND != ret;
                }
            );

            // Wrap the calls to get and set string variables.
            this.stringVariables = new Variables<string>(
                delegate(string name)
                {
                    int capacity = InitialBufferSize;
                    StringBuilder sb = new StringBuilder(capacity);

                    // Get the size of the buffer.
                    int ret = this.engine.GetVariableString(name, sb, ref capacity);
                    if (NativeMethods.E_INSUFFICIENT_BUFFER == ret || NativeMethods.E_MOREDATA == ret)
                    {
                        sb.Capacity = ++capacity; // Add one for the null terminator.
                        ret = this.engine.GetVariableString(name, sb, ref capacity);
                    }

                    if (NativeMethods.S_OK != ret)
                    {
                        throw new Win32Exception(ret);
                    }

                    return sb.ToString();
                },
                delegate(string name, string value)
                {
                    this.engine.SetVariableString(name, value);
                },
                delegate(string name)
                {
                    int capacity = InitialBufferSize;
                    StringBuilder sb = new StringBuilder(capacity);
                    int ret = this.engine.GetVariableString(name, sb, ref capacity);

                    return NativeMethods.E_NOTFOUND != ret;
                }
            );

            // Wrap the calls to get and set version variables.
            this.versionVariables = new Variables<Version>(
                delegate(string name)
                {
                    long value;
                    int ret = this.engine.GetVariableVersion(name, out value);
                    if (NativeMethods.S_OK != ret)
                    {
                        throw new Win32Exception(ret);
                    }

                    int major = (int)((value & ((long)0xffff << 48)) >> 48);
                    int minor = (int)((value & ((long)0xffff << 32)) >> 32);
                    int build = (int)((value & ((long)0xffff << 16)) >> 16);
                    int revision = (int)(value & 0xffff);

                    return new Version(major, minor, build, revision);
                },
                delegate(string name, Version value)
                {
                    // In Windows, each version component has a max value of 65535,
                    // so we truncate the version before shifting it, which will overflow if invalid.
                    long major = (long)(ushort)value.Major << 48;
                    long minor = (long)(ushort)value.Minor << 32;
                    long build = (long)(ushort)value.Build << 16;
                    long revision = (long)(ushort)value.Revision;

                    long version = major | minor | build | revision;
                    this.engine.SetVariableVersion(name, version);
                },
                delegate(string name)
                {
                    long value;
                    int ret = this.engine.GetVariableVersion(name, out value);

                    return NativeMethods.E_NOTFOUND != ret;
                }
            );
        }

        /// <summary>
        /// Gets or sets numeric variables for the engine.
        /// </summary>
        public Variables<long> NumericVariables
        {
            get { return this.numericVariables; }
        }

        /// <summary>
        /// Gets the number of packages in the bundle.
        /// </summary>
        public int PackageCount
        {
            get
            {
                int count;
                this.engine.GetPackageCount(out count);

                return count;
            }
        }

        /// <summary>
        /// Gets or sets string variables for the engine.
        /// </summary>
        public Variables<string> StringVariables
        {
            get { return this.stringVariables; }
        }

        /// <summary>
        /// Gets or sets <see cref="Version"/> vaiables for the engine.
        /// </summary>
        public Variables<Version> VersionVariables
        {
            get { return this.versionVariables; }
        }

        /// <summary>
        /// Install the packages.
        /// </summary>
        /// <param name="hwndParent">The parent window for the installation user interface.</param>
        public void Apply(IntPtr hwndParent)
        {
            this.engine.Apply(hwndParent);
        }

        /// <summary>
        /// Close the splash screen if it is still open. Does nothing if the splash screen is not or
        /// never was opened.
        /// </summary>
        public void CloseSplashScreen()
        {
            this.engine.CloseSplashScreen();
        }

        /// <summary>
        /// Determine if all installation conditions are fulfilled.
        /// </summary>
        public void Detect()
        {
            this.engine.Detect();
        }

        /// <summary>
        /// Elevate the install.
        /// </summary>
        /// <param name="hwndParent">The parent window of the elevation dialog.</param>
        /// <returns>true if elevation succeeded; otherwise, false if the user cancelled.</returns>
        /// <exception cref="Win32Exception">A Win32 error occured.</exception>
        public bool Elevate(IntPtr hwndParent)
        {
            int ret = this.engine.Elevate(hwndParent);

            if (NativeMethods.S_OK == ret || NativeMethods.E_ALREADYINITIALIZED == ret)
            {
                return true;
            }
            else if (NativeMethods.E_CANCELLED == ret)
            {
                return false;
            }
            else
            {
                throw new Win32Exception(ret);
            }
        }

        /// <summary>
        /// Escapes the input string.
        /// </summary>
        /// <param name="input">The string to escape.</param>
        /// <returns>The escaped string.</returns>
        /// <exception cref="Win32Exception">A Win32 error occured.</exception>
        public string EscapeString(string input)
        {
            int capacity = InitialBufferSize;
            StringBuilder sb = new StringBuilder(capacity);

            // Get the size of the buffer.
            int ret = this.engine.EscapeString(input, sb, ref capacity);
            if (NativeMethods.E_INSUFFICIENT_BUFFER == ret || NativeMethods.E_MOREDATA == ret)
            {
                sb.Capacity = ++capacity; // Add one for the null terminator.
                ret = this.engine.EscapeString(input, sb, ref capacity);
            }

            if (NativeMethods.S_OK != ret)
            {
                throw new Win32Exception(ret);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Evaluates the <paramref name="condition"/> string.
        /// </summary>
        /// <param name="condition">The string representing the condition to evaluate.</param>
        /// <returns>Whether the condition evaluated to true or false.</returns>
        public bool EvaluateCondition(string condition)
        {
            bool value;
            this.engine.EvaluateCondition(condition, out value);

            return value;
        }

        /// <summary>
        /// Formats the input string.
        /// </summary>
        /// <param name="format">The string to format.</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="Win32Exception">A Win32 error occured.</exception>
        public string FormatString(string format)
        {
            int capacity = InitialBufferSize;
            StringBuilder sb = new StringBuilder(capacity);

            // Get the size of the buffer.
            int ret = this.engine.FormatString(format, sb, ref capacity);
            if (NativeMethods.E_INSUFFICIENT_BUFFER == ret || NativeMethods.E_MOREDATA == ret)
            {
                sb.Capacity = ++capacity; // Add one for the null terminator.
                ret = this.engine.FormatString(format, sb, ref capacity);
            }

            if (NativeMethods.S_OK != ret)
            {
                throw new Win32Exception(ret);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Logs the <paramref name="message"/>.
        /// </summary>
        /// <param name="level">The logging level.</param>
        /// <param name="message">The message to log.</param>
        public void Log(LogLevel level, string message)
        {
            this.engine.Log(level, message);
        }

        /// <summary>
        /// Determine the installation sequencing and costing.
        /// </summary>
        /// <param name="action">The action to perform when planning.</param>
        public void Plan(LaunchAction action)
        {
            this.engine.Plan(action);
        }

        /// <summary>
        /// Set the update information for a bundle.
        /// </summary>
        /// <param name="localSource">Optional local source path for the update. Default is "update\[OriginalNameOfBundle].exe".</param>
        /// <param name="downloadSource">Optional download source for the update.</param>
        /// <param name="size">Size of the expected update.</param>
        /// <param name="hashType">Type of the hash expected on the update.</param>
        /// <param name="hash">Optional hash expected for the update.</param>
        public void SetUpdate(string localSource, string downloadSource, long size, UpdateHashType hashType, byte[] hash)
        {
            this.engine.SetUpdate(localSource, downloadSource, size, hashType, hash, null == hash ? 0 : hash.Length);
        }

        /// <summary>
        /// Set the local source for a package or container.
        /// </summary>
        /// <param name="packageOrContainerId">The id that uniquely identifies the package or container.</param>
        /// <param name="payloadId">The id that uniquely identifies the payload.</param>
        /// <param name="path">The new source path.</param>
        public void SetLocalSource(string packageOrContainerId, string payloadId, string path)
        {
            this.engine.SetLocalSource(packageOrContainerId, payloadId, path);
        }

        /// <summary>
        /// Set the new download URL for a package or container.
        /// </summary>
        /// <param name="packageOrContainerId">The id that uniquely identifies the package or container.</param>
        /// <param name="payloadId">The id that uniquely identifies the payload.</param>
        /// <param name="url">The new url.</param>
        /// <param name="user">The user name for proxy authentication.</param>
        /// <param name="password">The password for proxy authentication.</param>
        public void SetDownloadSource(string packageOrContainerId, string payloadId, string url, string user, string password)
        {
            this.engine.SetDownloadSource(packageOrContainerId, payloadId, url, user, password);
        }

        /// <summary>
        /// Sends error message when embedded.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        /// <param name="message">Error message.</param>
        /// <param name="uiHint">UI buttons to show on error dialog.</param>
        public int SendEmbeddedError(int errorCode, string message, int uiHint)
        {
            int result = 0;
            this.engine.SendEmbeddedError(errorCode, message, uiHint, out result);
            return result;
        }

        /// <summary>
        /// Sends progress percentages when embedded.
        /// </summary>
        /// <param name="progressPercentage">Percentage completed thus far.</param>
        /// <param name="overallPercentage">Overall precentage completed.</param>
        public int SendEmbeddedProgress(int progressPercentage, int overallPercentage)
        {
            int result = 0;
            this.engine.SendEmbeddedProgress(progressPercentage, overallPercentage, out result);
            return result;
        }

        /// <summary>
        /// Shuts down the engine.
        /// </summary>
        /// <param name="exitCode">Exit code indicating reason for shut down.</param>
        public void Quit(int exitCode)
        {
            this.engine.Quit(exitCode);
        }

        /// <summary>
        /// An accessor for numeric, string, and version variables for the engine.
        /// </summary>
        public sealed class Variables<T>
        {
            // .NET 2.0 does not support Func<T, TResult> or Action<T1, T2>.
            internal delegate T Getter<T>(string name);
            internal delegate void Setter<T>(string name, T value);

            private Getter<T> getter;
            private Setter<T> setter;
            private Predicate<string> contains;

            internal Variables(Getter<T> getter, Setter<T> setter, Predicate<string> contains)
            {
                this.getter = getter;
                this.setter = setter;
                this.contains = contains;
            }

            /// <summary>
            /// Gets or sets the variable given by <paramref name="name"/>.
            /// </summary>
            /// <param name="name">The name of the variable to set.</param>
            /// <returns>The value of the given variable.</returns>
            /// <exception cref="Win32Exception">An error occured getting the variable.</exception>
            public T this[string name]
            {
                get { return this.getter(name); }
                set { this.setter(name, value); }
            }

            /// <summary>
            /// Gets whether the variable given by <paramref name="name"/> exists.
            /// </summary>
            /// <param name="name">The name of the variable to check.</param>
            /// <returns>True if the variable given by <paramref name="name"/> exists; otherwise, false.</returns>
            public bool Contains(string name)
            {
                return this.contains(name);
            }
        }
    }
}
