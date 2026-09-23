using Ryujinx.Common.Configuration;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Vulkan.Effects;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System;
using System.IO;
using System.Linq;
using VkFormat = Silk.NET.Vulkan.Format;

namespace Ryujinx.Graphics.Vulkan
{
    class Window : WindowBase, IDisposable
    {
        private const int SurfaceWidth = 2186;
        private const int SurfaceHeight = 1080;
        private static void FLog(string s){ try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_log.txt"; var d=Path.GetDirectoryName(p); if(d!=null){ try{ if(!Directory.Exists(d)) Directory.CreateDirectory(d); }catch{} } File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff")+" [FILE] "+s+"\n"); Console.WriteLine(s); }catch{} }
        private static void FLogPresent(string s){ try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_present.txt"; var d=Path.GetDirectoryName(p); if(d!=null){ try{ if(!Directory.Exists(d)) Directory.CreateDirectory(d); }catch{} } File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }

        private readonly VulkanRenderer _gd;
        private readonly SurfaceKHR _surface;
        private readonly PhysicalDevice _physicalDevice;
        private readonly Device _device;
        private SwapchainKHR _swapchain;
        private Image[] _swapchainImages;
        private TextureView[] _swapchainImageViews;
        private Semaphore[] _imageAvailableSemaphores;
        private Semaphore[] _renderFinishedSemaphores;
        private int _frameIndex;
        private int _width;
        private int _height;
        private int _requestedWidth = 2186;
        private int _requestedHeight = 1080;
        private VSyncMode _vSyncMode;
        private bool _swapchainIsDirty;
        private VkFormat _format;
        private AntiAliasing _currentAntiAliasing;
        private bool _updateEffect;
        private IPostProcessingEffect _effect;
        private IScalingFilter _scalingFilter;
        private bool _isLinear = true;
        private float _scalingFilterLevel;
        private bool _updateScalingFilter;
        private ScalingFilter _currentScalingFilter = ScalingFilter.Bilinear;
        private bool _colorSpacePassthroughEnabled;

        public unsafe Window(VulkanRenderer gd, SurfaceKHR surface, PhysicalDevice physicalDevice, Device device)
        {
            _gd = gd; _physicalDevice = physicalDevice; _device = device; _surface = surface;
            FLog($"[VK] Window ctor v10.5 #555 req={_requestedWidth}x{_requestedHeight}");
            FLogPresent($"[VK] Window ctor v10.5 #555 req={_requestedWidth}x{_requestedHeight}");
            try { CreateSwapchain(); FLog($"[VK] Window ctor OK {_width}x{_height}"); FLogPresent($"[VK] Window ctor OK {_width}x{_height}"); } catch (Exception ex) { FLog($"[VK] CreateSwapchain initial fail: {ex}"); throw; }
        }

        private void RecreateSwapchain()
        {
            try
            {
                SwapchainKHR oldSwapchain = _swapchain; _swapchainIsDirty = false;
                FLog($"[VK] RecreateSwapchain BEGIN old={_width}x{_height} newReq={_requestedWidth}x{_requestedHeight}");
                FLogPresent($"[VK] RecreateSwapchain BEGIN old={_width}x{_height} newReq={_requestedWidth}x{_requestedHeight}");
                try { _gd.Api.DeviceWaitIdle(_device); } catch(Exception ex){ FLog($"[VK] DeviceWaitIdle fail {ex.Message}"); }
                if (_swapchainImageViews!= null) for (int i = 0; i < _swapchainImageViews.Length; i++) try { _swapchainImageViews[i]?.Dispose(); } catch {}
                unsafe
                {
                    if (_imageAvailableSemaphores!= null) for (int i = 0; i < _imageAvailableSemaphores.Length; i++) try { _gd.Api.DestroySemaphore(_device, _imageAvailableSemaphores[i], null); } catch {}
                    if (_renderFinishedSemaphores!= null) for (int i = 0; i < _renderFinishedSemaphores.Length; i++) try { _gd.Api.DestroySemaphore(_device, _renderFinishedSemaphores[i], null); } catch {}
                }
                try { _gd.SwapchainApi.DestroySwapchain(_device, oldSwapchain, Span<AllocationCallbacks>.Empty); } catch {}
                CreateSwapchain(); FLog($"[VK] RecreateSwapchain OK {_width}x{_height}"); FLogPresent($"[VK] RecreateSwapchain OK {_width}x{_height}");
            }
            catch (Exception ex){ FLog($"[VK] RecreateSwapchain fail: {ex}"); FLogPresent($"[VK] RecreateSwapchain fail: {ex}"); }
        }

        private unsafe void CreateSwapchain()
        {
            _gd.SurfaceApi.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, out SurfaceCapabilitiesKHR capabilities);
            uint surfaceFormatsCount; _gd.SurfaceApi.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &surfaceFormatsCount, null);
            SurfaceFormatKHR[] surfaceFormats = new SurfaceFormatKHR[surfaceFormatsCount];
            fixed (SurfaceFormatKHR* pSurfaceFormats = surfaceFormats) { _gd.SurfaceApi.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &surfaceFormatsCount, pSurfaceFormats); }
            uint presentModesCount; _gd.SurfaceApi.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &presentModesCount, null);
            PresentModeKHR[] presentModes = new PresentModeKHR[presentModesCount];
            fixed (PresentModeKHR* pPresentModes = presentModes) { _gd.SurfaceApi.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &presentModesCount, pPresentModes); }
            uint imageCount = capabilities.MinImageCount + 1;
            if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount) imageCount = capabilities.MaxImageCount;
            SurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(surfaceFormats, _colorSpacePassthroughEnabled);
            Extent2D extent = ChooseSwapExtent(capabilities, _requestedWidth, _requestedHeight);
            _width = (int)extent.Width; _height = (int)extent.Height; _format = surfaceFormat.Format;
            FLog($"[VK] CreateSwapchain {_width}x{_height} req={_requestedWidth}x{_requestedHeight} imgCount={imageCount}");
            SwapchainCreateInfoKHR swapchainCreateInfo = new() { SType = StructureType.SwapchainCreateInfoKhr, Surface = _surface, MinImageCount = imageCount, ImageFormat = surfaceFormat.Format, ImageColorSpace = surfaceFormat.ColorSpace, ImageExtent = extent, ImageUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit, ImageSharingMode = SharingMode.Exclusive, ImageArrayLayers = 1, PreTransform = capabilities.CurrentTransform, CompositeAlpha = ChooseCompositeAlpha(capabilities.SupportedCompositeAlpha), PresentMode = ChooseSwapPresentMode(presentModes, _vSyncMode), Clipped = true, };
            TextureCreateInfo textureCreateInfo = new(_width, _height, 1,1,1,1,1,1, FormatTable.GetFormat(surfaceFormat.Format), DepthStencilMode.Depth, Target.Texture2D, SwizzleComponent.Red, SwizzleComponent.Green, SwizzleComponent.Blue, SwizzleComponent.Alpha);
            _gd.SwapchainApi.CreateSwapchain(_device, in swapchainCreateInfo, null, out _swapchain).ThrowOnError();
            _gd.SwapchainApi.GetSwapchainImages(_device, _swapchain, &imageCount, null);
            _swapchainImages = new Image[imageCount];
            fixed (Image* pSwapchainImages = _swapchainImages) { _gd.SwapchainApi.GetSwapchainImages(_device, _swapchain, &imageCount, pSwapchainImages); }
            _swapchainImageViews = new TextureView[imageCount];
            for (int i = 0; i < _swapchainImageViews.Length; i++) _swapchainImageViews[i] = CreateSwapchainImageView(_swapchainImages[i], surfaceFormat.Format, textureCreateInfo);
            SemaphoreCreateInfo semaphoreCreateInfo = new() { SType = StructureType.SemaphoreCreateInfo };
            _imageAvailableSemaphores = new Semaphore[imageCount];
            for (int i = 0; i < _imageAvailableSemaphores.Length; i++) _gd.Api.CreateSemaphore(_device, in semaphoreCreateInfo, null, out _imageAvailableSemaphores[i]).ThrowOnError();
            _renderFinishedSemaphores = new Semaphore[imageCount];
            for (int i = 0; i < _renderFinishedSemaphores.Length; i++) _gd.Api.CreateSemaphore(_device, in semaphoreCreateInfo, null, out _renderFinishedSemaphores[i]).ThrowOnError();
            FLog($"[VK] CreateSwapchain OK {_width}x{_height} - NO FENCES v10.5 #555");
        }

        private unsafe TextureView CreateSwapchainImageView(Image swapchainImage, VkFormat format, TextureCreateInfo info)
        {
            ComponentMapping componentMapping = new(ComponentSwizzle.R, ComponentSwizzle.G, ComponentSwizzle.B, ComponentSwizzle.A);
            ImageSubresourceRange subresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1);
            ImageViewCreateInfo imageCreateInfo = new() { SType = StructureType.ImageViewCreateInfo, Image = swapchainImage, ViewType = ImageViewType.Type2D, Format = format, Components = componentMapping, SubresourceRange = subresourceRange, };
            _gd.Api.CreateImageView(_device, in imageCreateInfo, null, out ImageView imageView).ThrowOnError();
            return new TextureView(_gd, _device, new DisposableImageView(_gd.Api, _device, imageView), info, format);
        }

        private static SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats, bool colorSpacePassthroughEnabled)
        {
            if (availableFormats.Length == 1 && availableFormats[0].Format == VkFormat.Undefined) return new SurfaceFormatKHR(VkFormat.B8G8R8A8Unorm, ColorSpaceKHR.PaceSrgbNonlinearKhr);
            SurfaceFormatKHR formatToReturn = availableFormats[0];
            if (colorSpacePassthroughEnabled) { foreach (var format in availableFormats) if (format.Format == VkFormat.B8G8R8A8Unorm && format.ColorSpace == ColorSpaceKHR.SpacePassThroughExt) { formatToReturn = format; break; } else if (format.Format == VkFormat.B8G8R8A8Unorm && format.ColorSpace == ColorSpaceKHR.PaceSrgbNonlinearKhr) formatToReturn = format; }
            else { foreach (var format in availableFormats) if (format.Format == VkFormat.B8G8R8A8Unorm && format.ColorSpace == ColorSpaceKHR.PaceSrgbNonlinearKhr) { formatToReturn = format; break; } }
            return formatToReturn;
        }
        private static CompositeAlphaFlagsKHR ChooseCompositeAlpha(CompositeAlphaFlagsKHR supportedFlags) { if (supportedFlags.HasFlag(CompositeAlphaFlagsKHR.OpaqueBitKhr)) return CompositeAlphaFlagsKHR.OpaqueBitKhr; else if (supportedFlags.HasFlag(CompositeAlphaFlagsKHR.PreMultipliedBitKhr)) return CompositeAlphaFlagsKHR.PreMultipliedBitKhr; else return CompositeAlphaFlagsKHR.InheritBitKhr; }
        private static PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes, VSyncMode vSyncMode) { if (vSyncMode == VSyncMode.Unbounded && availablePresentModes.Contains(PresentModeKHR.ImmediateKhr)) return PresentModeKHR.ImmediateKhr; else if (availablePresentModes.Contains(PresentModeKHR.MailboxKhr)) return PresentModeKHR.MailboxKhr; else return PresentModeKHR.FifoKhr; }
        public static Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities, int reqW, int reqH)
        {
            if (capabilities.CurrentExtent.Width!= uint.MaxValue) return capabilities.CurrentExtent;
            uint width = Math.Max(capabilities.MinImageExtent.Width, Math.Min(capabilities.MaxImageExtent.Width, (uint)reqW));
            uint height = Math.Max(capabilities.MinImageExtent.Height, Math.Min(capabilities.MaxImageExtent.Height, (uint)reqH));
            return new Extent2D(width, height);
        }
        public static Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities) => ChooseSwapExtent(capabilities, SurfaceWidth, SurfaceHeight);

        public unsafe override void Present(ITexture texture, ImageCrop crop, Action swapBuffersCallback)
        {
            FLogPresent($"[VK] Present CALLED f={_frameIndex} tex={(texture==null?"NULL":"OK")} dirty={_swapchainIsDirty}");
            try
            {
                if(_swapchainIsDirty) RecreateSwapchain();
                uint nextImage = 0;
                int semIdx = _frameIndex % _imageAvailableSemaphores.Length;
                FLogPresent($"[VK] AcquireNextImage START f={_frameIndex} semIdx={semIdx}");
                Result acq = _gd.SwapchainApi.AcquireNextImage(_device, _swapchain, 1000000000, _imageAvailableSemaphores[semIdx], new Fence(), ref nextImage);
                FLogPresent($"[VK] Acquire {acq} img={nextImage} semIdx={semIdx} f={_frameIndex}");
                if (acq == Result.ErrorOutOfDateKhr || acq == Result.SuboptimalKhr) { _swapchainIsDirty = true; RecreateSwapchain(); acq = _gd.SwapchainApi.AcquireNextImage(_device, _swapchain, 1000000000, _imageAvailableSemaphores[semIdx], new Fence(), ref nextImage); }

                FLogPresent($"[VK] Rent START f={_frameIndex}");
                CommandBufferScoped cbs = _gd.CommandBufferPool.Rent();
                FLogPresent($"[VK] Rent OK f={_frameIndex}");
                Transition(cbs.CommandBuffer, _swapchainImages[nextImage], 0, AccessFlags.TransferWriteBit, ImageLayout.Undefined, ImageLayout.General, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit);

                if (texture == null)
                {
                    ClearColorValue clear = new(); clear.Float32_0 = 1f; clear.Float32_1 = 0f; clear.Float32_2 = 1f; clear.Float32_3 = 1f;
                    ImageSubresourceRange range = new(ImageAspectFlags.ColorBit,0,1,0,1);
                    _gd.Api.CmdClearColorImage(cbs.CommandBuffer, _swapchainImages[nextImage], ImageLayout.General, &clear, 1, &range);
                }
                else
                {
                    TextureView view = (TextureView)texture;
                    ClearColorValue clear = new(); clear.Float32_0 = 1f; clear.Float32_1 = 0f; clear.Float32_2 = 1f; clear.Float32_3 = 1f;
                    ImageSubresourceRange range = new(ImageAspectFlags.ColorBit,0,1,0,1);
                    _gd.Api.CmdClearColorImage(cbs.CommandBuffer, _swapchainImages[nextImage], ImageLayout.General, &clear, 1, &range);
                    try {
                        UpdateEffect();
                        TextureView src = view;
                        if (_effect!= null) src = _effect.Run(view, cbs, _width, _height);
                        int sx0 = crop.Left == 0 && crop.Right == 0? 0 : crop.Left;
                        int sx1 = crop.Right == 0? src.Width : crop.Right;
                        int sy0 = crop.Top == 0 && crop.Bottom == 0? 0 : crop.Top;
                        int sy1 = crop.Bottom == 0? src.Height : crop.Bottom;
                        if(sx1>0 && sy1>0){
                            _gd.HelperShader.BlitColor(_gd, cbs, src, _swapchainImageViews[nextImage], new Extents2D(sx0, sy0, sx1, sy1), new Extents2D(0, 0, _width, _height), true, true);
                        }
                    } catch(Exception ex){ FLogPresent($"[VK] BLIT FAIL f={_frameIndex} {ex}"); }
                }

                Transition(cbs.CommandBuffer, _swapchainImages[nextImage], AccessFlags.TransferWriteBit, 0, ImageLayout.General, ImageLayout.PresentSrcKhr, PipelineStageFlags.TransferBit, PipelineStageFlags.BottomOfPipeBit);
                FLogPresent($"[VK] Return START f={_frameIndex}");
                _gd.CommandBufferPool.Return(cbs, [_imageAvailableSemaphores[semIdx]], [PipelineStageFlags.ColorAttachmentOutputBit], [_renderFinishedSemaphores[semIdx]]);
                FLogPresent($"[VK] Return OK f={_frameIndex}");

                Semaphore sem = _renderFinishedSemaphores[semIdx]; SwapchainKHR sc = _swapchain; Result pres;
                PresentInfoKHR pi = new(){ SType=StructureType.PresentInfoKhr, WaitSemaphoreCount=1, PWaitSemaphores=&sem, SwapchainCount=1, PSwapchains=&sc, PImageIndices=&nextImage, PResults=&pres };
                FLogPresent($"[VK] QueuePresent START f={_frameIndex}");
                Result qr; lock(_gd.QueueLock){ qr = _gd.SwapchainApi.QueuePresent(_gd.Queue, in pi); }
                FLogPresent($"[VK] QueuePresent q={qr} p={pres} f={_frameIndex}");

                FLogPresent($"[VK] swapBuffers CALLBACK START f={_frameIndex}");
                try{ swapBuffersCallback?.Invoke(); }catch(Exception ex){ FLogPresent($"[VK] swapBuffers CALLBACK EX f={_frameIndex} {ex}"); }
                FLogPresent($"[VK] swapBuffers CALLBACK END f={_frameIndex}");

                _frameIndex++;
                FLogPresent($"[VK] Present END f={_frameIndex-1} -> next={_frameIndex}");
            }
            catch(Exception ex){ FLogPresent($"[VK] Present FAIL f={_frameIndex} {ex}"); _swapchainIsDirty = true; try{ FLogPresent($"[VK] swapBuffers CALLBACK START (FAIL PATH) f={_frameIndex}"); swapBuffersCallback?.Invoke(); FLogPresent($"[VK] swapBuffers CALLBACK END (FAIL PATH) f={_frameIndex}"); }catch{} }
        }

        public unsafe void ForcedPresentMagenta()
        {
            try
            {
                FLogPresent($"[VK] FORCED MAGENTA f={_frameIndex}");
                if(_swapchainIsDirty) RecreateSwapchain();
                uint nextImage = 0;
                int semIdx = _frameIndex % _imageAvailableSemaphores.Length;
                var acq = _gd.SwapchainApi.AcquireNextImage(_device, _swapchain, 1000000000, _imageAvailableSemaphores[semIdx], new Fence(), ref nextImage);
                var cbs = _gd.CommandBufferPool.Rent();
                Transition(cbs.CommandBuffer, _swapchainImages[nextImage], 0, AccessFlags.TransferWriteBit, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit);
                ClearColorValue clear = new(); clear.Float32_0 = 1f; clear.Float32_1 = 0f; clear.Float32_2 = 1f; clear.Float32_3 = 1f;
                ImageSubresourceRange range = new(ImageAspectFlags.ColorBit,0,1,0,1);
                _gd.Api.CmdClearColorImage(cbs.CommandBuffer, _swapchainImages[nextImage], ImageLayout.TransferDstOptimal, &clear, 1, &range);
                Transition(cbs.CommandBuffer, _swapchainImages[nextImage], AccessFlags.TransferWriteBit, 0, ImageLayout.TransferDstOptimal, ImageLayout.PresentSrcKhr, PipelineStageFlags.TransferBit, PipelineStageFlags.BottomOfPipeBit);
                _gd.CommandBufferPool.Return(cbs, [_imageAvailableSemaphores[semIdx]], [PipelineStageFlags.ColorAttachmentOutputBit], [_renderFinishedSemaphores[semIdx]]);
                Semaphore sem = _renderFinishedSemaphores[semIdx]; SwapchainKHR sc = _swapchain; Result pres;
                PresentInfoKHR pi = new(){ SType=StructureType.PresentInfoKhr, WaitSemaphoreCount=1, PWaitSemaphores=&sem, SwapchainCount=1, PSwapchains=&sc, PImageIndices=&nextImage, PResults=&pres };
                Result qr; lock(_gd.QueueLock){ qr = _gd.SwapchainApi.QueuePresent(_gd.Queue, in pi); }
                FLogPresent($"[VK] FORCED QueuePresent {qr}/{pres} f={_frameIndex}");
                _frameIndex++;
            }
            catch(Exception ex){ FLogPresent($"[VK] FORCED FAIL {ex}"); }
        }

        public override void SetAntiAliasing(AntiAliasing effect) { if (_currentAntiAliasing == effect && _effect!= null) return; _currentAntiAliasing = effect; _updateEffect = true; }
        public override void SetScalingFilter(ScalingFilter type){ if (type == ScalingFilter.Fsr) type = ScalingFilter.Bilinear; if (_currentScalingFilter == type && _scalingFilter!= null) return; _currentScalingFilter = type; _updateScalingFilter = true; }
        public override void SetColorSpacePassthrough(bool colorSpacePassthroughEnabled) { _colorSpacePassthroughEnabled = colorSpacePassthroughEnabled; _swapchainIsDirty = true; }
        private void UpdateEffect()
        {
            if (_updateEffect){ _updateEffect = false; switch (_currentAntiAliasing){ case AntiAliasing.Fxaa: _effect?.Dispose(); _effect = new FxaaPostProcessingEffect(_gd, _device); break; case AntiAliasing.None: _effect?.Dispose(); _effect = null; break; case AntiAliasing.SmaaLow: case AntiAliasing.SmaaMedium: case AntiAliasing.SmaaHigh: case AntiAliasing.SmaaUltra: int quality = _currentAntiAliasing - AntiAliasing.SmaaLow; if (_effect is SmaaPostProcessingEffect smaa) smaa.Quality = quality; else { _effect?.Dispose(); _effect = new SmaaPostProcessingEffect(_gd, _device, quality); } break; } }
            if (_updateScalingFilter){ _updateScalingFilter = false; switch (_currentScalingFilter){ case ScalingFilter.Bilinear: case ScalingFilter.Nearest: _scalingFilter?.Dispose(); _scalingFilter = null; _isLinear = _currentScalingFilter == ScalingFilter.Bilinear; break; case ScalingFilter.Fsr: _scalingFilter?.Dispose(); _scalingFilter = null; _isLinear = true; break; case ScalingFilter.Area: if (_scalingFilter is not AreaScalingFilter) { _scalingFilter?.Dispose(); _scalingFilter = new AreaScalingFilter(_gd, _device); } break; } }
        }
        public override void SetScalingFilterLevel(float level) { _scalingFilterLevel = level; _updateScalingFilter = true; }
        private unsafe void Transition(CommandBuffer commandBuffer, Image image, AccessFlags srcAccess, AccessFlags dstAccess, ImageLayout srcLayout, ImageLayout dstLayout, PipelineStageFlags srcStage, PipelineStageFlags dstStage) { ImageSubresourceRange subresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1); ImageMemoryBarrier barrier = new(){ SType = StructureType.ImageMemoryBarrier, SrcAccessMask = srcAccess, DstAccessMask = dstAccess, OldLayout = srcLayout, NewLayout = dstLayout, SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored, Image = image, SubresourceRange = subresourceRange, }; _gd.Api.CmdPipelineBarrier(commandBuffer, srcStage, dstStage, 0,0,null,0,null,1, in barrier); }
        public override void SetSize(int width, int height) { FLog($"[VK] SetSize {width}x{height} -> dirty v10.5 #555"); FLogPresent($"[VK] SetSize {width}x{height} -> dirty v10.5 #555"); if(width<=0 || height<=0) return; _requestedWidth=width; _requestedHeight=height; _swapchainIsDirty = true; }
        public override void ChangeVSyncMode(VSyncMode vSyncMode) { _vSyncMode = vSyncMode; _swapchainIsDirty = true; }
        protected virtual void Dispose(bool disposing){ if (disposing){ unsafe{ try{ if (_swapchainImageViews!= null) for (int i = 0; i < _swapchainImageViews.Length; i++) try { _swapchainImageViews[i]?.Dispose(); } catch {} if (_imageAvailableSemaphores!= null) for (int i = 0; i < _imageAvailableSemaphores.Length; i++) try { _gd.Api.DestroySemaphore(_device, _imageAvailableSemaphores[i], null); } catch {} if (_renderFinishedSemaphores!= null) for (int i = 0; i < _renderFinishedSemaphores.Length; i++) try { _gd.Api.DestroySemaphore(_device, _renderFinishedSemaphores[i], null); } catch {} try { _gd.SwapchainApi.DestroySwapchain(_device, _swapchain, null); } catch {} }catch{} } try { _effect?.Dispose(); } catch {} try { _scalingFilter?.Dispose(); } catch {} } }
        public override void Dispose() { Dispose(true); }
    }
}
