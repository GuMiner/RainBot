using BotCommon.Processors;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotCommon;
using BotCommon.Activity;
using GeoWeather;
using System.Device.Location;
using BotCommon.Storage;
using GeoWeather.Stations;
using GeoWeather.Location;
using GeoWeather.Layers;

namespace RainBot
{
    public class WeatherActivityProcessor : ConversationActivityProcessor<WeatherSettings>
    {
        protected override string SubContainer => "weather";
        private readonly LocationQuerier locationFinder;

        public WeatherActivityProcessor(string bingMapsApiKey)
        {
            this.locationFinder = new LocationQuerier(bingMapsApiKey);
        }

        protected override Task<ConversationResponse<WeatherSettings>> StartConversationAsync(IStore blobStore, ActivityRequest request)
        {
            string welcome = $"RainBot (Weather).\r\n\r\n{GetHelpText()}";
            return Task.FromResult(new ConversationResponse<WeatherSettings>(new WeatherSettings(), new ActivityResponse(welcome)));
        }

        protected override async Task<ConversationResponse<WeatherSettings>> ContinueConversationAsync(IStore blobStore, ActivityRequest request, WeatherSettings priorConversation)
        {
            if (request.SanitizedText.Contains("quit"))
            {
                priorConversation = null; // Reset the game state
            }

            string input = request.SanitizedText.ToLower();

            List<string> responseText = new List<string>();
            List<AttachmentResponse> attachments = new List<AttachmentResponse>();
            if (input.Contains("weather"))
            {
                input = input.Replace("weather", string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(input))
                {
                    // The rest of the input is the location, so update our geo-coordinate
                    try
                    {
                        GeoCoordinate coordinate = await this.locationFinder.GetLocationAsync(input).ConfigureAwait(false);
                        Station closestStation = StationLocator.FindClosestStation(coordinate);
                        string distanceToStation = $", {(int)(closestStation.Location.GetDistanceTo(coordinate) / 10.0) / 100.0} km away.";
                        if (closestStation.Callsign != priorConversation.Station)
                        {
                            responseText.Add($"Using {closestStation.Callsign} near {closestStation.City}{distanceToStation}");
                            priorConversation.Station = closestStation.Callsign;
                        }
                        else
                        {
                            responseText.Add($"{closestStation.Callsign}, the currently-selected station, is still the closest NOAA station{distanceToStation}");
                        }
                    }
                    catch (Exception ex)
                    {
                        responseText.Add(ex.Message + ": " + ex.StackTrace);
                    }
                }

                // Actually get the weather.
                AttachmentResponse radarImage = 
                    await LayerRetriever.GetRadarImageAsync(priorConversation, blobStore, "weather-images", TimeSpan.FromDays(3*30)).ConfigureAwait(false);
                attachments.Add(radarImage);
            }
            else if (input.Contains("layers") || input.Contains("layer"))
            {
                input = input.Replace("layers", string.Empty).Replace("layer", string.Empty).Trim();
                if (input.Contains("add"))
                {
                    input = input.Replace("add", string.Empty).Trim();
                    RadarLayerType layer = RadarLayerTypeNames.GetLayer(input);
                    if (layer == RadarLayerType.Unknown)
                    {
                        responseText.Add($"Unknown layer '{input}'");
                        OutputValidLayers(responseText);
                    }
                    else
                    {
                        string response = priorConversation.LayerStack.AddLayer(layer);
                        if (!string.IsNullOrEmpty(response))
                        {
                            responseText.Add(response);
                        }

                        OutputCurrentLayers(priorConversation, responseText);
                    }
                }
                else if (input.Contains("remove"))
                {
                    input = input.Replace("remove", string.Empty).Trim();
                    RadarLayerType layer = RadarLayerTypeNames.GetLayer(input);
                    if (layer == RadarLayerType.Unknown)
                    {
                        responseText.Add($"Unknown layer '{input}'");
                        OutputValidLayers(responseText);
                    }
                    else
                    {
                        string response = priorConversation.LayerStack.RemoveLayer(layer);
                        if (!string.IsNullOrEmpty(response))
                        {
                            responseText.Add(response);
                        }
                        OutputCurrentLayers(priorConversation, responseText);
                    }
                }
                else if (input.Contains("promote"))
                {
                    input = input.Replace("promote", string.Empty).Trim();
                    RadarLayerType layer = RadarLayerTypeNames.GetLayer(input);
                    if (layer == RadarLayerType.Unknown)
                    {
                        responseText.Add($"Unknown layer '{input}'");
                        OutputValidLayers(responseText);
                    }
                    else
                    {
                        string response = priorConversation.LayerStack.PromoteLayer(layer);
                        if (!string.IsNullOrEmpty(response))
                        {
                            responseText.Add(response);
                        }
                        OutputCurrentLayers(priorConversation, responseText);
                    }
                }
                else if (input.Contains("demote"))
                {
                    input = input.Replace("demote", string.Empty).Trim();
                    RadarLayerType layer = RadarLayerTypeNames.GetLayer(input);
                    if (layer == RadarLayerType.Unknown)
                    {
                        responseText.Add($"Unknown layer '{input}'");
                        OutputValidLayers(responseText);
                    }
                    else
                    {
                        string response = priorConversation.LayerStack.DemoteLayer(layer);
                        if (!string.IsNullOrEmpty(response))
                        {
                            responseText.Add(response);
                        }
                        OutputCurrentLayers(priorConversation, responseText);
                    }
                }
                else
                {
                    responseText.Add("Valid layer commands are 'add', 'remove', 'promote', or 'demote'");
                    OutputValidLayers(responseText);
                    OutputCurrentLayers(priorConversation, responseText);
                }
            }
            else
            {
                responseText.Add("Unknown command!");
                responseText.Add(this.GetHelpText());
            }

            return new ConversationResponse<WeatherSettings>(priorConversation, new ActivityResponse(string.Join("\r\n\r\n", responseText), attachments));
        }

        private static void OutputCurrentLayers(WeatherSettings priorConversation, List<string> responseText)
        {
            responseText.Add("The following layers will be rendered from bottom to top:");
            foreach (RadarLayerType layer in priorConversation.LayerStack.RadarLayers)
            {
                responseText.Add($"  {RadarLayerTypeNames.GetFriendlyName(layer)}");
            }
        }

        private void OutputValidLayers(List<string> layers)
        {
            layers.Add("The following layers are valid layers:");
            layers.Add($"  {RadarLayerTypeNames.GetAllLayerNames()}");
        }

        public string GetHelpText()
        {
            List<string> lines = new List<string>();
            lines.Add("To get the current weather, send in 'weather LOCATION'. For example, 'weather Seattle, WA'.");
            lines.Add("The weather command will return the weather from the nearest NOAA station to your provided location.");
            lines.Add("If you omit the location, the 'weather' command will use the last-returned location.");
            lines.Add("Layers can be added, removed, or rearranged. To see the current list of layers, type in 'layers'.");
            lines.Add("To add or remove layers, type in 'layers add LAYER' or 'layers remove LAYER'");
            lines.Add("To promote or demote layers (rearranging them), type in 'layers promote LAYER' or 'layers demote LAYER'");

            return string.Join("\r\n\r\n", lines);
        }
    }
}