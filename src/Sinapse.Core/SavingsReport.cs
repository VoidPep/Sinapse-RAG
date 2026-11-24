using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sinapse.Core;

public static class SavingsReport
{
    static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Write(string logPath, string htmlPath)
    {
        if (!File.Exists(logPath)) throw new FileNotFoundException("Nenhum log de economia ainda.", logPath);

        var recs = new JsonArray();
        foreach (var line in File.ReadLines(logPath).Where(l => l.Trim().Length > 0))
            recs.Add(JsonNode.Parse(line));
        // "</" would let a question close the <script> element
        var data = recs.ToJsonString(Json).Replace("</", "<\\/");

        using var s = typeof(SavingsReport).Assembly.GetManifestResourceStream("savings.html")!;
        using var reader = new StreamReader(s);
        File.WriteAllText(htmlPath, reader.ReadToEnd().Replace("/*DATA*/", data));
        return htmlPath;
    }
}
