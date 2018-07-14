namespace SwordsDance.Hls
{
    /// <summary>Defines an HLS playlist error.</summary>
    public class PlaylistError
    {
        /// <summary>
        /// Initializes a new <see cref="PlaylistError"/> instance with the specified values.
        /// </summary>
        /// <param name="description">The description of the error.</param>
        /// <param name="line">The line number of the error.</param>
        /// <param name="column">The line position of the error.</param>
        public PlaylistError(string description, int line, int column)
        {
            Description = description;
            Line = line;
            Column = column;
        }

        /// <summary>Gets the line position of the error.</summary>
        /// <remarks>
        /// The returned value reflects the 1-based index of the character where the error was encountered.
        /// </remarks>
        public int Column { get; }

        /// <summary>Gets the description of the error.</summary>
        public string Description { get; }

        /// <summary>Gets the line number of the error.</summary>
        /// <remarks>
        /// The returned value reflects the 1-based index of the line containing the character where the error
        /// was encountered.
        /// </remarks>
        public int Line { get; }

        /// <summary>Returns the string representation of the error.</summary>
        /// <returns>The string representation of the error.</returns>
        public override string ToString() => Description + " (" + Line + ", " + Column + ")";
    }
}
