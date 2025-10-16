using Microsoft.Extensions.DependencyInjection;

namespace KuyumculukTakipProgrami.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Uygulama katmanı bağımlılıkları buraya eklenecek
        return services;
    }
}

