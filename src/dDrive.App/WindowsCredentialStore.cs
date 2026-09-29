using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using dDrive.Core.Security;

namespace dDrive.App;

public sealed class WindowsCredentialStore : ISecretStore
{
    private const uint GenericCredentialType = 1;
    private const uint LocalMachinePersistence = 2;
    private const int ErrorNotFound = 1168;
    private const int MaxCredentialBlobSize = 2560;

    public void Save(string target, SecureString secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(secret);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Credential Manager ist nur unter Windows verfügbar.");
        }

        var byteCount = checked(secret.Length * sizeof(char));
        if (byteCount > MaxCredentialBlobSize)
        {
            throw new ArgumentException("Das Passwort überschreitet die maximale Länge des Windows Credential Managers.", nameof(secret));
        }

        var secretPointer = IntPtr.Zero;
        var blobPointer = IntPtr.Zero;
        try
        {
            secretPointer = Marshal.SecureStringToGlobalAllocUnicode(secret);
            if (byteCount > 0)
            {
                blobPointer = Marshal.AllocHGlobal(byteCount);
                for (var offset = 0; offset < byteCount; offset += sizeof(char))
                {
                    Marshal.WriteInt16(blobPointer, offset, Marshal.ReadInt16(secretPointer, offset));
                }
            }

            var credential = new NativeCredential
            {
                Type = GenericCredentialType,
                TargetName = target,
                CredentialBlobSize = (uint)byteCount,
                CredentialBlob = blobPointer,
                Persist = LocalMachinePersistence
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            if (blobPointer != IntPtr.Zero)
            {
                for (var offset = 0; offset < byteCount; offset++)
                {
                    Marshal.WriteByte(blobPointer, offset, 0);
                }

                Marshal.FreeHGlobal(blobPointer);
            }

            if (secretPointer != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(secretPointer);
            }
        }
    }

    public SecureString? Read(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Credential Manager ist nur unter Windows verfügbar.");
        }

        if (!CredRead(target, GenericCredentialType, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return null;
            }

            throw new Win32Exception(error);
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlobSize > MaxCredentialBlobSize || credential.CredentialBlobSize % sizeof(char) != 0)
            {
                throw new InvalidDataException("Das gespeicherte Credential hat ein ungültiges Format.");
            }

            var secret = new SecureString();
            var characterCount = (int)credential.CredentialBlobSize / sizeof(char);
            for (var index = 0; index < characterCount; index++)
            {
                secret.AppendChar((char)Marshal.ReadInt16(credential.CredentialBlob, index * sizeof(char)));
            }

            secret.MakeReadOnly();
            return secret;
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public void Delete(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Credential Manager ist nur unter Windows verfügbar.");
        }

        if (!CredDelete(target, GenericCredentialType, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new Win32Exception(error);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = false)]
    private static extern void CredFree(IntPtr buffer);
}