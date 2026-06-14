using Kerajel.Primitives.Enums;
using System.Runtime.InteropServices;

namespace Kerajel.Primitives.Models;

[StructLayout(LayoutKind.Sequential)]
public struct OperationResultInterop
{
    public nint Result;
    public OperationStatus OperationStatus;
    public nint ErrorMessage;
}