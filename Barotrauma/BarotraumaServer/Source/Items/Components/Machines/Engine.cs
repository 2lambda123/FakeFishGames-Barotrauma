﻿using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using System.Xml.Linq;
using Barotrauma.Networking;
using Lidgren.Network;

namespace Barotrauma.Items.Components
{
    partial class Engine : Powered, IServerSerializable, IClientSerializable
    {
        public void ServerWrite(NetBuffer msg, Client c, object[] extraData = null)
        {
            //force can only be adjusted at 10% intervals -> no need for more accuracy than this
            msg.WriteRangedInteger(-10, 10, (int)(targetForce / 10.0f));
        }

        public void ServerRead(ClientNetObject type, NetBuffer msg, Client c)
        {
            float newTargetForce = msg.ReadRangedInteger(-10, 10) * 10.0f;

            if (item.CanClientAccess(c))
            {
                if (Math.Abs(newTargetForce - targetForce) > 0.01f)
                {
                    GameServer.Log(c.Character.LogName + " set the force of " + item.Name + " to " + (int)(newTargetForce) + " %", ServerLog.MessageType.ItemInteraction);
                }

                targetForce = newTargetForce;
            }

            //notify all clients of the changed state
            item.CreateServerEvent(this);
        }
    }
}
