namespace QuantumMC.Blocks
{
    public class BlockOakPlanks : Block
    {
        public static int ID { get; internal set; }
        public override int RuntimeId => ID;

        public BlockOakPlanks() : base("minecraft:oak_planks") { }
    }
}
