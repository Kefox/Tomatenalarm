using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using dotenv.net;

DotEnv.Load();

using HttpClient client = new HttpClient();

string? clientId = Environment.GetEnvironmentVariable("CLIENTID");
string? clientSecret = Environment.GetEnvironmentVariable("CLIENTSECRET");

while (true)
{
    try
    {
        var parameters = new Dictionary<string, string>
        {
            {"grant_type", "client_credentials"},
            {"client_id", clientId!},
            {"client_secret", clientSecret!}
        };

        string urlToken = "https://api.authentication.husqvarnagroup.dev/v1/oauth2/token";
        var content = new FormUrlEncodedContent(parameters);

        var responseToken = await client.PostAsync(urlToken, content);
        responseToken.EnsureSuccessStatusCode();
        string jsonResponseToken = await responseToken.Content.ReadAsStringAsync();

        Console.WriteLine("\nNeuer Durchlauf:");
        Console.WriteLine("Token Status: " + responseToken.StatusCode);

        var parsedJsonToken = JsonNode.Parse(jsonResponseToken);
        string? accessToken = parsedJsonToken?["access_token"]?.ToString();
        
        string urlLocation = "https://api.smart.gardena.dev/v1/locations";
        using var requestLocation = new HttpRequestMessage(HttpMethod.Get, urlLocation);
        requestLocation.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestLocation.Headers.Add("X-Api-Key", clientId);

        var responseLocation = await client.SendAsync(requestLocation);
        responseLocation.EnsureSuccessStatusCode();
        string jsonResponseLocation = await responseLocation.Content.ReadAsStringAsync();

        Console.WriteLine("Location Status: " + responseLocation.StatusCode);

        var parsedJsonLocation = JsonNode.Parse(jsonResponseLocation);
        string? locationId = parsedJsonLocation?["data"]?[0]?["id"]?.ToString();

        string urlLocations = "https://api.smart.gardena.dev/v1/locations/" + locationId;
        using var requestLocations = new HttpRequestMessage(HttpMethod.Get, urlLocations);
        requestLocations.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        requestLocations.Headers.Add("X-Api-Key", clientId);

        var responseLocations = await client.SendAsync(requestLocations);
        responseLocations.EnsureSuccessStatusCode();
        string jsonResponseLocations = await responseLocations.Content.ReadAsStringAsync();

        Console.WriteLine("Location Details Status: " + responseLocations.StatusCode);

        var parsedJsonLocations = JsonNode.Parse(jsonResponseLocations);
        var LocationsArray = parsedJsonLocations?["included"]?.AsArray();

        if (LocationsArray != null)
        {
            foreach (var item in LocationsArray)
            {
                string? type = item?["type"]?.ToString();
                if (type == "SENSOR")
                {
                    string? soilTemperature = item?["attributes"]?["soilTemperature"]?["value"]?.ToString();
                    string? soilHumidityStr = item?["attributes"]?["soilHumidity"]?["value"]?.ToString();
                    
                    Console.WriteLine($"Soil Temperature: {soilTemperature}, Soil Humidity: {soilHumidityStr}");

                    // Hier prüfen wir die Werte DIREKT in der Schleife, solange die Variablen existieren
                    if (int.TryParse(soilHumidityStr, out int soilHumidityValue))
                    {
                        if (soilHumidityValue < 40)
                        {
                            Console.WriteLine($"ALARM: Feuchtigkeit ist bei {soilHumidityValue}%.");
                            string? urlWebhook = Environment.GetEnvironmentVariable("VOICEMONKEYURL");
                            using var requestWebhook = new HttpRequestMessage(HttpMethod.Get, urlWebhook);
                            var responseWebhook = await client.SendAsync(requestWebhook);
                            Console.WriteLine($"Webhook Status: {responseWebhook.StatusCode}");
                        }
                        else
                        {
                            Console.WriteLine("Kein Alarm: Feuchtigkeit ist über 40%.");
                        }
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ein Fehler ist aufgetreten: {ex.Message}");
    }

    Console.WriteLine($"Warte 30 Minuten bis zum nächsten Check.\nAktuelle Zeit: {DateTime.Now}");
    await Task.Delay(TimeSpan.FromMinutes(30));
}