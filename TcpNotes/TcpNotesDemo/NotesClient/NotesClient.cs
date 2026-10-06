using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TcpNotes.Client;

/// <summary>
/// Подключается к серверу TcpNotes и обменивается однострочными командами и ответами.
/// </summary>
public sealed class NotesClient : IAsyncDisposable
{
    private readonly TcpClient _client = new();
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _disposed;

    /// <summary>
    /// Создаёт клиент с адресом целевого сервера.
    /// </summary>
    /// <param name="host">DNS-имя или IP-адрес сервера; не может быть пустым.</param>
    /// <param name="port">TCP-порт сервера от 1 до 65535.</param>
    /// <exception cref="ArgumentException">
    /// Возникает, если <paramref name="host"/> пуст или состоит только из пробелов.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Возникает, если <paramref name="port"/> не входит в диапазон от 1 до 65535.
    /// </exception>
    public NotesClient(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Адрес сервера не может быть пустым.", nameof(host));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(port, IPEndPoint.MinPort + 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);
        Host = host;
        Port = port;
    }

    /// <summary>
    /// Получает DNS-имя или IP-адрес сервера.
    /// </summary>
    public string Host { get; }

    /// <summary>
    /// Получает TCP-порт сервера.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Получает признак установленного TCP-соединения.
    /// </summary>
    public bool IsConnected => _client.Connected && _reader is not null && _writer is not null;

    /// <summary>
    /// Устанавливает TCP-соединение и подготавливает текстовые потоки UTF-8.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены попытки подключения.</param>
    /// <returns>Задача, завершающаяся после успешного подключения.</returns>
    /// <exception cref="ObjectDisposedException">
    /// Возникает, если клиент уже освобождён.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Возникает, если клиент уже подключён.
    /// </exception>
    /// <exception cref="SocketException">
    /// Возникает, если не удалось установить TCP-соединение.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Возникает, если <paramref name="cancellationToken"/> отменён.
    /// </exception>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _client.ConnectAsync(Host, Port, cancellationToken);

        NetworkStream stream = _client.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        _writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };
    }

    /// <summary>
    /// Отправляет серверу команду добавления заметки.
    /// </summary>
    /// <param name="text">
    /// Текст заметки; не может быть пустым, состоять только из пробелов или содержать перевод строки.
    /// </param>
    /// <param name="cancellationToken">Токен отмены сетевого обмена.</param>
    /// <returns>Однострочный ответ сервера на команду <c>ADD</c>.</returns>
    /// <exception cref="ArgumentException">
    /// Возникает, если <paramref name="text"/> пуст, состоит только из пробелов
    /// или содержит символ перевода строки.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Возникает, если соединение с сервером ещё не установлено.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Возникает, если клиент уже освобождён.
    /// </exception>
    /// <exception cref="IOException">
    /// Возникает при ошибке сетевого обмена или если сервер закрыл соединение до ответа.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Возникает, если <paramref name="cancellationToken"/> отменён.
    /// </exception>
    public Task<string> AddNoteAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Текст заметки не может быть пустым.", nameof(text));
        }

        if (text.Contains('\r') || text.Contains('\n'))
        {
            throw new ArgumentException("Текст заметки должен занимать одну строку.", nameof(text));
        }

        return SendCommandAsync($"ADD {text.Trim()}", cancellationToken);
    }

    /// <summary>
    /// Запрашивает у сервера общий список заметок.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены сетевого обмена.</param>
    /// <returns>Однострочный ответ сервера, начинающийся с <c>NOTES</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Возникает, если соединение с сервером ещё не установлено.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Возникает, если клиент уже освобождён.
    /// </exception>
    /// <exception cref="IOException">
    /// Возникает при ошибке сетевого обмена или если сервер закрыл соединение до ответа.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Возникает, если <paramref name="cancellationToken"/> отменён.
    /// </exception>
    public Task<string> ListNotesAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync("LIST", cancellationToken);

    /// <summary>
    /// Отправляет произвольную непустую строку протокола и ожидает один ответ.
    /// </summary>
    /// <param name="command">
    /// Непустая команда без символов перевода строки.
    /// </param>
    /// <param name="cancellationToken">Токен отмены сетевого обмена.</param>
    /// <returns>Ответ сервера без завершающего символа перевода строки.</returns>
    /// <exception cref="ArgumentException">
    /// Возникает, если <paramref name="command"/> пуст, состоит только из пробелов
    /// или содержит символ перевода строки.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Возникает, если соединение с сервером ещё не установлено.
    /// </exception>
    /// <exception cref="IOException">
    /// Возникает, если сервер закрыл соединение до отправки ответа.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Возникает, если клиент уже освобождён.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Возникает, если <paramref name="cancellationToken"/> отменён.
    /// </exception>
    public async Task<string> SendCommandAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Команда не может быть пустой.", nameof(command));
        }

        if (command.Contains('\r') || command.Contains('\n'))
        {
            throw new ArgumentException("Команда должна занимать одну строку.", nameof(command));
        }

        if (_reader is null || _writer is null)
        {
            throw new InvalidOperationException("Сначала установите соединение с сервером.");
        }

        await _writer.WriteLineAsync(command.AsMemory(), cancellationToken);
        return await _reader.ReadLineAsync(cancellationToken)
            ?? throw new IOException("Сервер закрыл соединение до отправки ответа.");
    }

    /// <summary>
    /// Закрывает текстовые потоки и TCP-соединение.
    /// </summary>
    /// <returns>Завершённая задача освобождения ресурсов.</returns>
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _reader?.Dispose();
            _writer?.Dispose();
            _client.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
