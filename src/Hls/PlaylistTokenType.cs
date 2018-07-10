namespace SwordsDance.Hls
{
    /// <summary>Specifies HLS playlist token types.</summary>
    public enum PlaylistTokenType
    {
        /// <summary>
        /// Returned by <see cref="PlaylistTextReader.TokenType"/> if data from the underlying playlist has yet to
        /// be read.
        /// </summary>
        None,

        /// <summary>A URI.</summary>
        Uri,

        /// <summary>A blank line.</summary>
        Blank,

        /// <summary>A comment.</summary>
        Comment,

        /// <summary>A tag name.</summary>
        TagName,

        /// <summary>A tag value.</summary>
        TagValue,

        /// <summary>An attribute name.</summary>
        AttributeName,

        /// <summary>A quoted attribute value.</summary>
        QuotedAttributeValue,

        /// <summary>An unquoted attribute value.</summary>
        UnquotedAttributeValue,
    }
}
