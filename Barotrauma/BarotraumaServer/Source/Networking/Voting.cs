﻿using Barotrauma.Networking;
using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    partial class Voting
    {
        public bool AllowSubVoting
        {
            get { return allowSubVoting; }
            set {  allowSubVoting = value; }
        }
        public bool AllowModeVoting
        {
            get { return allowModeVoting; }
            set { allowModeVoting = value; }
        }

        public void ServerRead(NetIncomingMessage inc, Client sender)
        {
            if (GameMain.Server == null || sender == null) return;

            byte voteTypeByte = inc.ReadByte();
            VoteType voteType = VoteType.Unknown;
            try
            {
                voteType = (VoteType)voteTypeByte;
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Failed to cast vote type \"" + voteTypeByte + "\"", e);
                return;
            }

            switch (voteType)
            {
                case VoteType.Sub:
                    string subName = inc.ReadString();
                    Submarine sub = Submarine.SavedSubmarines.FirstOrDefault(s => s.Name == subName);
                    sender.SetVote(voteType, sub);
                    break;

                case VoteType.Mode:
                    string modeName = inc.ReadString();
                    GameModePreset mode = GameModePreset.list.Find(gm => gm.Name == modeName);
                    if (!mode.Votable) break;

                    sender.SetVote(voteType, mode);
                    break;
                case VoteType.EndRound:
                    if (!sender.HasSpawned) return;
                    sender.SetVote(voteType, inc.ReadBoolean());

                    GameMain.NetworkMember.EndVoteCount = GameMain.Server.ConnectedClients.Count(c => c.HasSpawned && c.GetVote<bool>(VoteType.EndRound));
                    GameMain.NetworkMember.EndVoteMax = GameMain.Server.ConnectedClients.Count(c => c.HasSpawned);

                    break;
                case VoteType.Kick:
                    byte kickedClientID = inc.ReadByte();

                    Client kicked = GameMain.Server.ConnectedClients.Find(c => c.ID == kickedClientID);
                    if (kicked != null && !kicked.HasKickVoteFrom(sender))
                    {
                        kicked.AddKickVote(sender);
                        Client.UpdateKickVotes(GameMain.Server.ConnectedClients);

                        GameMain.Server.SendChatMessage(sender.Name + " has voted to kick " + kicked.Name, ChatMessageType.Server, null);
                    }

                    break;
            }

            inc.ReadPadBits();

            GameMain.Server.UpdateVoteStatus();
        }

        public void ServerWrite(NetBuffer msg)
        {
            if (GameMain.Server == null) return;

            msg.Write(allowSubVoting);
            if (allowSubVoting)
            {
                List<Pair<object, int>> voteList = GetVoteList(VoteType.Sub, GameMain.Server.ConnectedClients);
                msg.Write((byte)voteList.Count);
                foreach (Pair<object, int> vote in voteList)
                {
                    msg.Write((byte)vote.Second);
                    msg.Write(((Submarine)vote.First).Name);
                }
            }
            msg.Write(AllowModeVoting);
            if (allowModeVoting)
            {
                List<Pair<object, int>> voteList = GetVoteList(VoteType.Mode, GameMain.Server.ConnectedClients);
                msg.Write((byte)voteList.Count);
                foreach (Pair<object, int> vote in voteList)
                {
                    msg.Write((byte)vote.Second);
                    msg.Write(((GameModePreset)vote.First).Name);
                }
            }
            msg.Write(AllowEndVoting);
            if (AllowEndVoting)
            {
                msg.Write((byte)GameMain.Server.ConnectedClients.Count(v => v.GetVote<bool>(VoteType.EndRound)));
                msg.Write((byte)GameMain.Server.ConnectedClients.Count);
            }

            msg.Write(AllowVoteKick);

            msg.WritePadBits();
        }
    }
}
