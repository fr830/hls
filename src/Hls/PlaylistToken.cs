namespace SwordsDance.Hls
{
    /// <summary>Defines an HLS playlist token.</summary>
    public class PlaylistToken
    {
        /// <summary>
        /// Initializes a new <see cref="PlaylistToken"/> instance with the specified type and value.
        /// </summary>
        /// <param name="type">The type of the token.</param>
        /// <param name="value">The value of the token.</param>
        /// <param name="line">The line number of the token.</param>
        /// <param name="column">The character position of the token.</param>
        public PlaylistToken(PlaylistTokenType type, string value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }

        /// <summary>Gets the character position of the token.</summary>
        /// <remarks>
        /// The returned value reflects the 1-based index of the first character of the value of the token.
        /// </remarks>
        public int Column { get; }

        /// <summary>Gets the line number of the token.</summary>
        /// <remarks>
        /// The returned value reflects the 1-based index of the line containing the first character of the value of
        /// the token.
        /// </remarks>
        public int Line { get; }

        /// <summary>Gets the type of the token.</summary>
        public PlaylistTokenType Type { get; }

        /// <summary>Gets the value of the token.</summary>
        public string Value { get; }

        /// <summary>Returns the string representation of the token.</summary>
        /// <returns>The string representation of the token.</returns>
        public override string ToString() => "[" + Type + "] " + Value + " (" + Line + ", " + Column + ")";
    }
}
