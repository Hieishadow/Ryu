using ARMeilleure.IntermediateRepresentation;

namespace ARMeilleure.CodeGen.RegisterAllocators
{
    class HybridAllocator : IRegisterAllocator
    {
        // ANDROID PATCH: Hybrid lento -> usa LinearScan rápido
        public AllocationResult RunPass(ControlFlowGraph cfg, StackAllocator stackAlloc, RegisterMasks regMasks)
        {
            return new LinearScanAllocator().RunPass(cfg, stackAlloc, regMasks);
        }
    }
}
