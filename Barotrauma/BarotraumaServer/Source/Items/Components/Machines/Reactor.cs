﻿using Barotrauma.Networking;
using Lidgren.Network;
using System;

namespace Barotrauma.Items.Components
{
    partial class Reactor
    {
        private Client blameOnBroken;

        private float? nextServerLogWriteTime;
        private float lastServerLogWriteTime;

        public void ServerRead(ClientNetObject type, NetBuffer msg, Client c)
        {
            bool autoTemp = msg.ReadBoolean();
            bool shutDown = msg.ReadBoolean();
            float fissionRate = msg.ReadRangedSingle(0.0f, 100.0f, 8);
            float turbineOutput = msg.ReadRangedSingle(0.0f, 100.0f, 8);

            if (!item.CanClientAccess(c)) return;

            if (!autoTemp && AutoTemp) blameOnBroken = c;
            if (turbineOutput < targetTurbineOutput) blameOnBroken = c;
            if (fissionRate > targetFissionRate) blameOnBroken = c;
            if (!this.shutDown && shutDown) blameOnBroken = c;

            AutoTemp = autoTemp;
            this.shutDown = shutDown;
            targetFissionRate = fissionRate;
            targetTurbineOutput = turbineOutput;

            LastUser = c.Character;
            if (nextServerLogWriteTime == null)
            {
                nextServerLogWriteTime = Math.Max(lastServerLogWriteTime + 1.0f, (float)Timing.TotalTime);
            }

            //need to create a server event to notify all clients of the changed state
            unsentChanges = true;
        }

        public void ServerWrite(NetBuffer msg, Client c, object[] extraData = null)
        {
            msg.Write(autoTemp);
            msg.Write(shutDown);
            msg.WriteRangedSingle(temperature, 0.0f, 100.0f, 8);
            msg.WriteRangedSingle(targetFissionRate, 0.0f, 100.0f, 8);
            msg.WriteRangedSingle(targetTurbineOutput, 0.0f, 100.0f, 8);
            msg.WriteRangedSingle(degreeOfSuccess, 0.0f, 1.0f, 8);
        }
    }
}
