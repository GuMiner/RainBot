using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Bot.Connector;
using System.Net.Http;
using System.Web.Http.Description;
using System.Net;
using BotCommon.Activity;
using System.Linq;
using Microsoft.Bot.Builder.Dialogs;
using System;
using BotCommon.Processors;
using BotCommon;
using System.Configuration;
using BotCommon.Storage;

namespace RainBot
{
    public class MessagesController : ActivityMessagesController
    {
        private static Lazy<IActivityProcessor> activityProcessor = new Lazy<IActivityProcessor>(() =>
        {
            return new WeatherActivityProcessor(ConfigurationManager.AppSettings["BingMapsApiKey"]);
        });

        public override IActivityProcessor ActivityProcessor { get; } = activityProcessor.Value;

        private static Lazy<IStore> store = new Lazy<IStore>(() =>
        {
            return new AzureBlobStore(ConfigurationManager.AppSettings["AzureStorageConnectionString"], ConfigurationManager.AppSettings["BotContainer"]);
        });

        public override IStore Store { get; } = store.Value;
    }
}