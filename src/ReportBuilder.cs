using System.Net;
using System.Text;

namespace TranscriptMvp;

public static class ReportBuilder
{
    public static string Build(ResultBatch batch)
    {
        var html = new StringBuilder("""
            <!doctype html><html lang="ru"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Анализ B2B-разговоров</title><style>
            :root{font-family:system-ui,Segoe UI,sans-serif;color:#16253a;background:#f2f5f8}*{box-sizing:border-box}body{margin:0}header{background:#102b45;color:white;padding:32px max(24px,calc((100vw - 1100px)/2))}h1{margin:0 0 8px;font-size:2rem}header p{margin:0;color:#c8d8e7}.wrap{max-width:1100px;margin:28px auto;padding:0 20px}.notice{background:#e9f3fb;border-left:4px solid #1e76af;padding:14px 18px;margin-bottom:22px}.card{background:white;border:1px solid #dce3e9;border-radius:12px;padding:24px;margin:0 0 22px;box-shadow:0 4px 16px #1b3d5a0d}h2{margin:0 0 5px}h3{font-size:1rem;margin:22px 0 8px;color:#22547b}.muted{color:#637386}.step{padding:14px;background:#edf7f4;border-radius:8px;border-left:4px solid #168f6b}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:0 28px}ul{padding-left:20px;margin:7px 0}li{margin:7px 0;line-height:1.45}blockquote{margin:8px 0 15px;padding:7px 12px;border-left:3px solid #b9cbd9;color:#526577;background:#f7f9fb}p{line-height:1.5}@media(max-width:700px){.grid{grid-template-columns:1fr}header{padding:25px 20px}.card{padding:18px}}
            </style></head><body><header><h1>Анализ B2B-разговоров</h1><p>Итоги, действия, риски и подтверждающие реплики</p></header><main class="wrap">
            """);
        html.Append("<div class=\"notice\"><strong>Режим: ").Append(E(batch.Mode)).Append("</strong><br>Сформировано: ").Append(E(batch.GeneratedAt)).Append(". Неоднозначные сроки оставлены без выдуманной точной даты.</div>");
        foreach (var item in batch.Conversations)
        {
            html.Append("<section class=\"card\" id=\"transcript-").Append(E(item.TranscriptId)).Append("\"><h2>").Append(E(item.TranscriptId)).Append(". ").Append(E(item.Client)).Append("</h2><div class=\"muted\">Разговор: ").Append(E(item.ConversationDate)).Append("</div>");
            html.Append("<h3>Итог</h3><p>").Append(E(item.Outcome)).Append("</p><h3>Ближайший согласованный шаг</h3><div class=\"step\">");
            Action(html, item.NextStep);
            html.Append("</div>");
            if (item.AdditionalAgreedActions.Count > 0)
            {
                html.Append("<h3>Другие договорённости</h3><ul>");
                foreach (var action in item.AdditionalAgreedActions) { html.Append("<li>"); Action(html, action); html.Append("</li>"); }
                html.Append("</ul>");
            }
            html.Append("<div class=\"grid\"><div>");
            List(html, "Потребности", item.ClientNeeds);
            List(html, "Риски", item.Risks);
            html.Append("</div><div>");
            List(html, "Возможные ошибки менеджера", item.ManagerMistakes);
            List(html, "Внимание руководителя", item.ManagerAttention);
            List(html, "Что неизвестно или неоднозначно", item.MissingOrAmbiguousInformation);
            html.Append("</div></div><h3>Подтверждения</h3>");
            foreach (var ev in item.Evidence) html.Append("<p><strong>").Append(E(ev.Claim)).Append("</strong></p><blockquote>").Append(E(ev.Quote)).Append("</blockquote>");
            html.Append("</section>");
        }
        return html.Append("</main></body></html>").ToString();
    }

    private static void Action(StringBuilder html, AgreedAction action)
    {
        html.Append(E(action.Action)).Append(" <strong>Ответственный:</strong> ").Append(E(action.Responsible))
            .Append(". <strong>Срок:</strong> ").Append(E(action.DateText));
        if (action.Date is not null) html.Append(" (").Append(E(action.Date)).Append(")");
        else html.Append(" (точная дата не согласована)");
    }

    private static void List(StringBuilder html, string title, List<string> items)
    {
        html.Append("<h3>").Append(E(title)).Append("</h3><ul>");
        foreach (var item in items) html.Append("<li>").Append(E(item)).Append("</li>");
        if (items.Count == 0) html.Append("<li>Нет отмеченных пунктов</li>");
        html.Append("</ul>");
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
}
