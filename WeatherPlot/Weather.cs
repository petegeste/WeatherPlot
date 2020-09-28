using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WeatherPlot
{
    public class Weather : HttpClient
    {
        public readonly Regex acceptableSearchChars = new Regex(@"^(?<City>[\w]+)[, ]+(?<State>[\w]+)$");
        public readonly string ApiKey;

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            private set => _statusMessage = value;
        }

        private WeatherXmlModel weatherResultModel = null;

        public Weather(string apiKey)
        {
            ApiKey = apiKey;
            BaseAddress = new Uri(@"https://api.openweathermap.org/data/2.5/forecast/");
        }

        public string GetLocationName()
        {
            var loc = weatherResultModel?.Location?.FirstOrDefault();
            if (loc == null)
            {
                return "Nowhere, USA";
            }

            return $"{loc.City}, {loc.Country}";
        }

        public async Task<WeatherResult> GetWeatherAsync(string search)
        {
            
            bool res = CheckInputText(search, out string city, out string state);
            if (!res)
            {
                return new WeatherResult(false, "Query was not understood. Understands only TOWN, STATE");
            };

            // get weather
            var result = await GetAsync($"daily?q={city},{state.ToLower()},us&appid={ApiKey}&mode=xml");

            // check status code
            if (result.StatusCode != System.Net.HttpStatusCode.OK)
            {
                switch (result.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        return new WeatherResult(false, "Query failed; city/state probably not found.");
                        break;
                    case HttpStatusCode.Unauthorized:
                        return new WeatherResult(false, "Needs an API key!");
                        break;
                    default:
                        return new WeatherResult(false, "OpenWeatherMap request failed. :/");
                }
            }

            // parse document
            string doc = await result.Content.ReadAsStringAsync();
            XDocument xml = XDocument.Parse(doc);

            weatherResultModel = WeatherXmlModel.Parse(xml?.Root);
            if (weatherResultModel == null)
            {
                return new WeatherResult(false, "Could not parse XML result");
            }

            return new WeatherResult(true, "Successful!");
        }

        /// <summary>
        /// Checks the input against a Regex pattern to ensure no illegal chars are entered
        /// </summary>
        /// <returns>Value indicating that inputs are sanitary</returns>
        public bool CheckInputText(string searchText, out string city, out string state)
        {
            if (searchText == null)
            {
                city = null;
                state = null;
                return false;
            }
            bool res = acceptableSearchChars.IsMatch(searchText);
            if (res)
            {
                var groups = acceptableSearchChars.Match(searchText).Groups;
                city = groups["City"].Value;
                state = groups["State"].Value;
            }
            else
            {
                city = null;
                state = null;
            }
            return res;
        }

        /// <summary>
        /// Gets a series of tuples representing day and max temperature
        /// </summary>
        /// <returns>A series of temperature data points</returns>
        public IEnumerable<(string day, double? max_temp, double? min_temp)> GetTemperatureSeries()
        {
            if (weatherResultModel == null)
            {
                yield break;
            }
            foreach (var day in weatherResultModel.ForecastDays)
            {
                var temp = day?.Temperature?.FirstOrDefault();
                yield return (day.Day, temp.KelvinToFarenheit(temp.Max), temp.KelvinToFarenheit(temp.Min));
            }
        }

    }

    /// <summary>
    /// Provides status information about a query
    /// </summary>
    public struct WeatherResult
    {
        public bool Successful;
        public string Message;

        public WeatherResult(bool success, string message)
        {
            Successful = success;
            Message = message;
        }
    }

}
