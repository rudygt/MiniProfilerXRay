using System;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Profiling.Internal;

namespace MiniProfilerXRay.EntityFrameworkCore
{
    /// <summary>
    /// Extension methods for the MiniProfiler.EntityFrameworkCore.
    /// </summary>
    public static class MiniProfilerServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Entity Framework Core profiling for MiniProfiler via DiagnosticListener.
        /// </summary>
        /// <param name="builder">The <see cref="IMiniProfilerBuilder" /> to add services to.</param>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <c>null</c>.</exception>
        public static IMiniProfilerBuilder AddXRayEntityFramework(this IMiniProfilerBuilder builder)
        {
            _ = builder ?? throw new ArgumentNullException(nameof(builder));

            builder.Services.AddSingleton<IMiniProfilerDiagnosticListener, RelationalDiagnosticListener>();

            return builder;
        }
    }
}
