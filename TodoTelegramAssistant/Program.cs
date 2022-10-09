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

var configuration = new ConfigurationBuilder()
.AddUserSecrets<Program>().Build();

var botClient = new TelegramBotClient(configuration["TelegramBotApiKey"]);

using var cts = new CancellationTokenSource();

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
};

Model model = new Model(configuration["UkrainianSTTModelPath"]);

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);
var me = await botClient.GetMeAsync();

Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();

// Send cancellation request to stop bot
cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    // Only process Message updates: https://core.telegram.org/bots/api#message
    if (update.Message is not { } message)
        return;
    // Only process audio messages
    if (message.Voice is not { })
        return;

    var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
    path = Path.GetDirectoryName(path);
    var filePath = Path.Combine(path, message.Voice.FileId + ".ogg");

    using (var file = File.OpenWrite(filePath))
    {
        await botClient.GetInfoAndDownloadFileAsync(message.Voice.FileId, file);
        Console.WriteLine($"Find Voice at {filePath}");
        file.Close();
    }

    var newFilePath = Path.Combine(path, message.Voice.FileId + ".wav");
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

    string text = rec.FinalResult();
    Console.WriteLine(text);

    var convertedSpeech = JsonConvert.DeserializeObject<VoskResult>(text);


    var chatId = message.Chat.Id;

    Console.WriteLine($"Received a '{convertedSpeech.Text}' message in chat {chatId}.");

    var s = new SpeechSynthesizer();

    filePath = Path.Combine(path, message.Voice.FileId + "s.wav");

    var a = s.GetInstalledVoices();
    s.SelectVoice(configuration["UkrainianVoiceForSynthesisName"]);
    s.SetOutputToWaveFile(filePath,
        new SpeechAudioFormatInfo(22050, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
    s.Speak(convertedSpeech.Text);
    s.SetOutputToNull();

    // Echo received message text
    Message sentMessage = await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: "You said:\n" + convertedSpeech.Text,
        cancellationToken: cancellationToken);

    using (var stream = File.OpenRead(filePath))
    {
        message = await botClient.SendVoiceAsync(
            chatId: chatId,
            voice: stream,
            cancellationToken: cancellationToken);
    }
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
