using Meebey.SmartIrc4net;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Chernobyl_Relay_Chat
{
    public class CRCClient
    {
        private const char META_DELIM = '☺'; // Separates metadata
        private const char FAKE_DELIM = '☻'; // Separates fake nick for death messages
        private static readonly Regex metaRx = new Regex("^(.*?)" + META_DELIM + "(.*)$");
        private static readonly Regex deathRx = new Regex("^(.*?)" + FAKE_DELIM + "(.*)$");
        private static readonly Regex commandArgsRx = new Regex(@"\S+");

        private static IrcClient client = new IrcClient();
        private static Dictionary<string, string> crcNicks = new Dictionary<string, string>();
        private static DateTime lastDeath = new DateTime();
        private static string lastName, lastChannel, lastQuery, lastFaction;
        private static bool retry = false;

        public static List<string> Users = new List<string>();

        public static void Start()
        {
            client.Encoding = Encoding.UTF8;
            client.SendDelay = 200;
            client.ActiveChannelSyncing = true;

            client.OnConnected += new EventHandler(OnConnected);
            client.OnChannelActiveSynced += new IrcEventHandler(OnChannelActiveSynced);
            client.OnRawMessage += new IrcEventHandler(OnRawMessage);
            client.OnChannelMessage += new IrcEventHandler(OnChannelMessage);
            client.OnQueryMessage += new IrcEventHandler(OnQueryMessage);
            client.OnJoin += new JoinEventHandler(OnJoin);
            client.OnPart += new PartEventHandler(OnPart);
            client.OnQuit += new QuitEventHandler(OnQuit);
            client.OnNickChange += new NickChangeEventHandler(OnNickChange);
            client.OnErrorMessage += new IrcEventHandler(OnErrorMessage);
            client.OnKick += new KickEventHandler(OnKick);
            client.OnDisconnected += new EventHandler(OnDisconnected);
            client.OnTopic += new TopicEventHandler(OnTopic);
            client.OnTopicChange += new TopicChangeEventHandler(OnTopicChange);
            client.OnCtcpRequest += new CtcpEventHandler(OnCtcpRequest);
            client.OnCtcpReply += new CtcpEventHandler(OnCtcpReply);

            try
            {
                client.Connect(CRCOptions.Server, 6667);
                client.Listen();
            }
            catch (CouldNotConnectException)
            {
                Console.WriteLine(CRCStrings.Localize("client_connection_error"));
                CRCDisplay.Stop();
            }
        }

        public static void Stop()
        {
            if (client.IsConnected)
            {
                client.RfcQuit("Safe");
            }
        }

        public static void UpdateSettings()
        {
            if (CRCOptions.Name != lastName)
            {
                client.RfcNick(CRCOptions.Name);
                lastName = CRCOptions.Name;
            }
            if (CRCOptions.ChannelProxy() != lastChannel)
            {
                Users.Clear();
                client.RfcPart(lastChannel);
                client.RfcJoin(CRCOptions.ChannelProxy());
                lastChannel = CRCOptions.ChannelProxy();
            }
            if (CRCOptions.GetFaction() != lastFaction)
            {
                foreach (string nick in crcNicks.Keys)
                {
                    client.SendMessage(SendType.CtcpReply, nick, CRCOptions.GetFaction());
                }
                lastFaction = CRCOptions.GetFaction();
            }
        }

        public static void ChangeNick(string nick)
        {
            CRCOptions.Name = nick;
            lastName = nick;
            client.RfcNick(nick);
        }

        public static void Send(string message)
        {
            client.SendMessage(SendType.Message, CRCOptions.ChannelProxy(), message);
            CRCDisplay.OnOwnChannelMessage(CRCOptions.Name, message);
            CRCGame.OnChannelMessage(CRCOptions.Name, CRCOptions.GetFaction(), message);
        }

        public static void SendDeath(string message)
        {
            string nick = CRCStrings.RandomName(CRCOptions.GameFaction);
            client.SendMessage(SendType.Message, CRCOptions.ChannelProxy(), nick + FAKE_DELIM + CRCOptions.GetFaction() + META_DELIM + message);
            CRCDisplay.OnChannelMessage(nick, message);
            CRCGame.OnChannelMessage(nick, CRCOptions.GameFaction, message);
        }

        public static void SendQuery(string nick, string message)
        {
            client.SendMessage(SendType.Message, nick, CRCOptions.GetFaction() + META_DELIM + message);
            CRCDisplay.OnQueryMessage(CRCOptions.Name, nick, message);
            CRCGame.OnQueryMessage(CRCOptions.Name, nick, CRCOptions.GetFaction(), message);
        }

        public static bool SendReply(string message)
        {
            if (lastQuery != null)
            {
                SendQuery(lastQuery, message);
                return true;
            }
            return false;
        }

        private static string GetMetadata(string message, out string fakeNick, out string faction)
        {
            Match metaMatch = metaRx.Match(message);
            if (metaMatch.Success)
            {
                Match deathMatch = deathRx.Match(metaMatch.Groups[1].Value);
                if (deathMatch.Success)
                {
                    fakeNick = deathMatch.Groups[1
