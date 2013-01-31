using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TTIRC
{
    public enum State
    {
        LoggedOut = 0,
        GettingUsername = 1 << 0,
        GettingPassword = 1 << 1,
        LoggingIn = 1 << 2,
        LoggedIn = 1 << 3,
        GettingRoom = 1 << 4,
        InRoom = 1 << 5
    }
}
