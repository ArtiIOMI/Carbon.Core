﻿///
/// Copyright (c) 2022 Carbon Community 
/// All rights reserved
/// 

using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oxide.Game.Rust.Libraries
{
    public class Rust
    {
        internal readonly Player Player = new Player ();
        internal readonly Server Server = new Server ();

        public string QuoteSafe ( string str )
        {
            return str.Quote ();
        }

        public void BroadcastChat ( string name, string message = null, string userId = "0" )
        {
            Server.Broadcast ( message, name, Convert.ToUInt64 ( userId ) );
        }

        public void SendChatMessage ( BasePlayer player, string name, string message = null, string userId = "0" )
        {
            Player.Message ( player, message, name, Convert.ToUInt64 ( userId ) );
        }

        public void RunClientCommand ( BasePlayer player, string command, params object [] args )
        {
            Player.Command ( player, command, args );
        }
        public void RunServerCommand ( string command, params object [] args )
        {
            Server.Command ( command, args );
        }
    }
}
