using System;
using Microsoft.Bot.Builder.FormFlow;

namespace BotApp.Model
{
    [Serializable]
    public class WeatherQuery
    {
        [Prompt("どこの天気が知りたいの？")]
        [Optional]
        public string Location{ get; set; }

        public string Datetime{ get; set; }
    }
}