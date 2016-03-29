﻿/*
    Intersect Game Engine (Editor)
    Copyright (C) 2015  JC Snider, Joe Bridges
    
    Website: http://ascensiongamedev.com
    Contact Email: admin@ascensiongamedev.com 

    This program is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License along
    with this program; if not, write to the Free Software Foundation, Inc.,
    51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
*/

using System;
using Intersect_Editor.Classes.General;

namespace Intersect_Editor.Classes
{
    public class ItemStruct
    {
        public const string Version = "0.0.0.1";
        public string Name = "";
        public string Desc = "";
        public int Type;
        public string Pic = "";
        public int Price;
        public int Bound;
        public int Animation;
        public int ClassReq;
        public int LevelReq;
        public int Projectile = -1;
        public int[] StatsReq;
        public int[] StatsGiven;
        public int StatGrowth;
        public int Damage;
        public int Speed;
        public string Paperdoll = "";
        public int Tool;
        public int Data1;
        public int Data2;
        public int Data3;
        public int Data4;

        public ItemStruct()
        {
            Speed = 10; // Set to 10 by default.
            StatsGiven = new int[Options.MaxStats];
            StatsReq = new int[Options.MaxStats];
        }

        public void LoadItem(byte[] data, int index)
        {
            ByteBuffer myBuffer = new ByteBuffer();
            myBuffer.WriteBytes(data);
            string loadedVersion = myBuffer.ReadString();
            if (loadedVersion != Version)
                throw new Exception("Failed to load Item #" + index + ". Loaded Version: " + loadedVersion + " Expected Version: " + Version);
            Name = myBuffer.ReadString();
            Desc = myBuffer.ReadString();
            Type = myBuffer.ReadInteger();
            Pic = myBuffer.ReadString();
            Price = myBuffer.ReadInteger();
            Bound = myBuffer.ReadInteger();
            Animation = myBuffer.ReadInteger();
            ClassReq = myBuffer.ReadInteger();
            LevelReq = myBuffer.ReadInteger();
            Projectile = myBuffer.ReadInteger();

            for (var i =0; i < Options.MaxStats; i++)
            {
                StatsReq[i] = myBuffer.ReadInteger();
                StatsGiven[i] = myBuffer.ReadInteger();
            }

            StatGrowth = myBuffer.ReadInteger();
            Damage = myBuffer.ReadInteger();
            Speed = myBuffer.ReadInteger();
            Paperdoll = myBuffer.ReadString();
            Tool = myBuffer.ReadInteger();
            Data1 = myBuffer.ReadInteger();
            Data2 = myBuffer.ReadInteger();
            Data3 = myBuffer.ReadInteger();
            Data4 = myBuffer.ReadInteger();
        }

        public byte[] ItemData()
        {
            var myBuffer = new ByteBuffer();
            myBuffer.WriteString(Version);
            myBuffer.WriteString(Name);
            myBuffer.WriteString(Desc);
            myBuffer.WriteInteger(Type);
            myBuffer.WriteString(Pic);
            myBuffer.WriteInteger(Price);
            myBuffer.WriteInteger(Bound);
            myBuffer.WriteInteger(Animation);
            myBuffer.WriteInteger(ClassReq);
            myBuffer.WriteInteger(LevelReq);
            myBuffer.WriteInteger(Projectile);

            for (var i = 0; i < Options.MaxStats; i++)
            {
                myBuffer.WriteInteger(StatsReq[i]);
                myBuffer.WriteInteger(StatsGiven[i]);
            }

            myBuffer.WriteInteger(StatGrowth);
            myBuffer.WriteInteger(Damage);
            myBuffer.WriteInteger(Speed);
            myBuffer.WriteString(Paperdoll);
            myBuffer.WriteInteger(Tool);
            myBuffer.WriteInteger(Data1);
            myBuffer.WriteInteger(Data2);
            myBuffer.WriteInteger(Data3);
            myBuffer.WriteInteger(Data4);
            return myBuffer.ToArray();
        }
    }
}
