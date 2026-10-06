using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TcpNotes.Server;

/// <summary>
/// Принимает TCP-подключения и обслуживает команды общего списка заметок.
/// </summary>
public sealed class NotesServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CommandProcessor _processor;
    private readonly ConcurrentDictionary<int, Task> _clients = new();
    private int _nextClientId;
    private bool _disposed;

    /// <summary>
    /// Создаёт TCP-сервер, привязанный к заданному адресу и порту.
    /// </summary>
    /// <param name="address">Локальный IP-адрес для прослушивания.</param>
    /// <param name="port">TCP-порт от 1 до 65535.</param>
    /// <exception cref="ArgumentNullException">
    /// Возникает, если <paramref name="address"/> равен <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Возникает, если <paramref name="port"/> не входит в диапазон от 1 до 65535.
    /// </exception>
    public NotesServer(IPAddress address, int port)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, IPEndPoint.MinPort + 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);

        Address = address;
        Port = port;
        _listener = new TcpListener(address, port);
        _processor = new CommandProcessor(new NotesRepository());
    }

    /// <summary>
    /// Получает локальный IP-адрес, настроенный для сервера.
    /// </summary>
    public IPAddress Address { get; }

    /// <summary>
    /// Получает TCP-порт, настроенный для сервера.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Запускает сервер и обслуживает клиентов до отмены операции.
    /// </summary>
    /// <param name="cancellationToken">Токен штатной остановки цикла приёма подключений.</param>
    /// <returns>Задача, завершающаяся после остановки сервера и всех активных обработчиков.</returns>
    /// <exception cref="ObjectDisposedException">
    /// Возникает, если экземпляр уже освобождён.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Возникает при отмене <paramref name="cancellationToken"/> после завершения
    /// активных обработчиков клиентов.
    /// </exception>
    /// <exception cref="SocketException">
    /// Возникает, если серверу не удалось начать прослушивание или принять подключение.
    /// </exception>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _listener.Start();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
                Console.WriteLine($"Подключился клиент: {client.Client.RemoteEndPoint}");

                int clientId = Interlocked.Increment(ref _nextClientId);
                Task task = HandleClientAsync(client, cancellationToken);
                _clients[clientId] = task;
                _ = task.ContinueWith(
                    completedTask => _clients.TryRemove(clientId, out _),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        finally
        {
            _listener.Stop();
            await Task.WhenAll(_clients.Values);
        }
    }

    /// <summary>
    /// Останавливает прослушивание порта и освобождает сетевые ресурсы сервера.
    /// </summary>
    /// <returns>Завершённая задача освобождения ресурсов.</returns>
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _listener.Stop();
        }

        return ValueTask.CompletedTask;
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        EndPoint? remoteEndPoint = client.Client.RemoteEndPoint;

        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true
            })
            {
                while (await reader.ReadLineAsync(cancellationToken) is { } command)
                {
                    string response = _processor.Process(command);
                    await writer.WriteLineAsync(response.AsMemory(), cancellationToken);
                }
            }
        }
        catch (IOException)
        {
            // Клиент мог закрыть программу или сеть могла оборваться.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Штатная остановка сервера.
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Ошибка при работе с клиентом: {exception.Message}");
        }
        finally
        {
            Console.WriteLine($"Клиент отключился: {remoteEndPoint}");
        }
    }
}
