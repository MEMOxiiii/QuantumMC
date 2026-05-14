using MiNET.LevelDB;
using Nbt;
using QuantumMC.Registry;
using Serilog;
using System;
using System.IO;

namespace QuantumMC.World
{
    public class LevelDBWorldProvider : IWorldProvider, IDisposable
    {
        public string LevelName { get; private set; } = "world";
        public int SpawnX { get; private set; } = 0;
        public int SpawnY { get; private set; } = 65;
        public int SpawnZ { get; private set; } = 0;

        private readonly IDatabase _db;
        private readonly string _path;

        public LevelDBWorldProvider(string worldPath)
        {
            LevelName = Path.GetFileName(worldPath);
            _path = Path.Combine(worldPath, "db");
            if (!Directory.Exists(_path))
            {
                Directory.CreateDirectory(_path);
            }

            var options = new Options();
            
            _db = new Database(new DirectoryInfo(_path), true, options);
            _db.Open();

            Log.Debug("LevelDBWorldProvider initialized at {Path}", _path);

            string levelDatPath = Path.Combine(worldPath, "level.dat");
            if (File.Exists(levelDatPath))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(levelDatPath);
                    if (data.Length > 8)
                    {
                        using var ms = new MemoryStream(data, 8, data.Length - 8);
                        var nbtFile = new Nbt.NbtFile();
                        nbtFile.BigEndian = false;  // Bedrock level.dat is little-endian
                        nbtFile.LoadFromStream(ms, Nbt.NbtCompression.None);

                        if (nbtFile.RootTag != null)
                        {
                            var root = nbtFile.RootTag;
                            var levelNameTag = root["LevelName"];
                            if (levelNameTag != null) LevelName = levelNameTag.StringValue ?? LevelName;

                            var spawnXTag = root["SpawnX"];
                            if (spawnXTag != null) SpawnX = spawnXTag.IntValue;

                            var spawnYTag = root["SpawnY"];
                            if (spawnYTag != null) SpawnY = spawnYTag.IntValue;

                            var spawnZTag = root["SpawnZ"];
                            if (spawnZTag != null) SpawnZ = spawnZTag.IntValue;
                            
                            Log.Information("Loaded world '{LevelName}' with spawn ({SpawnX}, {SpawnY}, {SpawnZ})", LevelName, SpawnX, SpawnY, SpawnZ);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to parse local level.dat");
                }
            }
        }

        public Chunk? LoadChunk(int x, int z)
        {
            try
            {
                byte[] versionKey = GetKey(x, z, 118);
                var versionData = _db.Get(versionKey);

                if (versionData == null || versionData.Length == 0)
                {
                    return null;
                }

                var chunk = new Chunk(x, z);
                bool foundData = false;

                for (sbyte y = Chunk.SubChunkIndexOffset; y < Chunk.SubChunkIndexOffset + Chunk.SubChunkCount; y++)
                {
                    // Tag 47 (0x2F) is the correct Bedrock SubChunk tag
                    byte[] subChunkKey = GetKey(x, z, 47, unchecked((byte)y));
                    var subChunkData = _db.Get(subChunkKey);

                    if (subChunkData != null && subChunkData.Length > 0)
                    {
                        foundData = true;
                        // TODO: Full SubChunk deserialization from disk NBT palette format
                        Log.Warning("Chunk at {X},{Z} SubChunk {Y} has data ({Len} bytes) – deserialization pending, falling back to generator.", x, z, y, subChunkData.Length);
                        return null;
                    }
                }

                if (!foundData) return null;

                return chunk;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load chunk at {X}, {Z} from LevelDB", x, z);
                return null;
            }
        }

        public void SaveChunk(Chunk chunk)
        {
            try
            {
                // Write chunk version (tag 118)
                _db.Put(GetKey(chunk.ChunkX, chunk.ChunkZ, 118), new byte[] { 8 });

                // Serialize each non-empty SubChunk using tag 47 (0x2F)
                for (int arrayIdx = 0; arrayIdx < Chunk.SubChunkCount; arrayIdx++)
                {
                    var subChunk = chunk.GetSubChunk(arrayIdx);
                    if (subChunk == null || subChunk.IsEmpty) continue;

                    sbyte subChunkY = (sbyte)(arrayIdx + Chunk.SubChunkIndexOffset);
                    byte[] key = GetKey(chunk.ChunkX, chunk.ChunkZ, 47, unchecked((byte)subChunkY));
                    byte[] data = SerializeSubChunkToDisk(subChunk, subChunkY);
                    _db.Put(key, data);
                }

                Log.Debug("Saved chunk at ({X}, {Z})", chunk.ChunkX, chunk.ChunkZ);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save chunk at {X}, {Z} to LevelDB", chunk.ChunkX, chunk.ChunkZ);
            }
        }

        /// <summary>
        /// Serializes a SubChunk to the Bedrock LevelDB disk format (version 8, NBT palette).
        /// </summary>
        private static byte[] SerializeSubChunkToDisk(SubChunk subChunk, sbyte subChunkY)
        {
            using var ms = new MemoryStream();

            ms.WriteByte(8);  // SubChunk disk version
            ms.WriteByte(1);  // Number of storage layers

            var storage = subChunk.GetStorage();
            var palette = storage.Palette;

            // Write bit-packed block data and palette count
            storage.WriteDiskLayerData(ms);

            // Write palette entries as little-endian NBT CompoundTags (no name, no compression)
            foreach (int runtimeId in palette)
            {
                string blockName = BlockRegistry.GetByRuntimeId(runtimeId)?.Identifier ?? "minecraft:air";

                var compound = new CompoundTag("");
                compound.Add(new StringTag("name", blockName));
                compound.Add(new CompoundTag("states"));

                using var nbtStream = new MemoryStream();
                var nbtFile = new NbtFile(compound);
                nbtFile.BigEndian = false;
                nbtFile.SaveToStream(nbtStream, NbtCompression.None);
                nbtStream.Position = 0;
                nbtStream.CopyTo(ms);
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Generates a Bedrock LevelDB chunk key
        /// </summary>
        private static byte[] GetKey(int x, int z, byte tag, byte? subChunkY = null)
        {
            // Format: X (4) + Z (4) + Tag (1) [+ SubChunkY (1)] 
            int len = subChunkY.HasValue ? 10 : 9;
            var key = new byte[len];

            BitConverter.GetBytes(x).CopyTo(key, 0);
            BitConverter.GetBytes(z).CopyTo(key, 4);

            key[8] = tag;
            
            if (subChunkY.HasValue) 
            {
                key[9] = subChunkY.Value;
            }

            return key;
        }

        public void Dispose()
        {
            _db?.Close();
            _db?.Dispose();
        }
    }
}
