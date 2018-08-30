﻿using Barotrauma.Networking;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class Wire : ItemComponent, IDrawableComponent, IServerSerializable
    {
        private void CreateNetworkEvent()
        {
            if (GameMain.Server == null) return;
            //split into multiple events because one might not be enough to fit all the nodes
            int eventCount = Math.Max((int)Math.Ceiling(nodes.Count / (float)MaxNodesPerNetworkEvent), 1);
            for (int i = 0; i < eventCount; i++)
            {
                GameMain.Server.CreateEntityEvent(item, new object[] { NetEntityEvent.Type.ComponentState, item.components.IndexOf(this), i });
            }

        }

        public void ServerWrite(NetBuffer msg, Client c, object[] extraData = null)
        {
            int eventIndex = (int)extraData[2];
            int nodeStartIndex = eventIndex * MaxNodesPerNetworkEvent;
            int nodeCount = MathHelper.Clamp(nodes.Count - nodeStartIndex, 0, MaxNodesPerNetworkEvent);

            msg.WriteRangedInteger(0, (int)Math.Ceiling(MaxNodeCount / (float)MaxNodesPerNetworkEvent), eventIndex);
            msg.WriteRangedInteger(0, MaxNodesPerNetworkEvent, nodeCount);
            for (int i = nodeStartIndex; i < nodeStartIndex + nodeCount; i++)
            {
                msg.Write(nodes[i].X);
                msg.Write(nodes[i].Y);
            }
        }
    }
}
