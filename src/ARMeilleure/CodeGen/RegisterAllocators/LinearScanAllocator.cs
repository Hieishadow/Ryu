using ARMeilleure.Common;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.Translation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

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
