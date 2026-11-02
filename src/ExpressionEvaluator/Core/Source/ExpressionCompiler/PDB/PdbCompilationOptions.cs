// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.Debugging;
using Microsoft.DiaSymReader;

namespace Microsoft.CodeAnalysis.ExpressionEvaluator
{
    /// <summary>
    /// Reads the compilation options that the compiler embeds in a portable PDB, such as "language-version".
    /// </summary>
    internal static class PdbCompilationOptions
    {
        /// <summary>
        /// The language version the module was compiled with, as the compiler records it, or null.
        /// </summary>
        public static string? TryGetLanguageVersion(object? symReader, int methodVersion)
            => TryGetValue(symReader, methodVersion, CompilationOptionNames.LanguageVersion);

        /// <summary>
        /// The value of <paramref name="name"/> in the compilation options of the PDB that <paramref name="symReader"/>
        /// reads, or null when the PDB is not portable, records no compilation options, or cannot be read.
        /// </summary>
        public static unsafe string? TryGetValue(object? symReader, int methodVersion, string name)
        {
            if (symReader is not ISymUnmanagedReader5 symReader5)
            {
                return null;
            }

            try
            {
                if (symReader5.GetPortableDebugMetadataByVersion(methodVersion, out byte* metadata, out int size) != 0)
                {
                    return null;
                }

                var reader = new MetadataReader(metadata, size);
                foreach (var handle in reader.GetCustomDebugInformation(EntityHandle.ModuleDefinition))
                {
                    var info = reader.GetCustomDebugInformation(handle);
                    if (reader.GetGuid(info.Kind) != PortableCustomDebugInfoKinds.CompilationOptions)
                    {
                        continue;
                    }

                    var blob = reader.GetBlobReader(info.Value);
                    while (blob.RemainingBytes > 0)
                    {
                        var key = ReadNullTerminatedUtf8(ref blob);
                        var value = ReadNullTerminatedUtf8(ref blob);
                        if (key == name)
                        {
                            return value;
                        }
                    }
                }
            }
            catch (Exception e) when (e is BadImageFormatException or COMException or NotSupportedException)
            {
            }

            return null;
        }

        private static string ReadNullTerminatedUtf8(ref BlobReader blob)
        {
            var length = blob.IndexOf(0);
            if (length < 0)
            {
                throw new BadImageFormatException();
            }

            var value = blob.ReadUTF8(length);
            blob.ReadByte();
            return value;
        }
    }
}
