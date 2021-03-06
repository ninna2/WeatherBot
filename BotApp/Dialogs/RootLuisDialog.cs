﻿using System;
using System.Threading.Tasks;

using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using System.Web.Configuration;

using System.Net.Http;
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Connector;
using System.Text;
using BotApp.Model;
using BotApp.Service;

namespace BotApp.Dialogs
{
    [Serializable]
    public class RootLuisDialog : LuisDialog<object>
    {
        //private const string ENTITY_LOCATION = "Location";
        private const string ENTITY_LOCATION = "Weather.Location";

        private WeatherQuery weatherQuery = new WeatherQuery();

        private readonly OpenWeatherMapService weatherService = new OpenWeatherMapService();

        public RootLuisDialog() : base(new LuisService(new LuisModelAttribute(WebConfigurationManager.AppSettings["LuisAppId"], WebConfigurationManager.AppSettings["LuisAPIKey"])))
        {
        }

        // Intent "" or None
        [LuisIntent("")]
        [LuisIntent("None")]
        public async Task None(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
        {
#if DEBUG
            await context.PostAsync(this.formatLuisResult(result));
#endif
            var act = await activity;
            string message = $"すいません。わかりません";
            await context.PostAsync(message);
            context.Wait(this.MessageReceived);
        }

        // getWeatherの場合
        [LuisIntent("Weather.GetForecast")]
        [LuisIntent("Weather.GetCondition")]
        public async Task getWeatherIntent(IDialogContext context, LuisResult result)
        {
#if DEBUG
            await context.PostAsync(formatLuisResult(result));
#endif
            var weatherQuery = new WeatherQuery();

            EntityRecommendation LocationRecommendation;
            if (result.TryFindEntity(ENTITY_LOCATION, out LocationRecommendation))
            {
                weatherQuery.Location = LocationRecommendation.Entity;
            }

            var weatherFormDialog = new FormDialog<WeatherQuery>(weatherQuery, this.BuildWeatherForm, FormOptions.PromptInStart, result.Entities);
            context.Call(weatherFormDialog, this.ResumeAfterFormDialog);
        }

        private IForm<WeatherQuery> BuildWeatherForm()
        {
            OnCompletionAsyncDelegate<WeatherQuery> processWeatherSearch = async (context, state) =>
            {
                var message = "Searching...";
                if (!string.IsNullOrEmpty(state.Location))
                {
                    message += $" in {state.Location}...";
                }
                await context.PostAsync(message);
            };

            return new FormBuilder<WeatherQuery>()
                .Field(nameof(WeatherQuery.Location))
                .OnCompletion(processWeatherSearch)
                .Build();
        }

        private async Task ResumeAfterFormDialog(IDialogContext context, IAwaitable<WeatherQuery> result)
        {
            try
            {
                var searchQuery = await result;
                var weatherInfo = await weatherService.GetWeatherAsync(searchQuery);

                var message = context.MakeMessage();
                message.Text = $"{weatherInfo.Name}の天気は...";
                message.Attachments.Add(new Attachment()
                {
                    ContentUrl = weatherService.GetWeatherIcon(weatherInfo),
                    ContentType = "image/png"
                });

                await context.PostAsync(message);
#if DEBUG 
                await context.PostAsync(this.formatWeatherInfo(weatherInfo));
#endif
            }
            catch (FormCanceledException ex)
            {
                //
            }
            finally
            {
                context.Done<object>(null);
            }
        }

        private string formatWeatherInfo(WeatherInfo weatherInfo)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"--City-- \n\n");
            sb.AppendLine($"CityID: {weatherInfo.Id} \n\n");
            sb.AppendLine($"City: {weatherInfo.Name} \n\n");

            sb.AppendLine($"--Weather-- \n\n");
            weatherInfo?.Weather.ForEach(w =>
            {
                sb.AppendLine($"WeatherID: {w.Id} \n\n");
                sb.AppendLine($"Weather: {w.Main} \n\n");
                sb.AppendLine($"Description: {w.Description} \n\n");
                sb.AppendLine($"WeatherIcon: {w.Icon} \n\n");
            });

            sb.AppendLine($"--Sys-- \n\n");
            sb.AppendLine($"Country: {weatherInfo.sys.Country} \n\n");

            sb.AppendLine($"--Main-- \n\n");
            sb.AppendLine($"Temperature: {weatherInfo.Main.Temperature} \n\n");
            sb.AppendLine($"MaxTemperature: {weatherInfo.Main.MaxTemperature} \n\n");
            sb.AppendLine($"MinTemperature: {weatherInfo.Main.MinTemperature} \n\n");

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            epoch = epoch.AddSeconds(weatherInfo.dt).ToLocalTime();
            sb.AppendLine($"--Timestamp-- \n\n");
            sb.AppendLine($"Timestamp: {epoch.ToLongTimeString()} \n\n");

            return sb.ToString();
        }

        private string formatLuisResult(LuisResult result)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("--Query-- \n\n");
            sb.AppendLine($"{result.Query} \n\n");

            sb.AppendLine("--Intents-- \n\n");
            foreach (IntentRecommendation intent in result.Intents)
            {
                sb.AppendLine($"{intent.Intent}. Score: {intent.Score} \n\n");
            }

            sb.AppendLine("--Entities-- \n\n");
            foreach (EntityRecommendation e in result.Entities)
            {
                sb.AppendLine($"  {e.Entity}. Type: {e.Type} \n\n");
            }

            return sb.ToString();
        }
    }
}