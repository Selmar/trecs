namespace Trecs
{
    /// <summary>
    /// Simulation state that lives outside the world's components and heaps
    /// (a system's containers, a queue between systems) but must still travel
    /// with a snapshot. Register one via <see cref="World.AddStateSection"/>.
    ///
    /// Sections are written after the heaps, in registration order, and read
    /// back in that order before <c>OnDeserializeCompleted</c> fires. Every
    /// snapshot, recording, checksum and desync dump goes through them, like a
    /// component array. The format is positional: <see cref="Deserialize"/>
    /// must read exactly what <see cref="Serialize"/> wrote.
    /// </summary>
    public interface IWorldStateSection
    {
        /// <summary>
        /// Identifies the section: its hash is written ahead of its data and
        /// checked on load, and it scopes the section in flat-path dumps.
        /// Unique per world, and stable for as long as saved snapshots must
        /// load.
        /// </summary>
        string Name { get; }

        void Serialize(ISerializationWriter writer);

        void Deserialize(ISerializationReader reader);
    }
}
