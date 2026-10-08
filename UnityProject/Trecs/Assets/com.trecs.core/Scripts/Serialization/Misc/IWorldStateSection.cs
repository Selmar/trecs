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
        /// <summary>Scope name in flat-path dumps.</summary>
        string Name { get; }

        void Serialize(ISerializationWriter writer);

        void Deserialize(ISerializationReader reader);
    }
}
