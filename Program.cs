using System.Net;
using System.Text.Json;

namespace Formula_1_Lap_Charts
{
    class Program
    {
        const string ergastAPI = "http://ergast.com/api/f1/";

        static HttpClient HttpClient = new HttpClient()
        {
            DefaultRequestHeaders = {
                { "User-Agent", "F1LapCharts/1.0 (https://github.com/twpol/formula-1-lap-charts)" }
            }
        };

        /// <summary>
        /// Command-line tool for generating a lap-by-lap spreadsheet of gaps for charts
        /// </summary>
        /// <param name="season">Specify the season (year) to analyse</param>
        /// <param name="race">Specify the race to analyse</param>
        static async Task Main(string season = "current", string race = "last")
        {
            var raceData = await GetErgastResponse($"{season}/{race}/results");
            var laps = int.Parse(raceData.RootElement.GetProperty("MRData").GetProperty("RaceTable").GetProperty("Races")[0].GetProperty("Results")[0].GetProperty("laps").GetString() ?? "0");
            var driverTotal = new Dictionary<string, float>();
            var driverLaps = new Dictionary<string, List<float>>();
            for (var lap = 1; lap <= laps; lap++)
            {
                var lapData = await GetErgastResponse($"{season}/{race}/laps/{lap}");
                var timings = lapData.RootElement.GetProperty("MRData").GetProperty("RaceTable").GetProperty("Races")[0].GetProperty("Laps")[0].GetProperty("Timings");
                foreach (var timing in timings.EnumerateArray())
                {
                    var driverId = timing.GetProperty("driverId").GetString() ?? "";
                    if (!driverTotal.ContainsKey(driverId)) driverTotal[driverId] = 0;
                    if (!driverLaps.ContainsKey(driverId)) driverLaps[driverId] = new();
                    driverTotal[driverId] += ParseTime(timing.GetProperty("time").GetString() ?? "0");
                    driverLaps[driverId].Add(driverTotal[driverId]);
                }
                var min = driverTotal.Where(kvp => driverLaps[kvp.Key].Count == lap).Min(kvp => kvp.Value);
                foreach (var driverId in driverTotal.Keys)
                {
                    if (driverLaps[driverId].Count == lap) driverLaps[driverId][^1] -= min;
                }
            }
            Console.WriteLine($"Driver,{string.Join(",", Enumerable.Range(1, laps))}");
            foreach (var driver in driverLaps)
            {
                Console.WriteLine($"{driver.Key},{string.Join(",", driver.Value.Select(v => v.ToString("F3")))}");
            }
        }

        static async Task<JsonDocument> GetErgastResponse(string requestUri)
        {
            // Rate limits: 4/second and 200/hour
            Thread.Sleep(250);
            return JsonDocument.Parse(await HttpClient.GetStreamAsync($"{ergastAPI}{requestUri}.json"));
        }

        static float ParseTime(string time)
        {
            var parts = time.Split(":");
            return uint.Parse(parts[0]) * 60 + float.Parse(parts[1]);
        }
    }
}
