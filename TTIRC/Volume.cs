using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TTIRC
{
    public class Volume
    {
        HTTPSimpleStreamInterface stream;

        public byte volumeLevel;
        public bool muted;

        public Volume()
        {
            volumeLevel = 75;
            muted = false;
        }

        public void IncreaseVolume()
        {
            volumeLevel = (byte)Math.Min(volumeLevel + 5, 100);
            if (muted) muted = false;
            stream.setVolume(volumeLevel);
        }

        public void DecreaseVolume()
        {
            //volumeLevel -= 5;
            volumeLevel = (byte)Math.Max(volumeLevel - 5, 0);
            stream.setVolume(volumeLevel);
        }

        public void ToggleMute()
        {
            muted = !muted;
            stream.setVolume(0);
        }

        public void SetStream(HTTPSimpleStreamInterface target)
        {
            stream = target;
            stream.setVolume(muted ? (byte)0 : volumeLevel);
        }
    }
}
