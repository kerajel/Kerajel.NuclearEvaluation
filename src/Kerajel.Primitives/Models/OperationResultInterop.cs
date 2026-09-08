using System.Runtime.InteropServices;
using Kerajel.Primitives.Enums;

namespace Kerajel.Primitives.Models;

[StructLayout(LayoutKind.Sequential)]
public struct OperationResultInterop
{
    public nint Result;
    public OperationStatus OperationStatus;
    public nint ErrorMessage;
}
