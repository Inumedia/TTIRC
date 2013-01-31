using System;
using System.Collections.Generic;
using System.Reflection;
using AxShockwaveFlashObjects;
using Flash.External;

namespace TTIRC
{
    public class HTTPSimpleStreamInterface
    {
        AxShockwaveFlash flash;
        ExternalInterfaceProxy proxy;
        static Dictionary<string, MethodInfo> cachedMethods;

        public event Action OnInitialized;

        static HTTPSimpleStreamInterface()
        {
            cachedMethods = new Dictionary<string, MethodInfo>();
            MethodInfo[] methods = typeof(HTTPSimpleStreamInterface).GetMethods();
            for (int i = 0; i < methods.Length; ++i)
                cachedMethods.Add(methods[i].Name, methods[i]);
        }

        public HTTPSimpleStreamInterface(AxShockwaveFlash movie)
        {
            flash = movie;
            proxy = new ExternalInterfaceProxy(flash);
            proxy.ExternalInterfaceCall += new ExternalInterfaceCallEventHandler(proxy_ExternalInterfaceCall);
        }

        object proxy_ExternalInterfaceCall(object sender, ExternalInterfaceCallEventArgs e)
        {
            /// We don't know yet? :D
            string function = e.FunctionCall.FunctionName;
            object[] args = Array.ConvertAll(e.FunctionCall.Arguments, (o) => o.ToString());
            //string call = String.Format("HttpSimpleStream Called : {0}({1})", function, String.Join(",", args));
            //Console.WriteLine(call);

            if (cachedMethods.ContainsKey(function))
                cachedMethods[function].Invoke(this, args); /// Oh the irony that this is actually very easy.

            return null;
        }

        public void HTTPSimpleStreamCallback(string state)
        {
            switch (state)
            {
                case "initialized":
                    if (OnInitialized != null) OnInitialized();
                    break;
                case"streamstart":

                    break;
            }
        }

        public void LOG(string output)
        {
            //Console.WriteLine("[Stream] Log: {0}", output);
        }

        public int getPosition()
        {
            int position = -1;
            object pos = proxy.Call("getPosition");
            if (pos is int) return (int)pos;
            if (pos is string)
                int.TryParse((string)pos, out position);
            return position;
            //return int.parse(a.getPosition(""));
        }

        public void setVolume(byte volume) /// 0-100
        {
            proxy.Call("setVolume", volume.ToString());
            //a.setVolume(volume.toString());
        }

        /// <summary>
        /// Used to load and play a stream of a song.
        /// </summary>
        /// <param name="key">netloc + roomid</param>
        public void loadStream(string key, int current_seg, int timestamp, int timeout = 500)
        {
            string call = String.Format("{0},{1},{2},{3}", key, current_seg, timestamp, timeout);
            proxy.Call("loadStream", call);
        }

        public void closeStream()
        {
            proxy.Call("closeStream");
            //a.closeStream("");
        }


        public void Play()
        {
            proxy.Call("resume");
            //a.resume("")
        }

        public void Pause()
        {
            proxy.Call("pause");
            //a.pause("");
        }
    }
}
