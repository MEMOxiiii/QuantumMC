namespace QuantumMC.Blocks
{
    public class BlockBarrier : Block
    {
        public static int ID { get; internal set; }
        public override int RuntimeId => ID;

        public BlockBarrier() : base("minecraft:barrier") { }
    }
}
