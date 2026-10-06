namespace TcpNotes.Server;

/// <summary>
/// Хранит заметки в памяти и обеспечивает потокобезопасный доступ к ним.
/// </summary>
public sealed class NotesRepository
{
    private readonly List<string> _notes = [];
    private readonly object _syncRoot = new();

    /// <summary>
    /// Создаёт пустое хранилище заметок в оперативной памяти.
    /// </summary>
    public NotesRepository()
    {
    }

    /// <summary>
    /// Получает текущее количество заметок.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _notes.Count;
            }
        }
    }

    /// <summary>
    /// Добавляет непустую однострочную заметку в конец общего списка.
    /// </summary>
    /// <param name="text">
    /// Текст заметки; не может быть пустым, состоять только из пробелов или содержать перевод строки.
    /// </param>
    /// <returns>Количество заметок после добавления.</returns>
    /// <exception cref="ArgumentException">
    /// Возникает, если <paramref name="text"/> пуст, состоит только из пробелов
    /// или содержит символ перевода строки.
    /// </exception>
    public int Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Текст заметки не может быть пустым.", nameof(text));
        }

        if (text.Contains('\r') || text.Contains('\n'))
        {
            throw new ArgumentException("Текст заметки должен занимать одну строку.", nameof(text));
        }

        lock (_syncRoot)
        {
            _notes.Add(text);
            return _notes.Count;
        }
    }

    /// <summary>
    /// Возвращает неизменяемый снимок заметок в порядке их добавления.
    /// </summary>
    /// <returns>Копия текущего списка заметок.</returns>
    public IReadOnlyList<string> GetAll()
    {
        lock (_syncRoot)
        {
            return _notes.ToArray();
        }
    }
}
