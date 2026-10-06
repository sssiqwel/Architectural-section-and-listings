using System.Net;

namespace TcpNotes.Server;

internal static class Program
{
    private const int Port = 5000;

    private static async Task Main()
    {
        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        await using var server = new NotesServer(IPAddress.Loopback, Port);
        Console.WriteLine($"Сервер заметок запущен: 127.0.0.1:{Port}");
        Console.WriteLine("Для остановки нажмите Ctrl+C.");

        try
        {
            await server.RunAsync(shutdown.Token);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            Console.WriteLine("Сервер остановлен.");
        }
    }
}
