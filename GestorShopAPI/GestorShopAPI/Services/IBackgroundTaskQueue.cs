namespace GestorShopAPI.Services;

public interface IBackgroundTaskQueue
{
    void Encolar(Func<IServiceProvider, CancellationToken, Task> tarea);
    Task<Func<IServiceProvider, CancellationToken, Task>> DesencolarAsync(CancellationToken ct);
}
