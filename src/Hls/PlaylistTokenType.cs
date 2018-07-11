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

        /// <summary>A URI or blank line.</summary>
        Uri,

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

        /// <summary>A quoted attribute value.</summary>
        QuotedAttributeValue,

        /// <summary>An unquoted attribute value.</summary>
        UnquotedAttributeValue,

        /// <summary>An attribute separator (<c>,</c>).</summary>
        AttributeSeparator,

        /// <summary>A line value terminator (LF or CR+LF).</summary>
        LineValueTerminator,
    }
}
