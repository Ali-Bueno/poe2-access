namespace Poe2Accessibility.Services
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>The worker thread of <see cref="PrismOutputService"/>: running PRISM, taking requests off the queue and executing them.</summary>
    public sealed partial class PrismOutputService
    {
        private void Run(string pluginDirectory, TaskCompletionSource<bool> ready)
        {
            PrismLibrary prism;
            try
            {
                prism = PrismLibrary.Load(Path.Join(pluginDirectory, PrismLibrary.FileName));
                var failure = prism.Start(message => this.log.Info(message));
                foreach (var (name, priority) in prism.Backends())
                {
                    this.log.Debug($"PRISM backend registered: {name} (priority {priority})");
                }

                if (prism.LibraryConfigVersion != PrismLibrary.ConfigVersion)
                {
                    this.log.Warn($"prism.dll {prism.Version} writes config version {prism.LibraryConfigVersion}; the binding matches {PrismLibrary.ConfigVersion}");
                }

                if (failure != null)
                {
                    this.log.Error($"PRISM {prism.Version}: {failure}");
                    prism.Dispose();
                    ready.SetResult(false);
                    return;
                }

                this.BackendName = prism.BackendName;
                var wine = PrismLibrary.WineVersion();
                this.log.Info($"PRISM {prism.Version} initialised, backend: {prism.BackendName}" + (wine == null ? string.Empty : $" (under Wine {wine})"));
            }
            catch (Exception ex)
            {
                this.log.Error("PRISM initialisation failed", ex);
                ready.SetResult(false);
                return;
            }

            ready.SetResult(true);
            try
            {
                var waitMs = Timeout.Infinite;
                while (!this.stopping)
                {
                    this.pendingSignal.WaitOne(waitMs);
                    waitMs = this.ReleaseWaiting();
                    while (!this.stopping && this.TryDequeue(out var request))
                    {
                        this.Execute(prism, request);
                    }
                }
            }
            finally
            {
                // An exception escaping this thread would end the host process.
                try
                {
                    prism.Dispose();
                }
                catch (Exception ex)
                {
                    this.log.Error("PRISM shutdown failed", ex);
                }
            }
        }

        private bool TryDequeue(out SpeechRequest request)
        {
            lock (this.queueGate)
            {
                return this.pending.TryDequeue(out request);
            }
        }

        private void Execute(PrismLibrary prism, SpeechRequest request)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                var error = request.Text == null ? prism.Stop() : prism.Speak(request.Text, request.Interrupt);
                if (error != 0 && request.Text != null)
                {
                    this.log.Error($"Speak failed: {prism.ErrorText(error)}");
                }
                else if (error != 0)
                {
                    this.log.Debug($"Stop: {prism.ErrorText(error)}"); // some backends call a stop with nothing to stop an error
                }
            }
            catch (Exception ex)
            {
                // An exception escaping this thread would end the host process.
                this.log.Error(request.Text == null ? "Stop failed" : "Speak failed", ex);
            }

            if (timer.ElapsedMilliseconds > SlowCallMs)
            {
                this.log.Debug($"PRISM call took {timer.ElapsedMilliseconds} ms");
            }
        }

        private enum Delivery
        {
            Normal,
            Urgent,
            Kept,
            KeptMessage,
            Move,
            Floor,
        }

        private readonly record struct SpeechRequest(string? Text, bool Interrupt);
    }
}
