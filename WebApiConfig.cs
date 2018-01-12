using BotCommon.Web;
using System.Web.Http;

namespace RainBot
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            CommonWebConfig.Register(config);
        }
    }
}
