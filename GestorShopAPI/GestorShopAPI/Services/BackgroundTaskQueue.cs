using System.Threading.Channels;

namespace GestorShopAPI.Services;

// Cola de trabajo en memoria (equivalente conceptual a una cola tipo
// RabbitMQ/Redis Queue, pero sin infraestructura externa, ideal para el
// taller). Cada tarea encolada se ejecuta de forma asíncrona por el
// QueuedHostedService, sin bloquear la petición HTTP que la generó.
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _cola;

    public BackgroundTaskQueue()
    {
        _cola = Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>();
    }

    public void Encolar(Func<IServiceProvider, CancellationToken, Task> tarea)
    {
        if (tarea is null) throw new ArgumentNullException(nameof(tarea));
        _cola.Writer.TryWrite(tarea);
    }

    public async Task<Func<IServiceProvider, CancellationToken, Task>> DesencolarAsync(CancellationToken ct)
    {
        return await _cola.Reader.ReadAsync(ct);
    }
}
