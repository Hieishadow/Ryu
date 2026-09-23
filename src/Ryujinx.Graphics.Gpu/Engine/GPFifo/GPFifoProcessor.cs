using Ryujinx.Graphics.Device;
using Ryujinx.Graphics.Gpu.Engine.Compute;
using Ryujinx.Graphics.Gpu.Engine.Dma;
using Ryujinx.Graphics.Gpu.Engine.InlineToMemory;
using Ryujinx.Graphics.Gpu.Engine.Threed;
using Ryujinx.Graphics.Gpu.Engine.Twod;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.Graphics.Gpu.Memory;
using System;
using System.Runtime.CompilerServices;

namespace Ryujinx.Graphics.Gpu.Engine.GPFifo
{
    class GPFifoProcessor : IDisposable
    {
        private const int MacrosCount = 0x80;
        private const int MacroIndexMask = MacrosCount - 1;
        private const int LoadInlineDataMethodOffset = 0x6d;
        private const int UniformBufferUpdateDataMethodOffset = 0x8e4;

        // #558 LOG
        private static void FLogPresent(string s){ try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_present.txt"; System.IO.File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }

        private readonly GpuChannel _channel;
        public MemoryManager MemoryManager => _channel.MemoryManager;
        public TextureManager TextureManager => _channel.TextureManager;
        public ThreedClass ThreedClass => _3dClass;

        private struct DmaState
        {
            public int Method;
            public int SubChannel;
            public int MethodCount;
            public bool NonIncrementing;
            public bool IncrementOnce;
        }

        private DmaState _state;

        private readonly ThreedClass _3dClass;
        private readonly ComputeClass _computeClass;
        private readonly InlineToMemoryClass _i2mClass;
        private readonly TwodClass _2dClass;
        private readonly DmaClass _dmaClass;
        private readonly GPFifoClass _fifoClass;

        public GPFifoProcessor(GpuContext context, GpuChannel channel)
        {
            _channel = channel;
            _fifoClass = new GPFifoClass(context, this);
            _3dClass = new ThreedClass(context, channel, _fifoClass);
            _computeClass = new ComputeClass(context, channel, _3dClass);
            _i2mClass = new InlineToMemoryClass(context, channel);
            _2dClass = new TwodClass(channel);
            _dmaClass = new DmaClass(context, channel, _3dClass);
        }

        public void Process(ulong baseGpuVa, ReadOnlySpan<int> commandBuffer)
        {
            FLogPresent($"[GPFIFO] Process START va=0x{baseGpuVa:X} len={commandBuffer.Length}");
            try
            {
                for (int index = 0; index < commandBuffer.Length; index++)
                {
                    int command = commandBuffer[index];
                    ulong gpuVa = baseGpuVa + (ulong)index * 4;

                    if (_state.MethodCount!= 0)
                    {
                        if (TryFastI2mBufferUpdate(commandBuffer, ref index))
                        {
                            continue;
                        }

                        Send(gpuVa, _state.Method, command, _state.SubChannel, _state.MethodCount <= 1);

                        if (!_state.NonIncrementing)
                        {
                            _state.Method++;
                        }

                        if (_state.IncrementOnce)
                        {
                            _state.NonIncrementing = true;
                        }

                        _state.MethodCount--;
                    }
                    else
                    {
                        CompressedMethod meth = Unsafe.As<int, CompressedMethod>(ref command);

                        if (TryFastUniformBufferUpdate(meth, commandBuffer, index))
                        {
                            index += meth.MethodCount;
                            continue;
                        }

                        switch (meth.SecOp)
                        {
                            case SecOp.IncMethod:
                            case SecOp.NonIncMethod:
                            case SecOp.OneInc:
                                _state.Method = meth.MethodAddress;
                                _state.SubChannel = meth.MethodSubchannel;
                                _state.MethodCount = meth.MethodCount;
                                _state.IncrementOnce = meth.SecOp == SecOp.OneInc;
                                _state.NonIncrementing = meth.SecOp == SecOp.NonIncMethod;
                                break;
                            case SecOp.ImmdDataMethod:
                                Send(gpuVa, meth.MethodAddress, meth.ImmdData, meth.MethodSubchannel, true);
                                break;
                        }
                    }
                }

                _3dClass.FlushUboDirty();
            }
            finally
            {
                FLogPresent("[GPFIFO] Process END");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryFastI2mBufferUpdate(ReadOnlySpan<int> commandBuffer, ref int offset)
        {
            if (_state.Method == LoadInlineDataMethodOffset && _state.NonIncrementing && _state.SubChannel <= 2)
            {
                int availableCount = commandBuffer.Length - offset;
                int consumeCount = Math.Min(_state.MethodCount, availableCount);
                ReadOnlySpan<int> data = commandBuffer.Slice(offset, consumeCount);

                if (_state.SubChannel == 0)
                {
                    _3dClass.LoadInlineData(data);
                }
                else if (_state.SubChannel == 1)
                {
                    _computeClass.LoadInlineData(data);
                }
                else
                {
                    _i2mClass.LoadInlineData(data);
                }

                offset += consumeCount - 1;
                _state.MethodCount -= consumeCount;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryFastUniformBufferUpdate(CompressedMethod meth, ReadOnlySpan<int> commandBuffer, int offset)
        {
            int availableCount = commandBuffer.Length - offset;
            if (meth.MethodAddress == UniformBufferUpdateDataMethodOffset &&
                meth.MethodCount < availableCount &&
                meth.SecOp == SecOp.NonIncMethod)
            {
                _3dClass.ConstantBufferUpdate(commandBuffer.Slice(offset + 1, meth.MethodCount));
                return true;
            }
            return false;
        }

        private void Send(ulong gpuVa, int offset, int argument, int subChannel, bool isLastCall)
        {
            if (offset < 0x60)
            {
                _fifoClass.Write(offset * 4, argument);
            }
            else if (offset < 0xe00)
            {
                offset *= 4;
                switch (subChannel)
                {
                    case 0:
                        _3dClass.Write(offset, argument);
                        break;
                    case 1:
                        _computeClass.Write(offset, argument);
                        break;
                    case 2:
                        _i2mClass.Write(offset, argument);
                        break;
                    case 3:
                        _2dClass.Write(offset, argument);
                        break;
                    case 4:
                        _dmaClass.Write(offset, argument);
                        break;
                }
            }
            else
            {
                IDeviceState state = subChannel switch
                {
                    0 => _3dClass,
                    3 => _2dClass,
                    _ => null,
                };

                if (state!= null)
                {
                    int macroIndex = (offset >> 1) & MacroIndexMask;
                    if ((offset & 1)!= 0)
                    {
                        _fifoClass.MmePushArgument(macroIndex, gpuVa, argument);
                    }
                    else
                    {
                        _fifoClass.MmeStart(macroIndex, argument);
                    }

                    if (isLastCall)
                    {
                        _fifoClass.CallMme(macroIndex, state);
                        _3dClass.PerformDeferredDraws();
                    }
                }
            }
        }

        public void Write(ClassId classId, int offset, int value)
        {
            switch (classId)
            {
                case ClassId.Threed:
                    _3dClass.Write(offset, value);
                    break;
                case ClassId.Compute:
                    _computeClass.Write(offset, value);
                    break;
                case ClassId.InlineToMemory:
                    _i2mClass.Write(offset, value);
                    break;
                case ClassId.Twod:
                    _2dClass.Write(offset, value);
                    break;
                case ClassId.Dma:
                    _dmaClass.Write(offset, value);
                    break;
                case ClassId.GPFifo:
                    _fifoClass.Write(offset, value);
                    break;
            }
        }

        public void SetShadowRamControl(int control)
        {
            _3dClass.SetShadowRamControl(control);
        }

        public void ForceAllDirty()
        {
            _3dClass.ForceStateDirty();
            _channel.BufferManager.Rebind();
            _channel.TextureManager.Rebind();
        }

        public void PerformDeferredDraws()
        {
            _3dClass.PerformDeferredDraws();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _3dClass.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
