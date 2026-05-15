using System;
using System.Collections.Generic;
using BedrockProtocol;
using BedrockProtocol.Packets;
using BedrockProtocol.Packets.Enums;
using BedrockProtocol.Packets.Types;
using BedrockProtocol.Types;
using BedrockProtocol.Utils;
using Nbt;
using Serilog;

namespace QuantumMC.Network.Handler
{
    public class ResourcePackHandler : PacketHandler
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> _resourceCache = new();

        private static byte[] LoadEmbeddedResource(string name)
        {
            return _resourceCache.GetOrAdd(name, n =>
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream(n)
                    ?? throw new Exception($"Embedded resource '{n}' not found");
                var buf = new byte[stream.Length];
                stream.ReadExactly(buf);
                return buf;
            });
        }

        public override void Handle(PlayerSession session, uint packetId, byte[] payload)
        {
            var stream = new BinaryStream(payload);
            var packet = new ResourcePackClientResponsePacket();
            packet.Decode(stream);

            switch (packet.ResponseStatus)
            {
                case ResourcePackClientResponseStatus.Refused:
                    session.Disconnect();
                    break;

                case ResourcePackClientResponseStatus.SendPacks:
                    Log.Debug("Player {Username} requested pack download (no packs to send)", session.Username);
                    break;

                case ResourcePackClientResponseStatus.HaveAllPacks:
                    var stackPacket = new ResourcePackStackPacket
                    {
                        MustAccept = false,
                        BaseGameVersion = "*",
                        UseVanillaEditorPacks = false
                    };
                    session.SendPacket(stackPacket);
                    break;

                case ResourcePackClientResponseStatus.Completed:
                    HandleCompleted(session);
                    break;
            }
        }

        private void HandleCompleted(PlayerSession session)
        {
            Log.Information("[JOIN] HandleCompleted start for {Username}", session.Username);
            session.State = SessionState.PlayPhase;

            var voxelShapes = new VoxelShapesPacket
            {
                Shapes = new List<VoxelShape>(),
                NameMap = new Dictionary<string, ushort>(),
                CustomShapeCount = 0
            };
            session.SendPacket(voxelShapes);
            Log.Information("[JOIN] Sent VoxelShapes");

            var startGame = new StartGamePacket
            {
                EntityUniqueId = session.Player.EntityUniqueId,
                EntityRuntimeId = session.Player.EntityRuntimeId,
                PlayerGamemode = session.Player.Gamemode,
                Position = new Vector3(session.Player.X, session.Player.Y + 1.62f, session.Player.Z),
                Yaw = session.Player.Yaw, Pitch = session.Player.Pitch,
                Seed = -1,
                SpawnBiomeType = 0,
                UserDefinedBiomeName = "plains",
                Dimension = 0,
                Generator = 1,
                WorldGamemode = session.Player.Gamemode,
                IsHardcore = false,
                Difficulty = 0,
                SpawnPosition = new BlockPosition(session.Player.World!.SpawnX, session.Player.World.SpawnY, session.Player.World.SpawnZ),
                HasAchievementsDisabled = true,
                EditorWorldType = 0,
                CreatedInEditor = false,
                ExportedFromEditor = false,
                DayCycleStopTime = 0,
                EduEditionOffer = 0,
                HasEduFeaturesEnabled = false,
                EducationProductId = "",
                RainLevel = 0.0f,
                LightningLevel = 0.0f,
                HasConfirmedPlatformLockedContent = false,
                MultiplayerGame = true,
                BroadcastToLan = true,
                XblBroadcastIntent = 2,
                PlatformBroadcastIntent = 2,
                CommandsEnabled = true,
                IsTexturePacksRequired = false,
                ExperimentsPreviouslyToggled = false,
                BonusChest = false,
                HasStartWithMapEnabled = false,
                PermissionLevel = 1,
                ServerChunkTickRange = 4,
                HasLockedBehaviorPack = false,
                HasLockedResourcePack = false,
                IsFromLockedWorldTemplate = false,
                IsUsingMsaGamertagsOnly = false,
                IsFromWorldTemplate = false,
                IsWorldTemplateOptionLocked = false,
                IsOnlySpawningV1Villagers = false,
                IsDisablingPersonas = false,
                IsDisablingCustomSkins = false,
                MuteEmoteAnnouncements = false,
                VanillaVersion = "*",
                LimitedWorldWidth = 16,
                LimitedWorldDepth = 16,
                NewNether = false,
                EduSharedResourceButtonName = "",
                EduSharedResourceLinkUri = "",
                ForceExperimentalGameplay = false,
                ChatRestrictionLevel = 0,
                DisablePlayerInteractions = false,
                LevelId = "",
                WorldName = session.Player.World!.Name,
                PremiumWorldTemplateId = "",
                IsTrial = false,
                RewindHistorySize = 0,
                IsServerAuthoritativeBlockBreaking = false,
                CurrentTick = 9000,
                EnchantmentSeed = 99000,
                MultiplayerCorrelationId = "c5d3d2cc-27fd-4221-9de6-d22c4d423d53",
                IsInventoryServerAuthoritative = false,
                ServerEngine = "*",
                PlayerPropertyData = new CompoundTag(),
                BlockRegistryChecksum = 0,
                WorldTemplateId = Guid.Empty,
                ClientSideGenerationEnabled = false,
                BlockNetworkIdsHashed = false,
                IsSoundsServerAuthoritative = false,
                ServerId = "",
                ScenarioId = "",
                WorldId = "",
                OwnerId = "",
                HasServerJoinInformation = false,
                ServerJoinInfo = null
            };

            session.SendPacket(startGame);
            Log.Information("[JOIN] Sent StartGame");

            // Required: Bedrock client waits for both of these after StartGame before it will proceed.
            session.SendPacket(new BiomeDefinitionListPacket { NbtPayload = LoadEmbeddedResource("biome_definitions.nbt") });
            session.SendPacket(new AvailableActorIdentifiersPacket { SerialisedEntityIdentifiers = LoadEmbeddedResource("entity_identifiers.nbt") });
            Log.Information("[JOIN] Sent BiomeDefinitionList + AvailableActorIdentifiers");

            // CameraPresetsPacket is required since MC 1.20.60 (protocol ~671).
            // TrimDataPacket is required since MC 1.20.10.
            // Both must be sent before PlayStatus(PlayerSpawn); empty lists are valid.
            session.SendPacket(new CameraPresetsPacket());
            session.SendPacket(new TrimDataPacket());
            Log.Information("[JOIN] Sent CameraPresets + TrimData");

            bool isCreative = session.Player.Gamemode == 1;

            // Survival: basic interactions + walk/fly speed values
            uint survivalValues = (uint)(Ability.Build | Ability.Mine | Ability.DoorsAndSwitches |
                Ability.OpenContainers | Ability.AttackPlayers | Ability.AttackMobs |
                Ability.FlySpeed | Ability.WalkSpeed);
            // Creative: adds mayFly, invulnerable, instant-build, op-cmds, teleport.
            // NOTE: Ability.Flying (is currently flying) is intentionally omitted so the
            // player controls fly-toggle via double-jump. VerticalFlySpeed (bit 19) is also
            // excluded from Abilities so the client uses its own default vertical fly speed.
            uint creativeValues = survivalValues | (uint)(Ability.MayFly |
                Ability.Invulnerable | Ability.InstantBuild | Ability.OperatorCommands | Ability.Teleport);

            var abilities = new UpdateAbilitiesPacket
            {
                AbilityData = new AbilityData
                {
                    EntityUniqueId = session.Player.EntityUniqueId,
                    PlayerPermissions = (byte)PermissionLevel.Member,
                    CommandPermissions = isCreative ? (byte)2 : (byte)0,
                    Layers = new List<AbilityLayer>
                    {
                        new AbilityLayer
                        {
                            Type = (ushort)AbilityLayerType.Base,
                            // 0x7FFFF = bits 0-18 only; excludes VerticalFlySpeed (bit 19)
                            // so the client uses its default vertical fly speed in fly mode.
                            Abilities = 0x7FFFF,
                            Values = isCreative ? creativeValues : survivalValues,
                            FlySpeed = 0.05f,
                            VerticalFlySpeed = 0.05f,
                            WalkSpeed = 0.1f
                        }
                    }
                }
            };
            session.SendPacket(abilities);
            Log.Information("[JOIN] Sent UpdateAbilities");

            // CreativeContent is required so the client can populate the creative inventory.
            // Sending an empty payload (0 items) satisfies the protocol requirement.
            session.SendPacket(new CreativeContentPacket());
            Log.Information("[JOIN] Sent CreativeContent (empty)");

            // ItemRegistry (0xa2) is required in MC 1.21+ for the client to initialise its
            // item table. Without it, the client may crash or disconnect during world load.
            // Sending an empty registry (0 items) satisfies the requirement.
            session.SendPacket(new ItemRegistryPacket());
            Log.Information("[JOIN] Sent ItemRegistry (empty)");

            // Send player attributes so the client knows correct movement/health values.
            // Without minecraft:movement, the client may default to 0 (causes very slow walking).
            var attrs = new UpdateAttributesPacket
            {
                EntityRuntimeId = session.Player.EntityRuntimeId,
                Attributes = new List<NetworkAttribute>
                {
                    new NetworkAttribute
                    {
                        Name = "minecraft:health",
                        Min = 0f, Max = 20f, Value = 20f,
                        DefaultMin = 0f, DefaultMax = 20f, DefaultValue = 20f
                    },
                    new NetworkAttribute
                    {
                        Name = "minecraft:movement",
                        Min = 0f, Max = 1f, Value = 0.1f,
                        DefaultMin = 0f, DefaultMax = 1f, DefaultValue = 0.1f
                    }
                },
                Tick = 0
            };
            session.SendPacket(attrs);
            Log.Information("[JOIN] HandleCompleted done — join sequence complete");
        }
    }
}
