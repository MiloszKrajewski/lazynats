using Microsoft.Extensions.DependencyInjection;

namespace lazynats;

public static class Services
{
    private static IServiceProvider _provider = null!;
    
    public static void Configure(IServiceCollection collection) => 
        _provider = collection.BuildServiceProvider();
    
    public static IServiceProvider Root => _provider;
}
