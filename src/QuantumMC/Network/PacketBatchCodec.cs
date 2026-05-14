using BedrockProtocol.Packets;
using BedrockProtocol.Utils;
using System.IO.Compression;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace QuantumMC.Network
{
    public static class PacketBatchCodec
    {
        private const byte GAME_PACKET_HEADER = 0xFE;

        public static void ProcessStream(BedrockStreamCipher cipher, byte[] input, byte[] output)
        {
            cipher.ProcessBytes(input, 0, input.Length, output, 0);
        }

        public static List<(uint packetId, byte[] payload)> Decode(byte[] data, PlayerSession session)
        {
            var packets = new List<(uint, byte[])>();

            if (data.Length < 1 || data[0] != GAME_PACKET_HEADER)
                return packets;

            var stream = new BinaryStream(data);
            stream.Position = 1;

            byte[] batchPayload;

            if (session.EncryptionEnabled && session.Decryptor != null && session.AesKey != null)
            {
                long remaining = data.Length - stream.Position;
                if (remaining < 8) throw new Exception("Encrypted payload too small to contain checksum.");
                
                byte[] encryptedData = stream.ReadBytes((int)remaining);
                byte[] decrypted = new byte[encryptedData.Length];
                
                ProcessStream(session.Decryptor, encryptedData, decrypted);
                
                byte[] plaintext = decrypted[..^8];
                byte[] receivedChecksum = decrypted[^8..];
                
                byte[] expectedChecksum = EncryptionUtils.CalculateChecksum(plaintext, session.ReceiveCounter++, session.AesKey);
                
                if (!receivedChecksum.SequenceEqual(expectedChecksum))
                {
                    Serilog.Log.Error("Bedrock checksum mismatch! " +
                        "Counter: {Counter}, " +
                        "Received: {ReceivedHex}, " +
                        "Expected: {ExpectedHex}, " +
                        "Key: {KeyFp}",
                        session.ReceiveCounter - 1,
                        BitConverter.ToString(receivedChecksum).Replace("-", ""),
                        BitConverter.ToString(expectedChecksum).Replace("-", ""),
                        BitConverter.ToString(session.AesKey).Replace("-", "").Substring(0, 8));

                    throw new Exception("Bedrock checksum verification failed! Encryption out of sync.");
                }

                batchPayload = plaintext;
                stream = new BinaryStream(batchPayload);
            }

            if (session.CompressionReady)
            {
                byte algorithm = stream.ReadByte();
                
                if (algorithm == 0x00) // Zlib
                {
                    long remaining = stream.GetBuffer().Length - stream.Position;
                    byte[] compressed = stream.ReadBytes((int)remaining);
                    batchPayload = ZlibDecompress(compressed);
                }
                else if (algorithm == 0xFF) // None
                {
                    long remaining = stream.GetBuffer().Length - stream.Position;
                    batchPayload = stream.ReadBytes((int)remaining);
                }
                else
                {
                    throw new Exception($"Unknown compression algorithm: {algorithm}");
                }
            }
            else
            {
                long remaining = stream.GetBuffer().Length - stream.Position;
                batchPayload = stream.ReadBytes((int)remaining);
            }

            var batchStream = new BinaryStream(batchPayload);

            while (!batchStream.Eof)
            {
                uint length = batchStream.ReadUnsignedVarInt();
                byte[] packetData = batchStream.ReadBytes((int)length);

                var packetStream = new BinaryStream(packetData);
                uint header = packetStream.ReadUnsignedVarInt();
                uint packetId = header & 0x3FF;

                long remaining = packetData.Length - (int)packetStream.Position;
                byte[] payload = packetStream.ReadBytes((int)remaining);

                packets.Add((packetId, payload));
            }

            return packets;
        }

        public static byte[] Encode(Packet packet, PlayerSession session, int threshold = 256)
        {
            return EncodeBatch(new List<Packet> { packet }, session, threshold);
        }

        public static byte[] EncodeBatch(List<Packet> packets, PlayerSession session, int threshold = 256)
        {
            var bodyStream = new BinaryStream();

            foreach (var packet in packets)
            {
                var packetStream = new BinaryStream();
                uint header = packet.PacketId & 0x3FF; // 10 bits packet id
                packetStream.WriteUnsignedVarInt(header);
                packet.Encode(packetStream);
                byte[] packetBody = packetStream.GetBuffer();

                bodyStream.WriteUnsignedVarInt((uint)packetBody.Length);
                bodyStream.WriteBytes(packetBody);
            }

            return CreateBatch(bodyStream.GetBuffer(), session, threshold);
        }

        private static byte[] CreateBatch(byte[] payloads, PlayerSession session, int threshold)
        {
            var batchStream = new BinaryStream();
            batchStream.WriteByte(GAME_PACKET_HEADER);

            if (session.CompressionReady)
            {
                if (payloads.Length >= threshold)
                {
                    batchStream.WriteByte(0x00); // Zlib
                    byte[] compressed = ZlibCompress(payloads);
                    batchStream.WriteBytes(compressed);
                }
                else
                {
                    batchStream.WriteByte(0xFF); // None
                    batchStream.WriteBytes(payloads);
                }
            }
            else
            {
                batchStream.WriteBytes(payloads);
            }

            if (session.EncryptionEnabled && session.Encryptor != null && session.AesKey != null)
            {
                byte[] rawPayload = batchStream.GetBuffer()[1..(int)batchStream.Position]; 
                
                byte[] checksum = EncryptionUtils.CalculateChecksum(rawPayload, session.SendCounter++, session.AesKey);
                
                byte[] toEncrypt = new byte[rawPayload.Length + checksum.Length];
                Buffer.BlockCopy(rawPayload, 0, toEncrypt, 0, rawPayload.Length);
                Buffer.BlockCopy(checksum, 0, toEncrypt, rawPayload.Length, checksum.Length);
                
                byte[] encrypted = new byte[toEncrypt.Length];
                ProcessStream(session.Encryptor, toEncrypt, encrypted);

                var encryptedStream = new BinaryStream();
                encryptedStream.WriteByte(GAME_PACKET_HEADER);
                encryptedStream.WriteBytes(encrypted);
                
                return encryptedStream.GetBuffer();
            }

            return batchStream.GetBuffer();
        }

        private static byte[] ZlibDecompress(byte[] data)
        {
            using var memStream = new MemoryStream(data);
            using var deflateStream = new DeflateStream(memStream, CompressionMode.Decompress);
            using var outStream = new MemoryStream();
            deflateStream.CopyTo(outStream);
            return outStream.ToArray();
        }

        private static byte[] ZlibCompress(byte[] data)
        {
            using var outStream = new MemoryStream();
            using (var deflateStream = new DeflateStream(outStream, CompressionLevel.Fastest))
            {
                deflateStream.Write(data, 0, data.Length);
            }
            return outStream.ToArray();
        }
    }
}
