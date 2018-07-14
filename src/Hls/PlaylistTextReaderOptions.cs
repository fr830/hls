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
        /// Specifies that <see cref="PlaylistTextReader.Read"/> and derivative methods should emit all tokens read,
        /// including markers, separators, terminators and end-of-line sequences.
        /// </summary>
        Verbose = 1 << 0,
    }
}
