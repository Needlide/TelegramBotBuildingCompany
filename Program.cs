using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotBuildingCompany;

internal static class Program
{
    private static TelegramBotClient _botClient = new(Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN"));
    
    private static readonly Dictionary<long, string> UserLanguages = new();

    private static async Task Main()
    {
        try
        {
            _botClient = new TelegramBotClient("YOUR_BOT_TOKEN");
            
            using var cts = new CancellationTokenSource();
            
            var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
            {
                AllowedUpdates = []
            };
            
            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );
            
            Console.WriteLine("Bot started. Press any key to exit");
            Console.ReadKey();
            
            await cts.CancelAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Problem with bot occured: {e.Message}");
            throw;
        }
    }
    
    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.CallbackQuery != null)
        {
            var callbackQuery = update.CallbackQuery;
            
            if (callbackQuery.Message is null)
                return;
            
            var callbackChatId = callbackQuery.Message.Chat.Id;
            
            if(callbackQuery.Data is null)
                return;
            
            if (callbackQuery.Data.StartsWith("lang_"))
            {
                var language = callbackQuery.Data[5..];
                UserLanguages[callbackChatId] = language;
            
                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                await SendMainMenu(callbackChatId, language, cancellationToken);
                return;
            }
        }
        
        if (update.Message is not { } message)
            return;
        
        if (message.Text is not { } messageText)
            return;
        
        var chatId = message.Chat.Id;
        
        Console.WriteLine($"Received a message from {message.Chat.FirstName} {message.Chat.LastName} ({message.Chat.Username}): {messageText}");
        
        if (messageText.StartsWith("/start"))
        {
            await SendWelcomeMessage(chatId, cancellationToken);
        }
        else if (messageText.StartsWith("/services"))
        {
            await SendServicesList(chatId, cancellationToken);
        }
        else if (messageText.StartsWith("/contacts"))
        {
            await SendContactInfo(chatId, cancellationToken);
        }
        else if (messageText.StartsWith("/appointment"))
        {
            await StartAppointmentProcess(chatId, cancellationToken);
        }
        else if (AppointmentManager.IsWaitingForAppointmentInfo(chatId))
        {
            await ProcessAppointmentInfo(chatId, messageText, cancellationToken);
        }
        else
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "I don't understand that command. Please use one of the available options.",
                cancellationToken: cancellationToken);
        }
    }

    private static async Task SendWelcomeMessage(long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("Українська 🇪🇸", "lang_ua"),
                InlineKeyboardButton.WithCallbackData("English 🇬🇧", "lang_en")
            ]
        ]);
    
        await _botClient.SendMessage(
            chatId: chatId,
            text: "Привіт! Будь ласка, оберіть бажану мову спілкування / Welcome! Please select your preferred language",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
    
    private static async Task SendMainMenu(long chatId, string language, CancellationToken cancellationToken)
    {
        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(GetTranslation(language, "services")), new KeyboardButton(GetTranslation(language, "contacts")) },
            new[] { new KeyboardButton(GetTranslation(language, "appointment")) }
        })
        {
            ResizeKeyboard = true
        };
    
        await _botClient.SendMessage(
            chatId: chatId,
            text: GetTranslation(language, "welcome_message"),
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
    
    private static async Task SendServicesList(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: "Our Services:\n\n" +
                  "🏗️ New Construction\n" +
                  "🔨 Renovations\n" +
                  "🏠 Home Extensions\n" +
                  "🛠️ Repairs and Maintenance\n" +
                  "🏢 Commercial Construction\n" +
                  "⚡ Electrical Work\n" +
                  "🚿 Plumbing Services",
            cancellationToken: cancellationToken);
    }
    
    private static async Task SendContactInfo(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: "Contact Information:\n\n" +
                  "📞 Phone: +38 (097) 050 60 70\n",
            cancellationToken: cancellationToken);
    }
    
    private static async Task StartAppointmentProcess(long chatId, CancellationToken cancellationToken)
    {
        AppointmentManager.StartAppointment(chatId);
        
        await _botClient.SendMessage(
            chatId: chatId,
            text: "Please provide the following information for your appointment:\n\n" +
                  "1. Your full address\n" +
                  "2. Phone number\n" +
                  "3. Preferred date (DD/MM/YYYY)\n" +
                  "4. Preferred time\n\n" +
                  "Example: 123 Main St, Anytown\n+1-234-567-8900\n15/05/2025\n10:00 AM",
            cancellationToken: cancellationToken);
    }
    
    private static async Task ProcessAppointmentInfo(long chatId, string messageText, CancellationToken cancellationToken)
    {
        // Store appointment information
        AppointmentManager.SaveAppointment(chatId, messageText);
        
        // Send confirmation
        await _botClient.SendMessage(
            chatId: chatId,
            text: "Thank you! Your appointment request has been received.\n\n" +
                  "We will contact you shortly to confirm the details.\n\n" +
                  "Your provided information:\n" + messageText,
            cancellationToken: cancellationToken);
    }
    
    private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };
        
        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
    
    private static string GetTranslation(string language, string key)
    {
        var translations = new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["services"] = "📋 Services",
                ["contacts"] = "📞 Contacts",
                ["appointment"] = "📅 Make an appointment",
                ["welcome_message"] = "Welcome! Please, choose an option from the menu below:"
            },
            ["ua"] = new()
            {
                ["services"] = "📋 Послуги",
                ["contacts"] = "📞 Контакти",
                ["appointment"] = "📅 Замовити візит",
                ["welcome_message"] = "Вітаємо! Будь ласка, оберіть послугу з меню нижче:"
            }
        };
    
        if (!translations.ContainsKey(language))
            language = "en";
        
        return translations[language].ContainsKey(key) ? translations[language][key] : key;
    }
}

public static class AppointmentManager
{
    private static Dictionary<long, bool> _pendingAppointments = new Dictionary<long, bool>();
    private static Dictionary<long, string> _appointments = new Dictionary<long, string>();
    
    public static void StartAppointment(long chatId)
    {
        _pendingAppointments[chatId] = true;
    }
    
    public static bool IsWaitingForAppointmentInfo(long chatId)
    {
        return _pendingAppointments.ContainsKey(chatId) && _pendingAppointments[chatId];
    }
    
    public static void SaveAppointment(long chatId, string appointmentInfo)
    {
        _appointments[chatId] = appointmentInfo;
        _pendingAppointments[chatId] = false;
        
        // In a real application, you would save this to a database
        Console.WriteLine($"New appointment from {chatId}: {appointmentInfo}");
    }
}