﻿using Lidgren.Network;
using Bricklayer.Client.World;

namespace Bricklayer.Client.Networking.Messages
{
    public class InitMessage : IMessage
    {
        public double MessageTime { get; set; }
        private Map map;

        public MessageTypes MessageType
        {
            get { return MessageTypes.Init; }
        }

        public InitMessage(Map map)
        {
            this.map = map;
        }

        public InitMessage(NetIncomingMessage im, Map map)
        {
            this.map = map;
            this.Decode(im);
        }

        public void Decode(NetIncomingMessage im)
        {
            map.Width = im.ReadInt16();
            map.Height = im.ReadInt16();
            map.Tiles = new Tile[map.Width, map.Height, 2];

            map.Minimap = new Minimap(map, map.Width, map.Height);
            map.Minimap.Position = new Microsoft.Xna.Framework.Vector2(16, 16);
            for (int z = 0; z < 2; z++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    byte[] b = im.ReadBytes(map.Width);
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (z == 0)
                            map.Tiles[x, y, 1] = new Tile(BlockType.BlockList[b[x]]);
                        else
                            map.Tiles[x, y, 0] = new Tile(BlockType.BlockList[b[x]]);
                    }
                }
            }
        }

        public void Encode(NetOutgoingMessage om)
        {
            //Write size of map
            om.Write((short)map.Width);
            om.Write((short)map.Height);
            //Write each layer, in rows
            for (int z = 0; z < 2; z++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    byte[] b = new byte[map.Width];

                    for (int x = 0; x < map.Width; x++)
                    {
                        if (z == 0)
                            b[x] = map.Tiles[x, y,1].Block.ID;
                        else
                            b[x] = map.Tiles[x, y,0].Block.ID;
                    }
                    om.Write(b);
                }
            }
        }
    }
}
