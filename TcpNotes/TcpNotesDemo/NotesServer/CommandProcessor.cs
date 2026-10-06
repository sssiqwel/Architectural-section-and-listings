namespace TcpNotes.Server;

/// <summary>
/// Разбирает однострочные команды протокола TcpNotes и формирует однострочные ответы.
/// </summary>
public sealed class CommandProcessor
{
    private readonly NotesRepository _repository;

    /// <summary>
    /// Создаёт обработчик команд для указанного хранилища.
    /// </summary>
    /// <param name="repository">Общее потокобезопасное хранилище заметок.</param>
    /// <exception cref="ArgumentNullException">
    /// Возникает, если <paramref name="repository"/> равен <see langword="null"/>.
    /// </exception>
    public CommandProcessor(NotesRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Обрабатывает команду <c>ADD &lt;текст&gt;</c> или <c>LIST</c>.
    /// </summary>
    /// <param name="command">Одна строка команды без символа перевода строки.</param>
    /// <returns>Ответ протокола, начинающийся с <c>OK</c>, <c>NOTES</c> или <c>ERROR</c>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Возникает, если <paramref name="command"/> равен <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Возникает, если <paramref name="command"/> содержит символ перевода строки
    /// и поэтому не является одной командой протокола.
    /// </exception>
    public string Process(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Contains('\r') || command.Contains('\n'))
        {
            throw new ArgumentException("Команда должна занимать одну строку.", nameof(command));
        }

        if (command.StartsWith("ADD ", StringComparison.OrdinalIgnoreCase))
        {
            string text = command[4..].Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return "ERROR Текст заметки не может быть пустым.";
            }

            int count = _repository.Add(text);
            return $"OK Заметка добавлена. Всего заметок: {count}.";
        }

        if (command.Equals("LIST", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<string> notes = _repository.GetAll();
            return notes.Count == 0
                ? "NOTES (список пуст)"
                : "NOTES " + string.Join(" | ", notes.Select((note, index) => $"{index + 1}. {note}"));
        }

        return "ERROR Неизвестная команда. Используйте: ADD <текст> или LIST.";
    }
}
