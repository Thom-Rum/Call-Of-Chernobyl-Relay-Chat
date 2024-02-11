using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Meebey.SmartIrc4net;
using System;
using System.Collections.Generic;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Chernobyl_Relay_Chat
{
    public class CRCClient : Window
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

        private TextBlock debugTextBlock;

        public CRCClient()
        {
            InitializeComponent();
#if DEBUG
            Thread debugThread = new Thread(ShowDebugWindow);
            debugThread.IsBackground = true;
            debugThread.Start();
#endif
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
                ShowErrorMessageBox(CRCStrings.Localize("client_connection_error"), CRCStrings.Localize("crc_name"));
                CRCDisplay.Stop();
            }
#if DEBUG
            debugTextBlock.Text += "Debug window initialized.\n";
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            debugTextBlock = this.FindControl<TextBlock>("DebugTextBlock");
        }

        public void ShowErrorMessageBox(string message, string title)
        {
            MessageBox.Show(this, message, title, MessageBoxType.Error);
        }

        private void ShowDebugWindow()
        {
            DebugDisplay debug = new DebugDisplay();
            debug.ShowDialog();
        }

        // Other methods remain unchanged...
    }
}
