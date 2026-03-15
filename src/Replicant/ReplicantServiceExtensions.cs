namespace Replicant;

/// <summary>
/// Extension methods for registering Replicant services with dependency injection.
/// </summary>
public static class ReplicantServiceExtensions
{
    /// <summary>
    /// Register a <see cref="ReplicantCache"/> singleton for use with <see cref="ReplicantHandler"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="directory">The directory to store cache files.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    /// <exception cref="InvalidOperationException">Thrown if a <see cref="ReplicantCache"/> has already been registered.</exception>
    public static IServiceCollection AddReplicantCache(
        this IServiceCollection services,
        string directory,
        int maxEntries = 1000)
    {
        if (services.Any(_ => _.ServiceType == typeof(ReplicantCache)))
        {
            throw new("A ReplicantCache has already been registered.");
        }

        services.AddSingleton(_ => new ReplicantCache(directory, maxEntries));
        return services;
    }

    /// <summary>
    /// Add a <see cref="ReplicantHandler"/> to the HTTP client pipeline
    /// using the registered <see cref="ReplicantCache"/> singleton.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <param name="staleIfError">If true, return stale cached content when the server returns an error.</param>
    public static IHttpClientBuilder AddReplicantCaching(
        this IHttpClientBuilder builder,
        bool staleIfError = false)
    {
        builder.AddHttpMessageHandler(
            provider =>
            {
                var cache = provider.GetService<ReplicantCache>();
                if (cache != null)
                {
                    return new ReplicantHandler(cache, staleIfError);
                }

                throw new("No ReplicantCache has been registered. Call services.AddReplicantCache() before AddReplicantCaching().");
            });
        return builder;
    }

    /// <summary>
    /// Register a <see cref="ReplicantDistributedCache"/> as <see cref="IDistributedCache"/>
    /// for use as an L2 cache backend with HybridCache.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="directory">The directory to store cache files.</param>
    /// <param name="maxEntries">The maximum entries to store in the cache.</param>
    public static IServiceCollection AddReplicantDistributedCache(
        this IServiceCollection services,
        string directory,
        int maxEntries = 1000)
    {
        services.AddSingleton<IDistributedCache>(_ => new ReplicantDistributedCache(directory, maxEntries));
        return services;
    }
}
