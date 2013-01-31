using System;
using System.Threading;
using TTAPI;
using Utilities;

namespace TTIRC
{
    class RoomPainter
    {
        public static RoomPainter Instance { get; protected set; }

        Timer threadTicker;
        bool enabled;
        int processing = 0;
        LockFreeQueue<Action> paintingQueue;

        TTClient client;

        int lastChatWidth = Console.BufferWidth;
        string[] chatLines;
        int marqueeOffset;

        public event Action OnPaint;

        public RoomPainter(TTClient parent)
        {
            client = parent;
            paintingQueue = new LockFreeQueue<Action>();
            chatLines = new string[0];
            threadTicker = new Timer(Tick, null, 300, 300); /// 4 fps :D
            Instance = this;
        }

        public void Enable()
        {
            if(threadTicker == null)
                threadTicker = new Timer(Tick, null, 250, 250);
            enabled = true;
        }

        public void Disable()
        {
            enabled = false;
        }

        void Tick(object state)
        {
            if (!enabled)
                return;
            if (Interlocked.CompareExchange(ref processing, 1, 0) == 1) return;

            //Console.Clear();
            PaintMarquee();
            PaintChat();
            PaintInput();

            Action callback;
            if (paintingQueue.Pop(out callback)) callback();
            if (OnPaint != null)
                OnPaint();

            processing = 0;
        }

        public void NewChat(string message)
        {
            string[] contents = BuildContents(message);
            paintingQueue.Push(() =>
            {
                MoveChat(contents);
                //chatLines = AppendNewText(contents, chatLines);
                PaintChat();
            });
        }

        string[] BuildContents(string message)
        {
            string[] contents;
            if (message.Length > Console.BufferWidth)
            {
                int lines = (int)Math.Ceiling((double)message.Length / (double)Console.BufferWidth);
                contents = new string[lines];
                for (int i = 0; i < lines; ++i)
                    contents[i] = message.Substring(i * Console.BufferWidth, (i + 1) * Console.BufferWidth > message.Length ? message.Length - (i * Console.BufferWidth) : Console.BufferWidth);
            }
            else
                contents = new string[] { message };

            return contents;
        }
        string[] AppendNewText(string[] contents, string[] oldChat)
        {
            string[] newChat = new string[Console.BufferHeight]; // Is CursorSize correct? o_O
            int oldChatLines = newChat.Length - contents.Length;
            int offset = (oldChat.Length + contents.Length) - newChat.Length;
            bool needsOmitting = offset > 0;
            if (!needsOmitting)
            {
                newChat = new string[oldChat.Length + contents.Length];
                offset = 0;
                oldChatLines = oldChat.Length;
            }
            if (oldChatLines != 0)
                Array.Copy(oldChat, offset, newChat, 0, oldChatLines);
            Array.Copy(contents, 0, newChat, oldChatLines, contents.Length);
            return newChat;
        }
        void MoveChat(string[] newLines)
        {
            int offset = (Console.WindowHeight - 2) - newLines.Length;

            Console.MoveBufferArea(0, 1 + newLines.Length, Console.WindowWidth, offset, 0, 1);
            ++offset;
            for (int i = 0; i < newLines.Length; ++i)
            {
                Console.CursorTop = offset + i;
                Console.CursorLeft = 0;
                Console.Write(newLines[i]);
            }
        }
        void PaintChat()
        {
            int offset = Math.Max(chatLines.Length - (Console.WindowHeight - 2), 0);
            for (int i = 0; i < Console.WindowHeight - 2 && i < chatLines.Length; ++i)
            {
                Console.CursorTop = i + 1;
                Console.CursorLeft = 0;

                Console.Write(chatLines[i + offset]);
            }
        }
        void PaintInput()
        {
            Console.CursorTop = Console.WindowHeight - 1;
            Console.CursorLeft = 0;
            Console.Write(TurnTable.readerCurrentLine);
        }
        void PaintMarquee()
        {
            string songName;
            int maxWidth = Console.WindowWidth - 1;

            if (client.roomInformation == null || client.roomInformation.metadata.current_song == null)
            {
                songName = "No active or playing song.";//string.Join(//new String(.ToCharArray(), Math.Floor(maxWidth / 15));
            }
            else
            {
                SongMetadata songmeta = client.roomInformation.metadata.current_song.metadata;
                songName = String.Format("{0} - {1}", songmeta.song, songmeta.artist);
            }


            int cap = Math.Min(songName.Length - marqueeOffset, maxWidth - marqueeOffset);
            string left = "";
            if (cap > -1)
                left = songName.Substring(marqueeOffset, cap);
            string right = "";
            if (marqueeOffset != 0)
                right = songName.Substring(0, Math.Min(marqueeOffset, songName.Length));
            if (marqueeOffset > songName.Length) right = string.Join("", right, new string(' ', marqueeOffset - right.Length));
            if (marqueeOffset == maxWidth) marqueeOffset = 0;

            string center = new string(' ', maxWidth - (left.Length + right.Length));
            //string farRight = new string(' ', Console.WindowWidth - (left.Length + center.Length + right.Length));
            string joined = String.Format("{0}{1}{2}", left, center, right);

            ++marqueeOffset;
            Console.CursorTop = 0;
            Console.CursorLeft = 0;
            Console.Write(joined);

            ///TODO: Implement this!  :D
        }
    }
}
