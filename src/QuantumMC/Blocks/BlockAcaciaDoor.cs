namespace QuantumMC.Blocks
{
    public class BlockAcaciaDoor : Block
    {
        public static int ID { get; internal set; }
        public override int RuntimeId => ID;

        public BlockAcaciaDoor() : base("minecraft:acacia_door") { }
    }
}
