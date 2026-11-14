namespace Cyclo.Core.Parsing;

public class InvalidGpxException(string message, Exception innerException) : Exception(message, innerException);