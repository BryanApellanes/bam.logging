namespace Bam.Logging
{
    /// <summary>
    /// Specifies the schema type used for log storage.
    /// </summary>
    public enum LogSchemaType
    {
        /// <summary>
        /// An invalid or unset schema type.
        /// </summary>
        Invalid,
        /// <summary>
        /// A flat schema where log entries are stored as simple records.
        /// </summary>
        Flat,
        /// <summary>
        /// A schema that captures relationships between log events.
        /// </summary>
        EventRelationships
    }
}
