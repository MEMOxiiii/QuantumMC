using BedrockProtocol.Utils;

namespace BedrockProtocol.Types
{
    /// <summary>
    /// Serializes a full chunk column into the payload format expected by LevelChunkPacket.
    /// 
    /// Payload structure:
    ///   [SubChunk data for each non-empty sub-chunk, bottom to top]
    ///   [Biome data — palette-based, one palette per sub-chunk section]
    ///   [Border blocks — single byte 0x00]
    /// </summary>
    public static class ChunkSerializer
    {
        /// <summary>
        /// Serializes a chunk column and returns the payload bytes and the sub-chunk count.
        /// </summary>
        /// <param name="subChunks">Array of sub-chunk data (from bottom to top), or null for empty sub-chunks.</param>
        /// <param name="defaultAirId">The runtime ID for air used in empty sub-chunks.</param>
        /// <returns>Tuple of (payload bytes, subChunkCount).</returns>
        // Bedrock overworld biome section count: 24 sub-chunk sections + 1 extra above the world.
        private const int BiomeSectionCount = 25;

        public static (byte[] Payload, int SubChunkCount) Serialize(SubChunkData?[] subChunks, int defaultAirId, int biomeId = 1 /* plains */)
        {
            var stream = new BinaryStream();

            int subChunkCount = FindTopSubChunkIndex(subChunks) + 1;

            for (int i = 0; i < subChunkCount; i++)
            {
                if (subChunks[i] != null)
                {
                    subChunks[i]!.WriteTo(stream);
                }
                else
                {
                    WriteEmptySubChunk(stream, i - 4, defaultAirId);
                }
            }

            // Client always expects BiomeSectionCount biome sections regardless of subChunkCount.
            // Sending fewer causes the client to read the border-block byte as a biome header → crash.
            for (int i = 0; i < BiomeSectionCount; i++)
            {
                WriteBiomePalette(stream, biomeId);
            }

            stream.WriteByte(0); // border blocks

            return (stream.GetBuffer(), subChunkCount);
        }

        /// <summary>
        /// Finds the index of the topmost non-null sub-chunk.
        /// </summary>
        private static int FindTopSubChunkIndex(SubChunkData?[] subChunks)
        {
            for (int i = subChunks.Length - 1; i >= 0; i--)
            {
                if (subChunks[i] != null)
                    return i;
            }
            return 0;
        }

        /// <summary>
        /// Writes an empty sub-chunk (all air, single palette entry).
        /// </summary>
        private static void WriteEmptySubChunk(BinaryStream stream, int subChunkIndex, int defaultAirId)
        {
            stream.WriteByte(SubChunkData.FormatVersion);
            stream.WriteByte(1);
            stream.WriteByte((byte)(sbyte)subChunkIndex);

            stream.WriteByte(3);

            int wordCount = 4096 / 32;
            for (int i = 0; i < wordCount; i++)
            {
                stream.WriteIntLE(0);
            }

            stream.WriteVarInt(1);
            stream.WriteVarInt(defaultAirId);
        }

        /// <summary>
        /// Writes a single-biome palette for one biome section.
        /// Uses isRuntime=1 so entries are network/runtime biome IDs (not NBT compounds).
        /// This MUST match the block storage format (isRuntime=1) or the client will try to
        /// parse VarInt integers as NBT and crash immediately.
        /// </summary>
        private static void WriteBiomePalette(BinaryStream stream, int biomeId)
        {
            // Header: (bitsPerBlock << 1) | isRuntime.  isRuntime=1 → runtime biome IDs.
            // Biome data uses 4x4x4 resolution per sub-chunk section = 64 entries (NOT 4096).
            // Sending 128 words (4096 entries) caused the client to read extra zero bytes as
            // the VarInt palette size (= 0 = invalid), resulting in an immediate crash.
            stream.WriteByte(3); // 1 bit per entry, isRuntime=1
            int wordCount = 64 / 32; // = 2 words (4x4x4 biome resolution)
            for (int i = 0; i < wordCount; i++)
            {
                stream.WriteIntLE(0);
            }

            stream.WriteVarInt(1);
            stream.WriteVarInt(biomeId);
        }
    }
}
