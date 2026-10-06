using System.Net.Sockets;

namespace TcpNotes.Client;

internal static class Program
{
    private const string Host = "127.0.0.1";
    private const int Port = 5000;

    private static async Task Main()
    {
        try
        {
            await using var client = new NotesClient(Host, Port);
            await client.ConnectAsync();
            Console.WriteLine($"Подключено к серверу {Host}:{Port}");
            Console.WriteLine("Команды: ADD <текст>, LIST, EXIT");

            while (true)
            {
                Console.Write("> ");
                string? command = Console.ReadLine();

                if (command is null || command.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(command))
                {
                    continue;
                }

                Console.WriteLine(await client.SendCommandAsync(command));
            }
        }
        catch (SocketException)
        {
            Console.WriteLine("Не удалось подключиться к серверу. Проверьте, что он запущен.");
        }
        catch (IOException)
        {
            Console.WriteLine("Соединение с сервером прервано.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Непредвиденная ошибка: {exception.Message}");
        }
    }
}
