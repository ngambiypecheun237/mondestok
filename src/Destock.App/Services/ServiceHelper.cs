using Microsoft.Extensions.DependencyInjection;

namespace Destock.App.Services;

public static class ServiceHelper
{
    private static IServiceProvider? _services;

    public static void Initialize(IServiceProvider services) => _services = services;

    public static T Get<T>() where T : notnull
    {
        if (_services is null)
        {
            throw new InvalidOperationException("Le conteneur de services n'est pas initialisé.");
        }
        return _services.GetRequiredService<T>();
    }
}
