﻿#region Usings
using System;
using System.Collections.Generic;
using Bricklayer.Client.Entities;
using Bricklayer.Client.Networking;
using Bricklayer.Client.Networking.Messages;
using Bricklayer.Client.World;
using Cyral.Extensions;
using Cyral.Extensions.Xna;
using Lidgren.Network;
using Microsoft.Xna.Framework;
#endregion

namespace Bricklayer.Server
{
    /// <summary>
    /// Handles processing of incoming messages
    /// </summary>
    public class MessageHandler
    {
        #region Fields
        private NetworkManager NetManager { get { return Server.NetManager; } }
        private List<Map> Maps { get { return Server.Maps; } set { Server.Maps = value; } }
        #endregion

        /// <summary>
        /// The process network messages such as player's joining, moving, etc
        /// </summary>
        public void ProcessNetworkMessages()
        {
            //Used to store messages
            NetIncomingMessage inc;

            while (true) //Continously check for new messages and process them
            {
                if ((inc = NetManager.ReadMessage()) != null)
                {
                    //Try and find who sent the message
                    Player sender = inc.SenderConnection == null ?
                        null : Server.PlayerFromRUI(inc.SenderConnection.RemoteUniqueIdentifier, true);

                    switch (inc.MessageType)
                    {
                        //ConnectionApproval messages are sent when a client would like to connect to the server
                        case NetIncomingMessageType.ConnectionApproval:
                            {
                                MessageTypes type = (MessageTypes)Enum.Parse(typeof(MessageTypes), inc.ReadByte().ToString()); //Find message type
                                switch (type)
                                {
                                    //If the player would like to login
                                    case MessageTypes.Login:
                                        {
                                            Program.WriteLine(string.Format("Incoming login request (RUI: {0} IP: {1})",
                                                inc.SenderConnection.RemoteUniqueIdentifier, inc.SenderEndPoint.Address),
                                                ConsoleColor.Green); //Log message

                                            //If the player isn't already connected, allow them to join
                                            if (!Server.Logins.ContainsKey(inc.SenderConnection.RemoteUniqueIdentifier))
                                            {
                                                LoginMessage login = new LoginMessage(inc);
                                                Server.Logins.Add(inc.SenderConnection.RemoteUniqueIdentifier, login);
                                                inc.SenderConnection.Approve();
                                            }
                                            else
                                            {
                                                inc.SenderConnection.Deny();
                                            }
                                            break;
                                        }
                                }
                                break;
                            }
                        //Data messages are all messages manually sent from client
                        //These are the bulk of the messages, used for player movement, block placing, etc
                        case NetIncomingMessageType.Data:
                            {
                                ProcessDataMessage(inc);
                                break;
                            }
                        //StatusChanged messages occur when a client connects, disconnects, is approved, etc
                        //NOTE: Disconnecting and Disconnected are not instant unless client is shutdown with Disconnect()
                        case NetIncomingMessageType.StatusChanged:
                            {
                                string name = Server.Logins.ContainsKey(inc.SenderConnection.RemoteUniqueIdentifier) ?
                                    Server.Logins[inc.SenderConnection.RemoteUniqueIdentifier].Username + " (" + inc.SenderConnection.RemoteUniqueIdentifier + ")" :
                                    "Unknown (" + inc.SenderConnection.RemoteUniqueIdentifier + ")";

                                Program.Write(string.Format("{0} status changed: ", name));
                                Program.WriteLine(inc.SenderConnection.Status.ToString(), ConsoleColor.Cyan);

                                //When a players connection is finalized
                                if (inc.SenderConnection.Status == NetConnectionStatus.Connected)
                                {
                                    LoginMessage login = Server.Logins[inc.SenderConnection.RemoteUniqueIdentifier];
                                    Program.WriteLine("\tUsername: " + login.Username);
                                    Program.WriteLine("\tRemote Unique Identifier: " + inc.SenderConnection.RemoteUniqueIdentifier);
                                    Program.WriteLine("\tIP: " + inc.SenderEndPoint.Address);
                                    break;
                                }
                                //When a client disconnects
                                if (inc.SenderConnection.Status == NetConnectionStatus.Disconnected || inc.SenderConnection.Status == NetConnectionStatus.Disconnecting)
                                {
                                    if (sender != null)
                                    {
                                        Program.WriteLine(sender.Username + " has left room: " + sender.Map.Name + " (disconnected).", ConsoleColor.Red);
                                        if (sender.Map.Players.Contains(sender))
                                        {
                                            //Remove player
                                            sender.Map.Players.Remove(sender);
                                            RebuildIndexes(sender.Map);
                                            //Send to players
                                            NetManager.Broadcast(sender.Map, new PlayerLeaveMessage(sender.ID));
                                            sender = null;
                                        }
                                    }
                                    else
                                    {
                                        Program.WriteLine(Server.Logins[inc.SenderConnection.RemoteUniqueIdentifier].Username + " has left lobby (disconnected).", ConsoleColor.Red);
                                    }
                                    Server.Logins.Remove(inc.SenderConnection.RemoteUniqueIdentifier);
                                }
                                break;
                            }
                        default:
                            break;
                    }
                }
                //Let other programs have a bit of cpu time
                System.Threading.Thread.Sleep(Server.Config.Sleep);
            }
        }
        /// <summary>
        /// Handles all actions for recieving data messages (Such as movement, block placing, etc)
        /// </summary>
        /// <param name="inc">The incoming message</param>
        private void ProcessDataMessage(NetIncomingMessage inc)
        {
            Player sender = Server.PlayerFromRUI(inc.SenderConnection.RemoteUniqueIdentifier, true);
            Map map = null;
            if (sender != null)
                map = sender.Map;
            MessageTypes type = (MessageTypes)Enum.Parse(typeof(MessageTypes), inc.ReadByte().ToString());
            switch (type)
            {
                //When a player request to recieve a message
                case MessageTypes.Request:
                    {
                        MessageTypes request = new RequestMessage(inc).RequestType;

                        //Request to recieve lobby data
                        if (request == MessageTypes.Lobby)
                        {
                            List<LobbySaveData> rooms = new List<LobbySaveData>();
                            foreach (Map m in Maps)
                            {
                                rooms.Add(LobbySaveData.FromMap(m));
                            }
                            LobbyMessage msg = new LobbyMessage(Server.Config.Name, Server.Config.Decription, Server.Config.Intro, NetworkManager.NetServer.ConnectionsCount, rooms);
                            NetManager.Send(msg, inc.SenderConnection);
                        }
                        break;
                    }
                //When a player exits to the lobby
                case MessageTypes.PlayerLeave:
                    {
                        PlayerLeaveMessage user = new PlayerLeaveMessage(inc);
                        user.ID = sender.ID;
                        Program.WriteLine(sender.Username + " has left room: " + sender.Map.Name + " (Exited to lobby)", ConsoleColor.Magenta);
                        //Remove player
                        map.Players.Remove(sender);
                        RebuildIndexes(map);
                        //Send to players
                        NetManager.Broadcast(sender.Map, new PlayerLeaveMessage(sender.ID));
                        sender = null;
                        break;
                    }
                //When a player moves
                case MessageTypes.PlayerStatus:
                    {
                        PlayerStateMessage state = new PlayerStateMessage(inc);
                        state.ID = sender.ID;
                        //Clamp position in bounds
                        state.Position = new Point((int)MathHelper.Clamp(state.Position.X, Tile.Width, (map.Width * Tile.Width) - (Tile.Width * 2)), (int)MathHelper.Clamp(state.Position.Y, Tile.Height, (map.Height * Tile.Height) - (Tile.Height * 2)));
                        sender.SimulationState.Position = state.Position.ToVector2();
                        sender.SimulationState.Velocity = state.Velocity.ToVector2();
                        sender.SimulationState.Movement = state.Movement.ToVector2();
                        sender.IsJumping = state.IsJumping;

                        NetManager.BroadcastExcept(state, sender);
                        break;
                    }
                //When a player places a block
                case MessageTypes.Block:
                    {
                        BlockMessage state = new BlockMessage(inc);
                        BlockType block = BlockType.FromID(state.BlockID);
                        //Verify Block (Make sure it is in bounds and it has changed, no use sending otherwise)
                        //TODO: Punish players spamming invalid data (Because official client should never send it)
                        if (map.InBounds(state.X, state.Y, state.Z) && map.Tiles[state.X, state.Y, state.Z].Block.ID != block.ID)
                        {
                            map.Tiles[state.X, state.Y, state.Z].Block = block;
                            NetManager.Broadcast(sender.Map, state);
                        }
                        break;
                    }
                //When a player sends a chat message
                case MessageTypes.Chat:
                    {
                        ChatMessage chat = new ChatMessage(inc);
                        chat.ID = sender.ID;
                        //Verify length
                        if (chat.Message.Length > ChatMessage.MaxLength)
                            chat.Message = chat.Message.Truncate(ChatMessage.MaxLength);
                        NetManager.BroadcastExcept(chat, sender);
                        break;
                    }
                //When a player changes smiley
                case MessageTypes.PlayerSmiley:
                    {
                        PlayerSmileyMessage smiley = new PlayerSmileyMessage(inc);
                        smiley.ID = sender.ID;
                        if (sender.Smiley != smiley.Smiley)
                        {
                            sender.Smiley = smiley.Smiley;
                            NetManager.BroadcastExcept(smiley, sender);
                        }
                        break;
                    }
                //When a player changes mode (Ex: godmode to normal
                case MessageTypes.PlayerMode:
                    {
                        PlayerModeMessage mode = new PlayerModeMessage(inc);
                        mode.ID = sender.ID;
                        if (sender.Mode != mode.Mode)
                        {
                            sender.Mode = mode.Mode;
                            NetManager.BroadcastExcept(mode, sender);
                        }
                        break;
                    }

                //When a player requests to join a room
                case MessageTypes.JoinRoom:
                    {
                        if (sender == null) //If the sender isn't null, then they are already in a room
                        {
                            JoinRoomMessage msg = new JoinRoomMessage(inc);
                            int newMap = Maps.IndexOf(Server.MapFromID(msg.ID));
                            LoginMessage login = Server.Logins[inc.SenderConnection.RemoteUniqueIdentifier]; //Fetch stored login from dictionary
                            Maps[newMap].Players.Add(new Player(Maps[newMap], Maps[newMap].Spawn, login.Username, inc.SenderConnection.RemoteUniqueIdentifier, Server.FindEmptyID(Maps[newMap])) { Tint = login.Color });
                            sender = Server.PlayerFromRUI(inc.SenderConnection.RemoteUniqueIdentifier, true);
                            NetManager.Send(new InitMessage(sender.Map), sender);
                            //Send message to player notifing he is connected and ready
                            NetManager.Send(new PlayerJoinMessage(sender.Username, sender.ID, true, sender.Tint), sender);

                            //Log message
                            Program.WriteLine(string.Format("{0} joined room: {1}", login.Username, Maps[newMap].Name), ConsoleColor.Magenta);
                            //Send message to everyone notifying of new user
                            NetManager.BroadcastExcept(new PlayerJoinMessage(sender.Username, sender.ID, false, sender.Tint), sender);
                            //Let new player know of all existing players and their states (Mode, Position, Smiley)
                            foreach (Player player in sender.Map.Players)
                            {
                                if (player.ID != sender.ID)
                                {
                                    NetManager.Send(new PlayerJoinMessage(player.Username, player.ID, false, player.Tint), sender);
                                    NetManager.Send(new PlayerStateMessage(player), sender);
                                    if (player.Mode != PlayerMode.Normal)
                                        NetManager.Send(new PlayerModeMessage(player), sender);
                                    if (player.Smiley != SmileyType.Default)
                                        NetManager.Send(new PlayerSmileyMessage(player, player.Smiley), sender);
                                }
                            }
                            Program.WriteLine(string.Format("\tMap ID/Player Index: {0}/{1}", sender.ID, sender.Index));
                        }
                        break;
                    }
                //When a player requests to make a room
                case MessageTypes.CreateRoom: //If the sender isn't null, then they are already in a room
                    {
                        if (sender == null)
                        {
                            CreateRoomMessage msg = new CreateRoomMessage(inc);
                            Map newMap = Server.CreateMap(msg.Name, msg.Description);
                            LoginMessage login = Server.Logins[inc.SenderConnection.RemoteUniqueIdentifier]; //Fetch stored login from dictionary
                            newMap.Players.Add(new Player(newMap, newMap.Spawn, login.Username, inc.SenderConnection.RemoteUniqueIdentifier, Server.FindEmptyID(newMap)) { Tint = login.Color });
                            sender = Server.PlayerFromRUI(inc.SenderConnection.RemoteUniqueIdentifier, true);
                            //Send message to player notifing he is connected and ready
                            NetManager.Send(new InitMessage(sender.Map), sender);
                            NetManager.Send(new PlayerJoinMessage(sender.Username, sender.ID, true, sender.Tint), sender);
                            //Log message
                            Program.WriteLine(string.Format("{0} created room: {1}", login.Username, newMap.Name), ConsoleColor.Magenta);
                            Program.WriteLine(string.Format("\tMap ID/Player Index: {0}/{1}", sender.ID, sender.Index));
                        }
                        break;
                    }
            }
        }
        /// <summary>
        /// Rebuilds the indexes of all players, by changing their index property to the correct index in the map's player list
        /// </summary>
        private static void RebuildIndexes(Map map)
        {
            for (int i = 0; i < map.Players.Count; i++)
                map.Players[i].Index = i;
        }
    }
}