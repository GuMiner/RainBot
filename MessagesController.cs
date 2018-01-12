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
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private static Lazy<IActivityProcessor> activityProcessor = new Lazy<IActivityProcessor>(() =>
        {
            return new WeatherActivityProcessor(ConfigurationManager.AppSettings["BingMapsApiKey"]);
        });

        private static IActivityProcessor ActivityProcessor { get; } = activityProcessor.Value;

        private static Lazy<IStore> store = new Lazy<IStore>(() =>
        {
            return new AzureBlobStore(ConfigurationManager.AppSettings["AzureStorageConnectionString"], ConfigurationManager.AppSettings["BotContainer"]);
        });

        private static IStore Store { get; } = store.Value;

        [ResponseType(typeof(void))]
        public virtual async Task<HttpResponseMessage> Post([FromBody] Activity activity)
        {
            try
            {
                if (activity != null && activity.GetActivityType() == ActivityTypes.Message)
                {
                    // Get and process
                    IMessageActivity message = activity.AsMessageActivity();

                    ActivityRequest request = new ActivityRequest(
                        recipient: message.Recipient.Name,
                        text: message.Text,
                        from: message.From.Name,
                        fromId: message.From.Id,
                        channelId: message.ChannelId,
                        conversationId: message.Conversation.Id,
                        isGroup: message.Conversation.IsGroup,
                        attachments: message.Attachments?.Select(
                            attachment => new AttachmentRequest(attachment.ContentUrl, attachment.ContentType)
                    ));

                    ActivityResponse response = await MessagesController.ActivityProcessor.ProcessActivityAsync(MessagesController.Store, request).ConfigureAwait(false);

                    // Reply (on a new network connection) back.
                    Activity reply = activity.CreateReply();
                    reply.Text = response.Text;
                    foreach (AttachmentResponse attachment in response.Attachments)
                    {
                        reply.Attachments.Add(new Attachment(attachment.ContentType, attachment.ContentUrl, null, attachment.Name));
                    }

                    using (ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl)))
                    {
                        if (message.Conversation.IsGroup.HasValue && message.Conversation.IsGroup.Value)
                        {
                            await connector.Conversations.SendToConversationAsync((Activity)reply);
                        }
                        else
                        {
                            await connector.Conversations.ReplyToActivityAsync(reply);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                using (ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl)))
                {
                    Activity reply = activity.CreateReply();
                    reply.Text = ex.Message + " " + ex.StackTrace;
                    await connector.Conversations.ReplyToActivityAsync(reply);
                }
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}