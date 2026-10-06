using System.Net;
using System.Net.Sockets;
using TcpNotes.Client;
using TcpNotes.Server;

namespace TcpNotes.Tests;

/// <summary>
/// Проверяет обработку строковых команд без сетевого транспорта.
/// </summary>
public sealed class CommandProcessorTests
{
    /// <summary>
    /// Создаёт набор проверок обработки команд.
    /// </summary>
    public CommandProcessorTests()
    {
    }

    /// <summary>
    /// Проверяет добавление заметок и сохранение порядка в ответе LIST.
    /// </summary>
    [Fact]
    public void Process_AddAndList_ReturnsOrderedNotes()
    {
        var processor = new CommandProcessor(new NotesRepository());

        Assert.Equal("OK Заметка добавлена. Всего заметок: 1.", processor.Process("ADD Первая"));
        Assert.Equal("OK Заметка добавлена. Всего заметок: 2.", processor.Process("add Вторая"));
        Assert.Equal("NOTES 1. Первая | 2. Вторая", processor.Process("LIST"));
    }

    /// <summary>
    /// Проверяет ответы на пустую и неизвестную команды.
    /// </summary>
    [Fact]
    public void Process_InvalidCommands_ReturnsErrors()
    {
        var repository = new NotesRepository();
        var processor = new CommandProcessor(repository);

        Assert.StartsWith("ERROR", processor.Process("ADD   "));
        Assert.StartsWith("ERROR", processor.Process("DELETE 1"));
        Assert.Throws<ArgumentException>(() => processor.Process("LIST\nADD Внедрённая"));
        Assert.Throws<ArgumentException>(() => repository.Add("Две\nстроки"));
    }
}

/// <summary>
/// Проверяет реальный TCP-обмен сервера с двумя экземплярами клиента.
/// </summary>
public sealed class NetworkIntegrationTests
{
    /// <summary>
    /// Создаёт набор интеграционных проверок TCP-обмена.
    /// </summary>
    public NetworkIntegrationTests()
    {
    }

    /// <summary>
    /// Проверяет, что заметки от двух клиентов доступны в общем списке.
    /// </summary>
    /// <returns>Задача полного сетевого сценария.</returns>
    [Fact]
    public async Task TwoClients_AddNotes_BothSeeSharedList()
    {
        int port = GetAvailablePort();
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var server = new NotesServer(IPAddress.Loopback, port);
        Task serverTask = server.RunAsync(shutdown.Token);

        await using var firstClient = new NotesClient("127.0.0.1", port);
        await using var secondClient = new NotesClient("127.0.0.1", port);

        await firstClient.ConnectAsync(shutdown.Token);
        await secondClient.ConnectAsync(shutdown.Token);

        await Assert.ThrowsAsync<ArgumentException>(() => firstClient.AddNoteAsync("Первая\nLIST", shutdown.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => firstClient.SendCommandAsync("LIST\r\nLIST", shutdown.Token));
        Assert.StartsWith("OK", await firstClient.AddNoteAsync("Первая заметка", shutdown.Token));
        Assert.StartsWith("OK", await secondClient.AddNoteAsync("Вторая заметка", shutdown.Token));

        string response = await firstClient.ListNotesAsync(shutdown.Token);
        Assert.Equal("NOTES 1. Первая заметка | 2. Вторая заметка", response);

        shutdown.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serverTask);
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
