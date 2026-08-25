using NUnit.Framework;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.GAL.Multithreading;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;

namespace Ryujinx.Tests.Graphics
{
    class ThreadedRendererTests
    {
        private const string DisposeFailureMessage = "Renderer disposal failed.";

        public class TestRendererProxy : DispatchProxy
        {
            public ConcurrentQueue<(string Name, int ThreadId)> Calls { get; } = new();
            public bool ThrowOnDispose { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name is nameof(IRenderer.PreFrame) or nameof(IDisposable.Dispose))
                {
                    Calls.Enqueue((targetMethod.Name, Environment.CurrentManagedThreadId));
                }

                if (targetMethod.Name == nameof(IDisposable.Dispose) && ThrowOnDispose)
                {
                    throw new InvalidOperationException(DisposeFailureMessage);
                }

                return targetMethod.ReturnType != typeof(void) && targetMethod.ReturnType.IsValueType
                    ? Activator.CreateInstance(targetMethod.ReturnType)
                    : null;
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        [Timeout(20000)]
        public void DisposeRunsOnBackendThreadAfterQueuedCommands(bool throwOnDispose)
        {
            IRenderer baseRenderer = DispatchProxy.Create<IRenderer, TestRendererProxy>();
            TestRendererProxy rendererProxy = (TestRendererProxy)baseRenderer;
            rendererProxy.ThrowOnDispose = throwOnDispose;
            ThreadedRenderer threadedRenderer = new(baseRenderer);

            using ManualResetEventSlim gpuThreadStarted = new();
            using ManualResetEventSlim releaseGpuThread = new();

            int backendThreadId = 0;
            Thread backendThread = new(() =>
            {
                backendThreadId = Environment.CurrentManagedThreadId;
                threadedRenderer.RunLoop(() =>
                {
                    gpuThreadStarted.Set();
                    releaseGpuThread.Wait();
                });
            })
            {
                IsBackground = true,
            };

            backendThread.Start();

            bool rendererDisposeAttempted = false;

            try
            {
                Assert.That(gpuThreadStarted.Wait(TimeSpan.FromSeconds(5)), Is.True);

                threadedRenderer.PreFrame();
                releaseGpuThread.Set();
                rendererDisposeAttempted = true;

                Exception disposeException = null;

                try
                {
                    threadedRenderer.Dispose();
                }
                catch (Exception exception)
                {
                    disposeException = exception;
                }

                Assert.That(disposeException, throwOnDispose
                    ? Is.TypeOf<InvalidOperationException>().With.Message.EqualTo(DisposeFailureMessage)
                    : Is.Null);
                Assert.That(backendThread.IsAlive, Is.False);
                Assert.That(rendererProxy.Calls, Is.EqualTo(new[]
                {
                    (nameof(IRenderer.PreFrame), backendThreadId),
                    (nameof(IDisposable.Dispose), backendThreadId),
                }));
            }
            finally
            {
                releaseGpuThread.Set();

                // If the startup assertion fails on a busy test runner, give the GPU thread a chance
                // to start and then stop the renderer before the test-owned wait handles are disposed.
                if (!rendererDisposeAttempted && gpuThreadStarted.Wait(TimeSpan.FromSeconds(5)))
                {
                    try
                    {
                        threadedRenderer.Dispose();
                    }
                    catch
                    {
                    }
                }

                backendThread.Join(TimeSpan.FromSeconds(5));
            }
        }
    }
}
