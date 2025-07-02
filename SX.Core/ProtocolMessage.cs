using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SX.Core;

public static class ProtocolConstants
{
	public const string Data = "DATA:";
}

[JsonConverter(typeof(StringEnumConverter))]
public enum ProtocolCommand
{
	Send,    // sxd - remote sending file to local
	Request, // sxu - remote requesting file from local  
	ListDir, // sxls - remote listing available files
	Error    // Error response
}

public class ProtocolMessage
{
	public ProtocolCommand Command { get; set; }
	public string Filename { get; set; }
	public long? Size { get; set; }
	public string Path { get; set; }
	public string ErrorMessage { get; set; }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum EntryType
{
	File,
	Dir
}

public class DirectoryEntry
{
	public EntryType Type { get; set; }
	public string Name { get; set; } = "";
	public long Size { get; set; }
	public DateTime ModifyDate { get; set; }
}

public class DirectoryListing
{
	public List<DirectoryEntry> Entries { get; set; } = new();
}
