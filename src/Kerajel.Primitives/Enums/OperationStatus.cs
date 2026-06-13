namespace Kerajel.Primitives.Enums;

public enum OperationStatus : short
{
    Unknown = 0,

    Succeeded = 200,
   
    NotFound = 404,
    Cancelled = 499,

    Error = 500,
}