using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotBuildingCompany;

internal static class Program
{
    private static readonly TelegramBotClient BotClient = new(Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN"));
    
    private static readonly Dictionary<long, string> UserLanguages = new();

    private static async Task Main()
    {
        try
        {
            using var cts = new CancellationTokenSource();
            
            var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
            {
                AllowedUpdates = []
            };
            
            BotClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleError,
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

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        long chatId;
        string? messageText;

        // Handle callback queries
        if (update.CallbackQuery != null)
        {
            var callbackQuery = update.CallbackQuery;

            if (callbackQuery.Data != null && callbackQuery.Data.StartsWith("lang_"))
            {
                await SetupLanguageAsync(botClient, callbackQuery, cancellationToken);
                return;
            }

            if (callbackQuery.Message != null)
            {
                chatId = callbackQuery.Message.Chat.Id;
                messageText = callbackQuery.Data;

                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
            }
            else
            {
                return;
            }
        }
        // Handle regular messages
        else if (update.Message != null)
        {
            chatId = update.Message.Chat.Id;
            messageText = update.Message.Text;
        }
        else
        {
            return;
        }

        if (string.IsNullOrEmpty(messageText))
            return;

        if (!UserLanguages.ContainsKey(chatId))
        {
            await SendLanguageMenu(chatId, cancellationToken);
            return;
        }

        var language = UserLanguages.GetValueOrDefault(chatId, "ua");

        Console.WriteLine($"Processing command: {messageText}");

        // Check for command buttons by exact text match or command prefix
        if (messageText.StartsWith("/services") ||
            messageText.StartsWith("/послуги") ||
            messageText == "cmd_services" ||
            messageText == "📋 Services" ||
            messageText == "📋 Послуги")
        {
            await SendServicesListAsync(chatId, language, cancellationToken);
        }
        else if (messageText.StartsWith("/contacts") ||
                 messageText.StartsWith("/контакти") ||
                 messageText == "cmd_contacts" ||
                 messageText == "📞 Contacts" ||
                 messageText == "📞 Контакти")
        {
            await SendContactInfoAsync(chatId, language, cancellationToken);
        }
        else if (messageText.StartsWith("/appointment") ||
                 messageText.StartsWith("/запис") ||
                 messageText == "cmd_appointment" ||
                 messageText == "📅 Book an appointment" ||
                 messageText == "📅 Замовити візит")
        {
            await StartAppointmentProcessAsync(chatId, language, cancellationToken);
        }
        else if (messageText.StartsWith("/language") ||
                 messageText.StartsWith("/мова"))
        {
            await SendLanguageMenu(chatId, cancellationToken);
        }
        else if (AppointmentManager.IsWaitingForAppointmentInfo(chatId))
        {
            // Only process appointment info from regular messages, not callbacks
            if (update.Message != null)
            {
                await ProcessAppointmentInfoAsync(chatId, messageText, language, cancellationToken);
            }
        }
        else
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: GetTranslation(language, "unknown_command"),
                cancellationToken: cancellationToken);
        }
    }

