namespace QuantumMC.Blocks
{
    public class BlockAllow : Block
    {
        public static int ID { get; internal set; }
        public override int RuntimeId => ID;

        public BlockAllow() : base("minecraft:allow") { }
    }
}
