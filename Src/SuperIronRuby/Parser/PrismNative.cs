using System.Runtime.InteropServices;

namespace SuperIronRuby.Parser;

/// <summary>
/// P/Invoke bindings to libprism (prism v1.8.1). The native library is copied
/// next to the managed assembly by the csproj; a resolver also looks in the
/// assembly base directory so it is found regardless of the OS search path.
/// </summary>
internal static class PrismNative
{
    private const string Lib = "prism";

    static PrismNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(PrismNative).Assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != Lib) return IntPtr.Zero;

        foreach (var name in new[] { "libprism.so", "libprism.dylib", "prism.dll", "libprism.dll" })
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, name);
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
                return handle;
        }

        // Fall back to the default OS resolution.
        return NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var h) ? h : IntPtr.Zero;
    }

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern nuint pm_buffer_sizeof();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool pm_buffer_init(IntPtr buffer);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr pm_buffer_value(IntPtr buffer);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern nuint pm_buffer_length(IntPtr buffer);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void pm_buffer_free(IntPtr buffer);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void pm_serialize_parse(IntPtr buffer, IntPtr source, nuint size, IntPtr data);

    /// <summary>
    /// Parses <paramref name="sourceUtf8"/> and returns Prism's serialized binary
    /// AST (the same bytes as <c>Prism.dump</c>). Throws if the native library
    /// cannot be loaded.
    /// </summary>
    public static byte[] SerializeParse(byte[] sourceUtf8)
    {
        nuint structSize = pm_buffer_sizeof();
        IntPtr buffer = Marshal.AllocHGlobal((int)structSize);
        IntPtr sourcePtr = IntPtr.Zero;
        try
        {
            // Zero the struct then initialize (pm_buffer_init zeroes length/capacity
            // and allocates the backing store lazily).
            for (int i = 0; i < (int)structSize; i++) Marshal.WriteByte(buffer, i, 0);
            if (!pm_buffer_init(buffer))
                throw new InvalidOperationException("pm_buffer_init failed");

            sourcePtr = Marshal.AllocHGlobal(Math.Max(1, sourceUtf8.Length));
            Marshal.Copy(sourceUtf8, 0, sourcePtr, sourceUtf8.Length);

            pm_serialize_parse(buffer, sourcePtr, (nuint)sourceUtf8.Length, IntPtr.Zero);

            IntPtr valuePtr = pm_buffer_value(buffer);
            int length = (int)pm_buffer_length(buffer);
            var result = new byte[length];
            Marshal.Copy(valuePtr, result, 0, length);
            return result;
        }
        finally
        {
            pm_buffer_free(buffer);
            Marshal.FreeHGlobal(buffer);
            if (sourcePtr != IntPtr.Zero) Marshal.FreeHGlobal(sourcePtr);
        }
    }
}
