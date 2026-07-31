using Newtonsoft.Json.Linq;

namespace Care.Wasm.Client.Infrastructure.ApiClient;

public static class ApiErrorHelper
{
    private const string GenericMessage = "Something went wrong. Please try again.";

    public static string GetFriendlyMessage(Exception ex) =>
        ex is ApiException apiException ? GetFriendlyMessage(apiException) : GenericMessage;

    public static string GetFriendlyMessage(ApiException ex)
    {
        if (string.IsNullOrEmpty(ex.Response))
        {
            return GenericMessage;
        }

        try
        {
            var json = JObject.Parse(ex.Response);

            if (json["errors"] is JObject errors)
            {
                var messages = errors.Properties()
                    .SelectMany(p => p.Value is JArray array
                        ? array.Select(v => v.ToString())
                        : new[] { p.Value?.ToString() ?? string.Empty })
                    .Where(m => !string.IsNullOrWhiteSpace(m));

                string joined = string.Join(" ", messages);
                if (!string.IsNullOrWhiteSpace(joined))
                {
                    return joined;
                }
            }

            if (json["message"]?.ToString() is { Length: > 0 } message)
            {
                return message;
            }

            if (json["title"]?.ToString() is { Length: > 0 } title)
            {
                return title;
            }
        }
        catch
        {
            // Not parseable JSON — fall through to the generic message.
        }

        return GenericMessage;
    }
}
