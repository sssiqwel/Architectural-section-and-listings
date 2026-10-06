# TcpNotes

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Tests: 3](https://img.shields.io/badge/tests-3%20passed-2EA44F)](TcpNotesDemo/TcpNotes.Tests/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

TcpNotes — учебное клиент-серверное приложение для совместной работы с заметками по TCP. Сервер хранит единый список в оперативной памяти, а несколько консольных клиентов добавляют заметки и запрашивают актуальный список. Обмен выполняется строками UTF-8 через `127.0.0.1:5000`.

## Возможности

- одновременное обслуживание нескольких TCP-клиентов;
- добавление заметок командой `ADD <текст>`;
- получение общего упорядоченного списка командой `LIST`;
- понятные ответы `OK`, `NOTES` и `ERROR`;
- корректная обработка отключения клиента и остановки сервера;
- потокобезопасное хранение заметок в памяти;
- XML-документация публичного API рядом со сборками;
- модульные и интеграционные проверки, включая обмен с двумя клиентами.

> Данные существуют только во время работы сервера. После его перезапуска список пуст.

## Требования

- Windows, Linux или macOS;
- [.NET SDK 10.0.401](https://dotnet.microsoft.com/download/dotnet/10.0) либо совместимый SDK 10.0;
- свободный локальный TCP-порт `5000`.

## Установка

```bash
git clone https://github.com/sssiqwel/TcpNotes.git
cd TcpNotes
dotnet restore TcpNotes.slnx
dotnet build TcpNotes.slnx --configuration Release
```

Версия SDK закреплена в [`TcpNotesDemo/global.json`](TcpNotesDemo/global.json).

## Запуск

### 1. Сервер

Откройте первый терминал в корне репозитория:

```bash
dotnet run --project TcpNotesDemo/NotesServer/NotesServer.csproj
```

После строки `Сервер заметок запущен: 127.0.0.1:5000` оставьте терминал открытым. Для остановки нажмите `Ctrl+C`.

### 2. Первый клиент

Откройте второй терминал:

```bash
dotnet run --project TcpNotesDemo/NotesClient/NotesClient.csproj
```

### 3. Второй клиент

Откройте третий терминал и повторите ту же команду:

```bash
dotnet run --project TcpNotesDemo/NotesClient/NotesClient.csproj
```

Оба процесса используют один проект клиента, но создают независимые TCP-подключения к общему серверу.

## Использование

В приглашении клиента (`>`) доступны команды:

| Команда | Назначение | Пример ответа |
|---|---|---|
| `ADD <текст>` | Добавить непустую заметку | `OK Заметка добавлена. Всего заметок: 1.` |
| `LIST` | Показать все заметки | `NOTES 1. Купить молоко \| 2. Подготовить отчёт` |
| `EXIT` | Закрыть клиент | Клиент завершает работу |

Пример:

```text
> ADD Купить молоко
OK Заметка добавлена. Всего заметок: 1.
> LIST
NOTES 1. Купить молоко
> EXIT
```

Неизвестная команда возвращает подсказку, а `ADD` без текста — сообщение об ошибке. Команды и ответы занимают по одной строке. Регистр имени команды не учитывается.
Публичный клиентский API отклоняет `\r` и `\n` внутри команды или текста заметки, поэтому один вызов не может внедрить вторую команду и нарушить соответствие «запрос — ответ».

## Проверка

```bash
dotnet test TcpNotes.slnx --configuration Release
```

Интеграционная проверка запускает настоящий сервер, подключает два экземпляра клиента и подтверждает доступность общего списка.

## Структура

```text
TcpNotes.slnx
├── TcpNotesDemo/
│   ├── NotesServer/       TCP-сервер, обработчик команд и хранилище
│   ├── NotesClient/       консольный TCP-клиент
│   └── TcpNotes.Tests/    модульные и интеграционные проверки
└── docs/
    ├── diagrams/          исходники diagrams.net и PNG
    └── images/            снимки фактических запусков
```

## Документация

- [Руководство оператора по ГОСТ 19.505-79 — DOCX](docs/user-guide-gost-19-505-79.docx)
- [Техническое задание по ГОСТ 19.201-78 — DOCX](docs/technical-specification-gost-19-201-78.docx) · [редактируемый исходник Markdown](docs/technical-specification-gost-19-201-78.md)
- [Диаграмма классов — PNG](docs/diagrams/class-diagram.png) · [исходник diagrams.net](docs/diagrams/class-diagram.drawio)
- [Диаграмма последовательности — PNG](docs/diagrams/sequence-diagram.png) · [исходник diagrams.net](docs/diagrams/sequence-diagram.drawio)
- [Ответы на вопросы по проекту](docs/questions-and-answers.txt)
- [Снимок сервера](docs/images/server-running.png), [клиента 1](docs/images/client-1-running.png) и [клиента 2](docs/images/client-2-running.png)

## Лицензия

Проект распространяется по лицензии [MIT](LICENSE).