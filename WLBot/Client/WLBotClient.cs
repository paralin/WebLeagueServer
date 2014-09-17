using System;
using JWT;
using WLCommon.Model;
using XSockets.Client40;
using XSockets.Client40.Common.Interfaces;

namespace WLBot.Client
{
    /// <summary>
    /// Client to communicate with the network server. Also manages bots.
    /// </summary>
    public class WLBotClient
    {
        private XSocketClient client;
        private IController controller;
        private bool shouldReconnect = false;
        private bool running = false;
        /// <summary>
        /// Create a new client with an XSocket URI.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="botid">ID of the bot in the database</param>
        /// <param name="secret">Corresponding secret of the bot in the db</param>
        public WLBotClient(string url, string botid, string secret)
        {
            client = new XSocketClient(url, "http://localhost", "dotabot");
            controller = client.Controller("dotabot");
            client.QueryString["bid"] = botid;
            Guid data = Guid.NewGuid();
            client.QueryString["bdata"] = data.ToString();
            client.QueryString["btoken"] =
                JWT.JsonWebToken.Encode(new BotToken() {Id = botid, SecretData = data.ToString()}, secret,
                    JwtHashAlgorithm.HS256);
        }

        /// <summary>
        /// Connect and also autoreconnect
        /// </summary>
        public void Start()
        {
            if (running) return;
            running = true;
            if(shouldReconnect) client.Reconnect();
            else client.Open();
            shouldReconnect = true;
        }

        /// <summary>
        /// Disconnect and stop everything
        /// </summary>
        public void Stop()
        {
            running = false;
            client.Disconnect();
        }
    }
}
