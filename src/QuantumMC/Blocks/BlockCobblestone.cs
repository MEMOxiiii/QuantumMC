namespace QuantumMC.Blocks
{
    public class BlockCobblestone : Block
    {
        public static int ID { get; internal set; }
        public override int RuntimeId => ID;

        public BlockCobblestone() : base("minecraft:cobblestone") { }
    }
}
