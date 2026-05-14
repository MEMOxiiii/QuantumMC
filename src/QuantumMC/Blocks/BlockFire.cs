namespace QuantumMC.Blocks
{
    public class BlockFire : Block
    {
        public static int ID { get; internal set; }
        public override int RuntimeId => ID;

        public BlockFire() : base("minecraft:fire") { }
    }
}
