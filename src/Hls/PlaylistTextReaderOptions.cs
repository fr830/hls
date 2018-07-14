using System;

namespace SwordsDance.Hls
{
    /// <summary>Specifies options for <see cref="PlaylistTextReader"/>.</summary>
    [Flags]
    public enum PlaylistTextReaderOptions
    {
        /// <summary>Specifies that all options should be disabled.</summary>
        None = 0,

        /// <summary>
        /// Specifies that <see cref="PlaylistTextReader.Read"/> should emit all tokens it reads, including markers,
        /// separators, terminators and end-of-line sequences.
        /// </summary>
        Verbose = 1 << 0,

        /// <summary>The default set of options used by <see cref="PlaylistTextReader"/> instances.</summary>
        Default = Verbose,
    }
}
