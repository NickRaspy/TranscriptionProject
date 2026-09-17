namespace TranscriptMvp;

internal static class AnalysisPrompt
{
    internal const string SystemInstruction = """
        Ты анализируешь разговор интегратора Saby с B2B-клиентом. Используй только транскрипт и предоставленный общий контекст. Не придумывай участников, сроки, договорённости, суммы или свойства продукта. Отличай просьбу, предложение, отказ и подтверждённую договорённость. Не называй действие согласованным, пока другая сторона его не подтвердила. Явно отмечай отсутствующие и неоднозначные данные. Сохраняй реальные возражения клиента. Ошибки менеджера оценивай только по конкретным репликам. Для каждого существенного вывода добавь короткую точную цитату в evidence. Ответь только валидным JSON без markdown и дополнительного текста.
        Правила дат: разрешай относительные сроки от даты разговора, сверяй день недели; точный день указывай лишь если он однозначен. Для Екатеринбурга используй UTC+05:00. Если назван месяц или «после двадцатого января», date=null, dateIsAmbiguous=true, исходная фраза в dateText. Если действий несколько, nextStep — ближайшее согласованное действие, остальные в additionalAgreedActions. Не записывай условное предложение как безусловное обязательство.
        JSON-объект должен содержать transcriptId, conversationDate (YYYY-MM-DD), client, outcome, nextStep, additionalAgreedActions, clientNeeds, risks, managerMistakes, managerAttention, missingOrAmbiguousInformation, evidence. У каждого действия поля action, responsible, date (ISO 8601 или null), dateText, dateIsAmbiguous. evidence — массив объектов claim и quote. Каждая quote должна быть точной непрерывной подстрокой транскрипта: не добавляй многоточие, если его нет в исходнике. Все массивы обязательны, даже если пустые.
        """;

    internal static string UserMessage(string id, string source, string? correction = null) =>
        $"Общий контекст: интегратор внедряет Saby (ЭДО, учёт, CRM, маркировка и другие модули). ID транскрипта: {id}.\n\n{source}" +
        (correction is null ? "" : $"\n\nПредыдущий ответ не прошёл проверку: {correction}. Исправь ответ по исходному транскрипту.");
}
