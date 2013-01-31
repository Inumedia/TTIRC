using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using AxShockwaveFlashObjects;
using TTAPI;
using TTAPI.Recv;
using TTAPI.Send;

namespace TTIRC
{
    public class TurnTable : Form
    {
        [STAThread]
        public static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            using (TurnTable yay = new TurnTable())
                Application.Run(yay);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
        }

        bool readerActive;
        bool readerShouldMaintainLine;
        int preserveLineNumber;
        Thread reader;
        public Dictionary<ConsoleKey, Action> hotkeyCallbacks;
        public static StringBuilder readerCurrentLine;
        RoomPainter painter;

        List<Room> roomList;

        public Song currentSong;
        State currentState;
        Volume currentVolumeSettings;

        TTClient client;
        UserAuth userInformation;
        event Action<string> OnGetInput;

        private AxShockwaveFlash flash;
        public HTTPSimpleStreamInterface simpleStream;

        public TurnTable()
        {
            base.ShowInTaskbar = false;
            reader = new Thread(ConsoleReader);
            InitializeComponent();
            InitializeStream();
            currentState = State.LoggedOut;

            if (!Security.LoadEncryptedData(out currentVolumeSettings))
                currentVolumeSettings = new Volume();
#if DEBUG
            //TTClient.DEBUG_STRING += Log;
#endif
            //userInformation = new Credentials(); // We can change this to load existing later.
        }

        void Log(string output)
        {
            System.Diagnostics.Debug.WriteLine(output);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            base.Location = new Point(SystemInformation.VirtualScreen.Width + 100, SystemInformation.VirtualScreen.Height + 100);
            base.Size = new Size(1, 1);
            reader.Start();
        }

