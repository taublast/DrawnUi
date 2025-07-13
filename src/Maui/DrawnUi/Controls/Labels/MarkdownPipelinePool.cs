/*

using System.Collections.Concurrent;
using Markdig;
using static System.Net.Mime.MediaTypeNames;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Provides a thread-safe pool of MarkdownPipeline instances for SkiaRichLabel.
    /// </summary>
    public static class MarkdownPipelinePool
    {
        private static readonly ConcurrentBag<MarkdownPipeline> _pipelinePool = new();
        private static int _activePipelineCount = 0;

        static MarkdownPipelinePool()
        {
            _pipelinePool.Add(CreatePipeline());
            _pipelinePool.Add(CreatePipeline());
        }

        /// <summary>
        /// Acquires a MarkdownPipeline from the pool. If the pool is empty, a new pipeline is created.
        /// </summary>
        /// <returns>A MarkdownPipeline instance.</returns>
        public static MarkdownPipeline AcquirePipeline()
        {
            if (_pipelinePool.TryTake(out var pipeline))
            {
                Interlocked.Increment(ref _activePipelineCount);
                //Debug.WriteLine($"Active Pipelines: {_activePipelineCount}");
                return pipeline;
            }

            var newPipeline = CreatePipeline();
            Interlocked.Increment(ref _activePipelineCount);
            //Debug.WriteLine($"Active Pipelines: {_activePipelineCount}");
            return newPipeline;
        }

        /// <summary>
        /// Returns a MarkdownPipeline to the pool.
        /// </summary>
        /// <param name="pipeline">The MarkdownPipeline to return.</param>
        public static void ReleasePipeline(MarkdownPipeline pipeline)
        {
            if (pipeline != null)
            {
                _pipelinePool.Add(pipeline);
                Interlocked.Decrement(ref _activePipelineCount);
                //Debug.WriteLine($"Active Pipelines: {_activePipelineCount}");
            }
        }

        /// <summary>
        /// Creates a new MarkdownPipeline instance.
        /// </summary>
        /// <returns>A new MarkdownPipeline.</returns>
        public static MarkdownPipeline CreatePipeline()
        {
            return new MarkdownPipelineBuilder()
                .UseEmphasisExtras()
                .Build();
        }
    }
}

*/
