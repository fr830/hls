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

        /// <summary>A comment marker (<c>#</c>).</summary>
        CommentMarker,

        /// <summary>A comment.</summary>
        Comment,

        /// <summary>A tag name.</summary>
        TagName,

        /// <summary>A tag name/value separator (<c>:</c>).</summary>
        TagNameValueSeparator,

        /// <summary>A tag value.</summary>
        TagValue,

        /// <summary>An attribute name.</summary>
        AttributeName,

        /// <summary>An attribute name/value separator (<c>=</c>).</summary>
        AttributeNameValueSeparator,

        /// <summary>An unquoted attribute value.</summary>
        AttributeValue,

        /// <summary>A quoted attribute value marker (<c>"</c>).</summary>
        QuotedAttributeValueMarker,

        /// <summary>A quoted attribute value.</summary>
        QuotedAttributeValue,

        /// <summary>A quoted attribute value terminator (<c>"</c>).</summary>
        QuotedAttributeValueTerminator,

        /// <summary>Unexpected data.</summary>
        UnexpectedData,

        /// <summary>An attribute separator (<c>,</c>).</summary>
        AttributeSeparator,

        /// <summary>An end-of-line sequence (<c>LF</c> or <c>CR+LF</c>).</summary>
        EndOfLine,

        /// <summary>An end-of-file marker.</summary>
        EndOfFile,
    }
}