        protected override void OnShown(EventArgs e)
        {
            Console.WriteLine("Calling Turntable's player service");
            flash.LoadMovie(0, "http://turntable.fm/static/swf/HTTPSimpleStream.swf");
            Console.WriteLine("Yes, I'd like 2 hoes, please.");
            base.OnShown(e);

            AttemptLogin();
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TurnTable));
            this.flash = new AxShockwaveFlashObjects.AxShockwaveFlash();
            ((System.ComponentModel.ISupportInitialize)(this.flash)).BeginInit();
            this.SuspendLayout();
            // 
            // flash
            // 
            this.flash.Enabled = true;
            this.flash.Location = new System.Drawing.Point(30, 12);
            this.flash.Name = "flash";
            this.flash.OcxState = ((System.Windows.Forms.AxHost.State)(resources.GetObject("flash.OcxState")));
            this.flash.Size = new System.Drawing.Size(240, 240);
            this.flash.TabIndex = 0;
            // 
            // TurnTable
            // 
            this.ClientSize = new System.Drawing.Size(282, 255);
            this.Controls.Add(this.flash);
            this.Name = "TurnTable";
            ((System.ComponentModel.ISupportInitialize)(this.flash)).EndInit();
            this.ResumeLayout(false);

        }

        public void InitializeStream()
        {
            simpleStream = new HTTPSimpleStreamInterface(flash);
            simpleStream.OnInitialized += new Action(() => currentVolumeSettings.SetStream(simpleStream));
        }

        void ConsoleReader()
        {
            hotkeyCallbacks = new Dictionary<ConsoleKey, Action>();
            readerCurrentLine = new StringBuilder(Console.LargestWindowWidth);
            while (!IsDisposed && !Disposing)
            {
                ConsoleKeyInfo info;
                int len = 0;
                do
                {
                    info = Console.ReadKey(true);

                    if (!readerActive) continue;

                    if (info.Modifiers == ConsoleModifiers.Control)
                    {
                        if (hotkeyCallbacks.ContainsKey(info.Key))
                            hotkeyCallbacks[info.Key]();
                        continue;
                    }

                    if (info.Key == ConsoleKey.Backspace)
                    {
                        if (len == 0)
                        {
                            if (Console.CursorTop != 0 && !readerShouldMaintainLine)
                                --Console.CursorTop;
                            else if (readerShouldMaintainLine)
                                Console.CursorTop = preserveLineNumber;
                            continue;
                        }
                        --len;
                        readerCurrentLine.Remove(len, 1);
                        if (len > 79)
                        {
                            Console.CursorLeft = 0;
                            Console.Write(readerCurrentLine.ToString().Substring(readerCurrentLine.Length - 79, 79));
                            Console.CursorLeft = 79;
                        }
                        else
                        {
                            --Console.CursorLeft;
                            Console.Write(' ');
                            --Console.CursorLeft;
                        }
                        continue;
                    }

                    if (info.Key != ConsoleKey.Enter)
                        readerCurrentLine.Append(info.KeyChar);
                    else if (readerShouldMaintainLine) break;
                    else { ++Console.CursorTop; Console.CursorLeft = 0; break; }
                    if (len >= 79)
                    {
                        Console.CursorLeft = 0;
                        Console.Write(readerCurrentLine.ToString().Substring(readerCurrentLine.Length-79,79));
                        Console.CursorLeft = 79;
                    }
                    else
                    {
                        Console.Write(info.KeyChar);
                    }
                    ++len;
                } while (info.Key != ConsoleKey.Enter);

                string input = readerCurrentLine.ToString();
                readerCurrentLine.Clear();

                if (readerShouldMaintainLine)
                {
                    string overriding = new string(' ', len);
                    Console.CursorLeft = Console.CursorLeft - len;
                    Console.Write(overriding);
                    Console.CursorLeft = Console.CursorLeft - len;
                }

                if (OnGetInput != null) OnGetInput(input);
            }
        }

        void AttemptLogin()
        {
            if (Security.LoadEncryptedData(out userInformation))
                DisplayRooms();
            else
                DisplayLogin();
        }

        void DisplayLogin()
        {
            currentState = State.LoggedOut;
            Console.Clear();
            Console.Error.Write("Login: ");
            string email;
            StringBuilder password = new StringBuilder();
            readerActive = true;
            OnGetInput += (user) =>
            {
                readerActive = false;
                email = user;
                OnGetInput = null;

                Console.Error.Write("Password: ");
                ConsoleKeyInfo key;
                do
                {
                    key = Console.ReadKey(true);
                    if (key.Key != ConsoleKey.Enter)
                        password.Append(key.KeyChar);
                } while (key.Key != ConsoleKey.Enter);

                currentState = State.LoggingIn;
                Console.Error.WriteLine("Logging in...");

                if (!TTWebInterface.Request<UserAuth>(new EmailLogin(email, password.ToString()), out userInformation))
                {
                    new Thread(() => Invoke(new Action(DisplayLogin)));
                    return;
                }

                currentState = State.LoggedIn;
                Console.Error.WriteLine("Logged in!");

                Security.SaveEncryptedData(userInformation);

                Thread.Sleep(500);
                DisplayRooms();
            };
        }

        void DisplayRooms()
        {
            Console.WriteLine("Calling Turntable's room service...");
            ListRooms fetchList = new ListRooms()
            {
                userauth = userInformation.userauth,
                userid = userInformation.userid
            };
            ///TODO: If this fails?
            Rooms topRooms = TTWebInterface.Request<Rooms>(fetchList);
            Console.WriteLine("Yes, I need a room for these fine hoes...");
            GetFavorites fetchFavs = new GetFavorites()
            {
                userauth = userInformation.userauth,
                userid = userInformation.userid
            };
            FavoriteList favs = TTWebInterface.Request<FavoriteList>(fetchFavs);
            ///Get Favorites.
            Console.Clear();
            Console.CursorTop = 0;
            Console.CursorLeft = 0;
            roomList = new List<Room>();
            for (int i = 0; i < topRooms.rooms.Length && i < Console.CursorSize - 1; ++i)
            {
                Rooms.RoomUserPair pair = topRooms.rooms[i];
                bool isFav;
                if (isFav = Array.Exists(favs.list, (t) => t == pair.room.roomid))
                    Console.BackgroundColor = ConsoleColor.DarkCyan;
                if (pair.room.metadata.listeners == pair.room.metadata.max_size)
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                string roomInfo = string.Format("{0}:{1} {2}^{3}|{4}", i, pair.room.name, pair.room.metadata.listeners, pair.room.metadata.djcount, pair.room.metadata.max_djs);
                string buffer = new string(' ', Console.BufferWidth - 2 - roomInfo.Length);
                Console.Write(string.Join("", roomInfo, buffer));
                if (isFav)
                {
                    Console.BackgroundColor = ConsoleColor.DarkCyan;
                    Console.Write("<3");
                }
                else Console.Write("  ");
                Console.BackgroundColor = ConsoleColor.Black;
                roomList.Add(pair.room);
            }
            Console.SetCursorPosition(0, Console.CursorSize - 1);
            Console.Write("Join room:");
            //Console.SetCursorPosition(10, Console.CursorSize - 1);
            readerShouldMaintainLine = true;
            readerActive = true;
            preserveLineNumber = Console.CursorSize - 1;

            OnGetInput += (room) =>
            {
                int roomSelection = -1;
                if (int.TryParse(room, out roomSelection))
                    JoinRoom(roomSelection);
                else
                {
                    Console.SetCursorPosition(0, Console.CursorSize - 2);
                    Console.WriteLine("Error!  Please select a proper room number!");
                    Console.Write("Join room:");
                    Console.Write(new string(' ', room.Length));
                    Console.SetCursorPosition(10, Console.CursorSize - 1);
                }
            };
        }

        void JoinRoom(int roomIndex, bool resetReader = true)
        {
            currentState = State.GettingRoom;
            if (resetReader)
            {
                OnGetInput = null;
                readerActive = false;
            }
            Room toJoin = roomList[roomIndex];
            client = new TTClient(userInformation.userid, userInformation.userauth, toJoin.roomid);
            painter = new RoomPainter(client);

            client.OnJoinedRoom += new Action(client_OnJoinedRoom);

            Console.Clear();
            Console.SetCursorPosition(0, 0);
            Console.WriteLine("Now where did I leave my card for the room... ");

            client.StreamsToSync += new StreamSync(simpleStream.loadStream);
            client.StreamsToSync += new StreamSync(client_StreamsToSync);
            client.OnSpeak += RoomPainter.Instance.NewChat;

            int attempts = 0;
            while (!client.Connect() && attempts++ < 5) Console.WriteLine("Damnit, where the hell is it... x{0}", attempts);
            if (!client.isConnected)
            {
                DisplayRooms();
                return;
            }
        }

        void client_StreamsToSync(string key, int segment, int timeStamp, int timeout)
        {
            currentSong = client.roomInformation.metadata.current_song;
        }

        void client_OnJoinedRoom()
        {
            currentState = State.InRoom;
            Console.WriteLine("There it is!");
            Thread.Sleep(500);
            Console.Clear();
            Console.CursorTop = Console.CursorSize;
            OnGetInput += new Action<string>(TurnTable_Speak);
            readerActive = true;
            LoadHotkeys();
            painter.Enable();
            //painter.NewChat(String.Format("Joined room {0}!", client.roomInformation.name));
        }

        void TurnTable_Speak(string obj)
        {
            client.Send(new SpeakAPI(obj));
        }

        void extendRoomCount()
        {
            /// Todo.  :D
        }

        void LoadHotkeys()
        {
            hotkeyCallbacks.Clear();
            hotkeyCallbacks.Add(ConsoleKey.UpArrow, currentVolumeSettings.IncreaseVolume);
            hotkeyCallbacks.Add(ConsoleKey.DownArrow, currentVolumeSettings.DecreaseVolume);
            hotkeyCallbacks.Add(ConsoleKey.M, currentVolumeSettings.ToggleMute);
        }
    }
}
