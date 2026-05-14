using Nbt;
using Serilog;
using System.Collections.Concurrent;
using System.IO;

namespace QuantumMC.World
{
    public class WorldManager
    {
        public World DefaultWorld => GetWorld(Server.Instance.Config.WorldName)!;

        private readonly ConcurrentDictionary<string, World> _worlds = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _worldsFolder;

        public WorldManager()
        {
            _worldsFolder = Path.Combine(QuantumMC.DataFolder, "worlds");

            if (!Directory.Exists(_worldsFolder))
            {
                Directory.CreateDirectory(_worldsFolder);
            }
        }

        public void LoadWorlds()
        {
            Log.Debug("Loading worlds from {Path}...", _worldsFolder);

            string defaultWorldName = Server.Instance.Config.WorldName;
            string defaultWorldPath = Path.Combine(_worldsFolder, defaultWorldName);

            if (!Directory.Exists(defaultWorldPath))
            {
                Log.Warning("Default world '{WorldName}' not found. Creating it...", defaultWorldName);
                CreateWorld(defaultWorldName);
            }

            foreach (var worldDir in Directory.GetDirectories(_worldsFolder))
            {
                string worldName = Path.GetFileName(worldDir);
                if (_worlds.ContainsKey(worldName)) continue;

                try
                {
                    LoadWorld(worldName);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to load world '{WorldName}'", worldName);
                }
            }

            Log.Information("Loaded {Count} worlds.", _worlds.Count);
        }

        /// <summary>
        /// Loads a specific world by name from the worlds folder.
        /// </summary>
        public World LoadWorld(string name)
        {
            if (_worlds.TryGetValue(name, out var existingWorld))
            {
                return existingWorld;
            }

            string worldPath = Path.Combine(_worldsFolder, name);
            if (!Directory.Exists(worldPath))
            {
                throw new DirectoryNotFoundException($"World directory not found: {worldPath}");
            }

            Log.Information("Loading world: {WorldName}", name);

            IWorldGenerator generator = new FlatWorldGenerator();
            
            var provider = new LevelDBWorldProvider(worldPath);
            var world = new World(generator, provider);
            
            _worlds.TryAdd(name, world);
            return world;
        }

        /// <summary>
        /// Creates a new world directory with a default level.dat and then loads it.
        /// </summary>
        public World CreateWorld(string name)
        {
            string worldPath = Path.Combine(_worldsFolder, name);
            if (!Directory.Exists(worldPath))
            {
                Directory.CreateDirectory(worldPath);
                WriteLevelDat(worldPath, name);
                Log.Information("Created new world '{WorldName}' at {Path}", name, worldPath);
            }
            
            return LoadWorld(name);
        }

        private static void WriteLevelDat(string worldPath, string name)
        {
            try
            {
                var root = new Nbt.CompoundTag("");
                root.Add(new Nbt.StringTag("LevelName", name));
                root.Add(new Nbt.IntTag("SpawnX", 0));
                root.Add(new Nbt.IntTag("SpawnY", 65));
                root.Add(new Nbt.IntTag("SpawnZ", 0));
                root.Add(new Nbt.IntTag("GameType", 0));
                root.Add(new Nbt.IntTag("StorageVersion", 10));

                using var nbtStream = new MemoryStream();
                var nbtFile = new Nbt.NbtFile(root);
                nbtFile.BigEndian = false;
                nbtFile.SaveToStream(nbtStream, Nbt.NbtCompression.None);
                byte[] nbtBytes = nbtStream.ToArray();

                // Bedrock level.dat header: 8 bytes (version=8 + size as little-endian int32)
                using var fs = new FileStream(Path.Combine(worldPath, "level.dat"), FileMode.Create, FileAccess.Write);
                Span<byte> header = stackalloc byte[8];
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header, 8);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4), nbtBytes.Length);
                fs.Write(header);
                fs.Write(nbtBytes);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to write level.dat for world '{WorldName}'", name);
            }
        }

        /// <summary>
        /// Gets a loaded world by name.
        /// </summary>
        public World? GetWorld(string name)
        {
            _worlds.TryGetValue(name, out var world);
            return world;
        }

        /// <summary>
        /// Returns all currently loaded worlds.
        /// </summary>
        public IEnumerable<World> GetAllWorlds() => _worlds.Values;
    }
}
