using System;

public enum ArchiveFormat
{
	Zip,
	Tgz,
}

public static class ArchiveFormatExtensions
{
	// Nuke's AbsolutePath.CompressTo picks the writer from this extension.
	public static string Extension(this ArchiveFormat format) => format switch
	{
		ArchiveFormat.Zip => ".zip",
		ArchiveFormat.Tgz => ".tgz",
		_ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown archive format"),
	};
}
