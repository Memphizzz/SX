namespace SX.Core;

/// <summary>
///     Base exception for all SX transfer-related exceptions
/// </summary>
public abstract class SxTransferException : Exception
{
	protected SxTransferException(string message) : base(message) { }
	protected SxTransferException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
///     Thrown when a transfer is incomplete during a file download (client sending to server)
/// </summary>
public class TransferIncompleteException : SxTransferException
{
	public long BytesReceived { get; }
	public long ExpectedBytes { get; }

	public TransferIncompleteException(long bytesReceived, long expectedBytes)
		: base($"Transfer incomplete - received {bytesReceived} of {expectedBytes} bytes")
	{
		BytesReceived = bytesReceived;
		ExpectedBytes = expectedBytes;
	}
}