    private static async Task SendLanguageMenu(long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("Українська", "lang_ua"),
                InlineKeyboardButton.WithCallbackData("English", "lang_en")
            ]
        ]);
    
        await BotClient.SendMessage(
            chatId: chatId,
            text: "Привіт! Будь ласка, оберіть бажану мову спілкування\n/\nWelcome! Please select your preferred language",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
    
    private static async Task SendMainMenuAsync(long chatId, string language, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData(GetTranslation(language, "services"), "cmd_services"),
                InlineKeyboardButton.WithCallbackData(GetTranslation(language, "contacts"), "cmd_contacts")
            ],
            [
                InlineKeyboardButton.WithCallbackData(GetTranslation(language, "appointment"), "cmd_appointment")
            ]
        ]);
    
        var botInfo = GetTranslation(language, "bot_info");

        await BotClient.SendMessage(
            chatId: chatId,
            text: botInfo,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
    
    private static async Task SendServicesListAsync(long chatId, string language, CancellationToken cancellationToken)
    {
        await BotClient.SendMessage(
            chatId: chatId,
            text: GetTranslation(language, "services_list"),
            cancellationToken: cancellationToken);
    }
    
    private static async Task SendContactInfoAsync(long chatId, string language, CancellationToken cancellationToken)
    {
        await BotClient.SendMessage(
            chatId: chatId,
            text: GetTranslation(language, "contact_information"),
            cancellationToken: cancellationToken);
    }
    
    private static async Task StartAppointmentProcessAsync(long chatId, string language, CancellationToken cancellationToken)
    {
        AppointmentManager.StartAppointment(chatId);
        
        await BotClient.SendMessage(
            chatId: chatId,
            text: GetTranslation(language, "appointment_information"),
            cancellationToken: cancellationToken);
    }
    
    private static async Task ProcessAppointmentInfoAsync(long chatId, string messageText, string language, CancellationToken cancellationToken)
    {
        AppointmentManager.SaveAppointment(chatId, messageText);
        
        await BotClient.SendMessage(
            chatId: chatId,
            text: GetTranslation(language, "appointment_received") + messageText,
            cancellationToken: cancellationToken);
    }
    
    private static Task HandleError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
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
                ["unknown_command"] = "I don't understand that command. Please use one of the available options:\n/contacts\n/services\n/appointment\n/language",
                ["select_date"] = "Please select a date for your appointment:",
                ["appointment_received"] = "Thank you! Your appointment request has been received.\n\nWe will contact you shortly to confirm the details.\n\nYour provided information:\n",
                ["appointment_information"] = "Please provide the following information:\n\n1. Full address\n2. Phone number\n3. Preferred appointment date (day/month/year)\n4. Preferred appointment time\n\nExample: Kyiv, Shevchenka St., 21, Apt. 33\n+38 (099) 123‑45‑67\n15/05/2025\n14:00",
                ["contact_information"] = "Contact Information:\n\n📞 Phone:\n+38 (097) 050-60-70\n+38 (093) 118-33-71\n\nPavlo Andriyovych Symoniuk",
                ["services_list"] = "Our Services:\n\nInstallation/Dismantling\nDrywall Constructions\nPlastering & Painting Works\n️Wallpapering of Any Type\nTiling & Parquet Flooring\nDoor Installation\nStretch Ceilings\nFacade Works\nFence Installation\nPlumbing and Electrical Services",
                ["bot_info"] = "Welcome! I am a service bot for construction and repair services. You can use the menu below to learn about our services, contact us, or make an appointment:\n/contacts\n/services\n/appointment\n/language"
            },
            ["ua"] = new()
            {
                ["services"] = "📋 Послуги",
                ["contacts"] = "📞 Контакти",
                ["appointment"] = "📅 Замовити візит",
                ["unknown_command"] = "Я не розумію даної команди. Будь ласка, використайте одну з наданих команд:\n/контакти\n/послуги\n/запис\n/мова",
                ["select_date"] = "Будь ласка, оберіть дату візиту:",
                ["appointment_received"] = "Дякуємо! Ваш запит на візит було отримано.\n\nМи зв'яжемося з Вами для підтвердження деталей зустрічі.\n\nІнформація надана Вами:\n",
                ["appointment_information"] = "Будь ласка, вкажіть наступну інформацію:\n\n1. Повна адреса\n2. Номер телефону\n3. Бажану дату зустрічі (день/місяць/рік)\n4. Бажаний час зустрічі\n\nПриклад: м. Київ, вул. Шевченка, буд. 21, кв. 33\n+38 (099) 123-45-67\n15/05/2025\n14:00",
                ["contact_information"] = "Контактна інформація:\n\n📞 Телефон:\n+38 (097) 050-60-70\n+38 (093) 118-33-71\n\nСимонюк Павло Андрійович",
                ["services_list"] = "Наші послуги:\n\nМонтаж/Демонтаж\nГіпсокартонні конструкції\nШтукатурні та малярні роботи\nПоклейка шпалер будь-якого типу\nУкладання плитки, паркету\nВстановлення дверей\nНатяжні стелі\nФасадні роботи\nВстановлення парканів\nПослуги Сантехніка та Електрика",
                ["bot_info"] = "Вітаю! Я бот для будівельних та ремонтних послуг. Ви можете використовувати меню нижче, щоб дізнатися про наші послуги, зв'язатися з нами або замовити візит:\n/контакти\n/послуги\n/запис\n/мова"
            }
        };
    
        if (!translations.ContainsKey(language))
            language = "ua";
        
        return translations[language].GetValueOrDefault(key, key);
    }

    private static async Task SetupLanguageAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null)
            return;

        var callbackChatId = callbackQuery.Message.Chat.Id;

        if (callbackQuery.Data is null)
            return;

        if (callbackQuery.Data.StartsWith("lang_"))
        {
            var language = callbackQuery.Data[5..];
            UserLanguages[callbackChatId] = language;

            Console.WriteLine($"SetupLanguageAsync Success. Language: {language}");

            await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
            await SendMainMenuAsync(callbackChatId, language, cancellationToken);
        }
    }
}

public static class AppointmentManager
{
    private static readonly Dictionary<long, bool> PendingAppointments = new();
    private static readonly Dictionary<long, string> Appointments = new();
    
    public static void StartAppointment(long chatId)
    {
        PendingAppointments[chatId] = true;
    }
    
    public static bool IsWaitingForAppointmentInfo(long chatId)
    {
        return PendingAppointments.ContainsKey(chatId) && PendingAppointments[chatId];
    }
    
    public static void SaveAppointment(long chatId, string appointmentInfo)
    {
        Appointments[chatId] = appointmentInfo;
        PendingAppointments[chatId] = false;
        
        // TODO implement saving into DB
        Console.WriteLine($"New appointment from {chatId}: {appointmentInfo}");
    }
}


/*
    CREATE TABLE Appointments (
       ChatId BIGINT PRIMARY KEY,
       AppointmentInfo TEXT,
       IsPending BOOLEAN DEFAULT FALSE,
       CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
   ); 
 */