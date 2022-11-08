using FFmpeg.NET;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TodoTelegramAssistant;
using Vosk;
using File = System.IO.File;
using System.Speech.Synthesis;
using System.Speech.AudioFormat;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types.ReplyMarkups;

var configuration = new ConfigurationBuilder()
.AddUserSecrets<Program>().Build();

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddLocalization(options =>
        {
            options.ResourcesPath = "Resources";
        });
        services.AddTransient<LocalizationService>();
    }).Build();

IServiceProvider services = host.Services;
LocalizationService localizationService = services.GetRequiredService<LocalizationService>();

var botClient = new TelegramBotClient(configuration["TelegramBotApiKey"]);

using var cts = new CancellationTokenSource();

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
};

Model ukrainianModel = new Model(configuration["UkrainianSTTModelPath"]);
Model englishModel = new Model(configuration["EnglishSTTModelPath"]);

var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
path = Path.GetDirectoryName(path);

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);
var me = await botClient.GetMeAsync();

Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();

cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    TodoController todoController = new TodoController();
    if (update.CallbackQuery != null)
    {
        long id = update.CallbackQuery.Message.Chat.Id;
        int todoId = int.Parse(update.CallbackQuery.Data);
        todoController.DeleteTodo(id, todoId);

        UpdateLocalization(todoController.GetLocalization(id));

        await botClient.SendTextMessageAsync(
        chatId: id,
        text: localizationService.GetFormattedMessage("TaskDeleted"),
        cancellationToken: cancellationToken);

        return;
    }
    if (update.Message is not { } message)
    {
        return;
    }

    var chatId = message.Chat.Id;
    UpdateLocalization(todoController.GetLocalization(chatId));
    if (message.Text is null && message.Voice is null)
    {
        await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: localizationService.GetFormattedMessage("NotSupportedMessage"),
        cancellationToken: cancellationToken);
        return;
    }
    string text;
    if (message.Voice is not null)
    {
        text = await ParseUserAudio(message.Voice.FileId);
    }
    else
    {
        text = message.Text;
    }
    string command = "/switchlang";
    text = text.ToLower();
    if (text.StartsWith(command, true, CultureInfo.InvariantCulture))
    {
        string localization = text.Remove(0, command.Count());
        todoController.ChangeLocalization(chatId, localization.Trim());
    }
    else if (text.StartsWith("/start", true, CultureInfo.InvariantCulture))
    {
        todoController.AddUser(chatId);
    }
    else if (text.StartsWith(localizationService.GetFormattedMessage("AddTodoKeyword").ToLower(), true, CultureInfo.InvariantCulture))
    {
        string add = localizationService.GetFormattedMessage("AddTodoKeyword");
        text = text.Remove(0, add.Length);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        todoController.AddTodo(chatId, text);
    }
    else if (text.StartsWith(localizationService.GetFormattedMessage("DeleteTodoKeyword").ToLower(), true, CultureInfo.InvariantCulture))
    {
        var deleteKeyword = localizationService.GetFormattedMessage("DeleteTodoKeyword");
        Todo[] todos = todoController.GetAllTodos(chatId).ToArray();
        InlineKeyboardButton[] buttons = new InlineKeyboardButton[todos.Count()];
        for (int i = 0; i < buttons.Length; i++)
        {
            InlineKeyboardButton button = new InlineKeyboardButton(todos[i].TodoId + ". " + todos[i].Title);
            button.CallbackData = todos[i].TodoId.ToString();
            buttons[i] = button;
        }
        InlineKeyboardMarkup inline = new InlineKeyboardMarkup(buttons);
        await botClient.SendTextMessageAsync(chatId, localizationService.GetFormattedMessage("SelectTodoToDelete"), ParseMode.Html, null, false, false, false, 0, false, inline);        
    }
    else if (text.StartsWith(localizationService.GetFormattedMessage("ShowTodosKeyword").ToLower(), true, CultureInfo.InvariantCulture))
    {
        List<Todo> todoList = todoController.GetAllTodos(chatId).ToList();
        string result = "";
        foreach (Todo todo in todoList)
        {
            result += todo.Title + Environment.NewLine;
        }
        if (string.IsNullOrEmpty(result))
        {
            result = localizationService.GetFormattedMessage("NoTodos");
        }
        Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: result,
                cancellationToken: cancellationToken);
    }
    else
    {
        await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: localizationService.GetFormattedMessage("UnknownCommand"),
        cancellationToken: cancellationToken);
        return;
    }
}

void UpdateLocalization(Localization localization)
{
    CultureInfo ci = null;
    switch (localization)
    {
        case Localization.UA:
            ci = new CultureInfo("uk-UA");
            break;
        case Localization.US:
            ci = new CultureInfo("en-US");
            break;
    }
    Thread.CurrentThread.CurrentCulture = ci;
    Thread.CurrentThread.CurrentUICulture = ci;
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

async Task<string> ParseUserAudio(string fileId)
{
    var filePath = Path.Combine(path, fileId + ".ogg");

    using (var file = File.OpenWrite(filePath))
    {
        await botClient.GetInfoAndDownloadFileAsync(fileId, file);
        Console.WriteLine($"Find Voice at {filePath}");
        file.Close();
    }

    var newFilePath = Path.Combine(path, fileId + ".wav");
    using (Stream source = File.OpenRead(filePath))
    {
        var inputFile = new InputFile(filePath);
        var outputFile = new OutputFile(newFilePath);

        var ffmpeg = new Engine(configuration["FFMpegExePath"]);
        ConversionOptions conversion = new ConversionOptions();
        conversion.AudioChanel = 1;
        conversion.CustomWidth = 2;
        conversion.AudioSampleRate = FFmpeg.NET.Enums.AudioSampleRate.Hz22050;
        var output = await ffmpeg.ConvertAsync(inputFile, outputFile, conversion, default).ConfigureAwait(false);
        source.Close();
    }
    Model model = null;
    if (CultureInfo.CurrentCulture == new CultureInfo("en-US"))
    {
        model = englishModel;
    }
    else
    {
        model = ukrainianModel;
    }
    VoskRecognizer rec = new VoskRecognizer(model, 22050.0f);
    rec.SetMaxAlternatives(0);
    rec.SetWords(true);
    using (Stream source = File.OpenRead(newFilePath))
    {
        byte[] buffer = new byte[4096];
        int bytesRead;
        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            rec.AcceptWaveform(buffer, bytesRead);
        }
        source.Close();
    }

    return JsonConvert.DeserializeObject<VoskResult>(rec.FinalResult()).Text;
}
