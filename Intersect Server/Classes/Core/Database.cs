﻿/*
    Intersect Game Engine (Server)
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
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Security.Cryptography;
using System.Text;
using Intersect_Server.Classes.Game_Objects;
using Intersect_Server.Classes.General;
using Intersect_Server.Classes.Maps;
using Intersect_Server.Classes.Networking;

namespace Intersect_Server.Classes
{
    public static class Database
    {
        public static object MapGridLock = new Object();
        public static List<MapGrid> MapGrids = new List<MapGrid>();
        public static string ConnectionString = "";
        public static MapList MapStructure = new MapList();

        public static List<string> Emails = new List<string>();
        public static List<string> Accounts = new List<string>();
        public static List<string> Characters = new List<string>();

        private enum MySqlFields
        {
            m_string = 0,
            m_int
        }

        //Check Directories
        public static void CheckDirectories()
        {
            if (!Directory.Exists("resources")) { Directory.CreateDirectory("resources"); }
            if (!Directory.Exists("resources/accounts")) { Directory.CreateDirectory("resources/accounts"); }
        }

        //Players General
        public static void LoadPlayerDatabase()
        {
            Console.WriteLine("Using local file system for account database.");
            LoadAccounts();
        }
        public static Client GetPlayerClient(string username)
        {
            //Try to fetch a player entity by username, online or offline.
            //Check Online First
            for (int i = 0; i < Globals.Clients.Count; i++)
            {
                if (Globals.Clients[i] != null && Globals.Clients[i].IsConnected() && Globals.Clients[i].Entity != null)
                {
                    if (Globals.Clients[i].MyAccount == username) { return Globals.Clients[i]; }
                }
            }

            //Didn't find the player online, lets load him from our database.
            Client fakeClient = new Client(-1, -1, null);
            Player en = new Player(-1, fakeClient);
            fakeClient.Entity = en;
            fakeClient.MyAccount = username;
            LoadPlayer(fakeClient);
            return fakeClient;
        }
        public static void SetPlayerPower(string username, int power)
        {
            if (AccountExists(username))
            {
                Client player = GetPlayerClient(username);
                player.Power = power;
                SavePlayer(player);
                if (player.ClientIndex > -1)
                {
                    PacketSender.SendPlayerMsg(player, "Your power has been modified!");
                }
                Console.WriteLine(username + "'s power has been set to " + power + "!");
            }
            else
            {
                Console.WriteLine("Account does not exist!");
            }
        }

        //Players_XML
        public static void LoadAccounts()
        {
            string[] accounts = Directory.GetDirectories("resources\\accounts");
            for (int i = 0; i < accounts.Length; i++)
            {
                if (File.Exists(accounts[i] + "\\" + accounts[i].Replace("resources\\accounts", "") + ".xml"))
                {
                    var playerdata = new XmlDocument();
                    playerdata.Load(accounts[i] + "\\" + accounts[i].Replace("resources\\accounts", "") + ".xml");
                    Accounts.Add(playerdata.SelectSingleNode("//PlayerData/Username").InnerText);
                    Emails.Add(playerdata.SelectSingleNode("//PlayerData/Email").InnerText);
                    if (playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Name") != null)
                        Characters.Add(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Name").InnerText);
                }
                else
                {
                    Directory.Delete(accounts[i], true);
                }
            }
        }
        public static bool AccountExists(string accountname)
        {
            if (Accounts.IndexOf(accountname) > -1) { return true; }
            return false;
        }
        public static bool EmailInUse(string email)
        {
            if (Emails.IndexOf(email) > -1) { return true; }
            return false;
        }
        public static bool CharacterNameInUse(string name)
        {
            if (Characters.IndexOf(name) > -1) { return true; }
            return false;
        }
        public static int GetRegisteredPlayers()
        {
            return Accounts.Count;
        }
        public static void CreateAccount(Client client, string username, string password, string email)
        {
            var sha = new SHA256Managed();
            Directory.CreateDirectory("resources\\accounts\\" + username);
            client.MyAccount = username;

            //Generate a Salt
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] buff = new byte[20];
            rng.GetBytes(buff);
            client.MySalt = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Convert.ToBase64String(buff)))).Replace("-", "");

            //Hash the Password
            client.MyPassword = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(password + client.MySalt))).Replace("-", "");

            client.MyEmail = email;

            if (Accounts.Count == 0) client.Power = 2;
            Accounts.Add(username);
            Emails.Add(email);
            SavePlayer(client);
        }
        public static bool CheckPassword(string username, string password)
        {
            var playerdata = new XmlDocument();
            var sha = new SHA256Managed();
            playerdata.Load("resources\\accounts\\" + username + "\\" + username + ".xml");
            string salt = playerdata.SelectSingleNode("//PlayerData/Salt").InnerText;
            string pass = playerdata.SelectSingleNode("//PlayerData/Pass").InnerText;
            string temppass = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(password + salt))).Replace("-", "");
            if (temppass == pass) { return true; }
            return false;
        }
        public static bool LoadPlayer(Client client)
        {
            var en = client.Entity;
            try
            {
                var playerdata = new XmlDocument();
                playerdata.Load("resources\\accounts\\" + client.MyAccount + "\\" + client.MyAccount + ".xml");
                client.MyEmail = playerdata.SelectSingleNode("//PlayerData/Email").InnerText;
                client.MySalt = playerdata.SelectSingleNode("//PlayerData/Salt").InnerText;
                client.MyPassword = playerdata.SelectSingleNode("//PlayerData/Pass").InnerText;
                client.Power = Int32.Parse(playerdata.SelectSingleNode("//PlayerData/Power").InnerText);

                if (playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Name") != null && !string.IsNullOrEmpty(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Name").InnerText))
                {
                    en.MyName = playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Name").InnerText;
                    en.CurrentMap = Int32.Parse(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Map").InnerText);
                    en.CurrentX = Int32.Parse(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/X").InnerText);
                    en.CurrentY = Int32.Parse(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Y").InnerText);
                    en.CurrentZ = Int32.Parse(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Z").InnerText);
                    en.Dir = Int32.Parse(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Dir").InnerText);
                    en.MySprite = playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Sprite").InnerText;
                    en.Class = Int32.Parse(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Class").InnerText);
                    en.Gender = Int32.Parse(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Gender").InnerText);
                    en.Level = Int32.Parse(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Level").InnerText);
                    en.Experience =
                        Int32.Parse(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Experience").InnerText);
                    for (var i = 0; i < (int) Enums.Vitals.VitalCount; i++)
                    {
                        en.Vital[i] =
                            Int32.Parse(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Vital" + i).InnerText);
                        en.MaxVital[i] =
                            Int32.Parse(
                                playerdata.SelectSingleNode("//PlayerData//CharacterInfo/MaxVital" + i).InnerText);
                    }
                    for (var i = 0; i < (int) Enums.Stats.StatCount; i++)
                    {
                        en.Stat[i].Stat =
                            Int32.Parse(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Stat" + i).InnerText);
                    }
                    en.StatPoints =
                        Int32.Parse(playerdata.SelectSingleNode("//PlayerData//CharacterInfo/StatPoints").InnerText);
                    for (var i = 0; i < Options.EquipmentSlots.Count; i++)
                    {
                        en.Equipment[i] =
                            Int32.Parse(
                                playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Equipment" + i).InnerText);
                    }
                    en.Face = playerdata.SelectSingleNode("//PlayerData//CharacterInfo/Face").InnerText;

                    for (int i = 0; i < Options.MaxPlayerSwitches; i++)
                    {
                        if (playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Switches/Switch" + i) != null)
                            ((Player) (en)).Switches[i] =
                                Convert.ToBoolean(
                                    Int32.Parse(
                                        playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Switches/Switch" + i)
                                            .InnerText));
                    }

                    for (int i = 0; i < Options.MaxPlayerSwitches; i++)
                    {
                        if (playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Variables/Variable" + i) != null)
                            ((Player) (en)).Variables[i] =
                                Int32.Parse(
                                    playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Variables/Variable" + i)
                                        .InnerText);
                    }

                    for (int i = 0; i < Options.MaxInvItems; i++)
                    {
                        en.Inventory[i].ItemNum =
                            Int32.Parse(
                                playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Inventory/Slot" + i + "Num")
                                    .InnerText);
                        en.Inventory[i].ItemVal =
                            Int32.Parse(
                                playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Inventory/Slot" + i + "Val")
                                    .InnerText);
                        for (int x = 0; x < (int) Enums.Stats.StatCount; x++)
                        {
                            en.Inventory[i].StatBoost[x] =
                                Int32.Parse(
                                    playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Inventory/Slot" + i +
                                                                "Buff" + x).InnerText);
                        }
                    }

                    for (int i = 0; i < Options.MaxPlayerSkills; i++)
                    {
                        en.Spells[i].SpellNum =
                            Int32.Parse(
                                playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Spells/Slot" + i + "Num")
                                    .InnerText);
                    }

                    for (int i = 0; i < Options.MaxHotbar; i++)
                    {
                        en.Hotbar[i].Type =
                            Int32.Parse(
                                playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Hotbar/Slot" + i + "Type")
                                    .InnerText);
                        en.Hotbar[i].Slot =
                            Int32.Parse(
                                playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Hotbar/Slot" + i + "Slot")
                                    .InnerText);
                    }

                    for (int i = 0; i < Options.MaxBankSlots; i++)
                    {
                        if (playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Bank/Slot" + i + "Num") != null)
                        {
                            int itemNum =
                                Int32.Parse(
                                    playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Bank/Slot" + i + "Num")
                                        .InnerText);
                            int itemVal =
                                Int32.Parse(
                                    playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Bank/Slot" + i + "Val")
                                        .InnerText);
                            if (itemNum < 0 || itemVal == 0)
                            {
                                en.Bank[i] = null;
                            }
                            else
                            {
                                en.Bank[i] = new ItemInstance(itemNum, itemVal);
                                for (int x = 0; x < (int) Enums.Stats.StatCount; x++)
                                {
                                    en.Bank[i].StatBoost[x] =
                                        Int32.Parse(
                                            playerdata.SelectSingleNode("//PlayerData//CharacterInfo//Bank/Slot" + i +
                                                                        "Buff" + x).InnerText);
                                }
                            }
                        }
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public static void SavePlayer(Client client)
        {
            if (client == null) { return; }

            var en = (Player)client.Entity;

            var playerdata = new XmlWriterSettings { Indent = true };
            playerdata.ConformanceLevel = ConformanceLevel.Auto;
            var writer = XmlWriter.Create("resources\\accounts\\" + client.MyAccount + "\\" + client.MyAccount + ".xml", playerdata);
            writer.WriteStartDocument();
            writer.WriteStartElement("PlayerData");
            writer.WriteElementString("Username", client.MyAccount);
            writer.WriteElementString("Email", client.MyEmail);
            writer.WriteElementString("Pass", client.MyPassword);
            writer.WriteElementString("Salt", client.MySalt);
            writer.WriteElementString("Power", client.Power.ToString());

            if (en != null && en.MyName != "")
            {
                writer.WriteStartElement("CharacterInfo");
                writer.WriteElementString("Name", en.MyName);
                writer.WriteElementString("Map", en.CurrentMap.ToString());
                writer.WriteElementString("X", en.CurrentX.ToString());
                writer.WriteElementString("Y", en.CurrentY.ToString());
                writer.WriteElementString("Z", en.CurrentZ.ToString());
                writer.WriteElementString("Dir", en.Dir.ToString());
                writer.WriteElementString("Sprite", en.MySprite);
                writer.WriteElementString("Class", en.Class.ToString());
                writer.WriteElementString("Gender", en.Gender.ToString());
                writer.WriteElementString("Level", en.Level.ToString());
                writer.WriteElementString("Experience", en.Experience.ToString());
                for (var i = 0; i < (int) Enums.Vitals.VitalCount; i++)
                {
                    writer.WriteElementString("Vital" + i, en.Vital[i].ToString());
                    writer.WriteElementString("MaxVital" + i, en.MaxVital[i].ToString());
                }
                for (var i = 0; i < (int) Enums.Stats.StatCount; i++)
                {
                    writer.WriteElementString("Stat" + i, en.Stat[i].Stat.ToString());
                }
                writer.WriteElementString("StatPoints", en.StatPoints.ToString());
                for (var i = 0; i < Options.EquipmentSlots.Count; i++)
                {
                    writer.WriteElementString("Equipment" + i, en.Equipment[i].ToString());
                }
                writer.WriteElementString("Face", en.Face);

                writer.WriteStartElement("Switches");
                for (int i = 0; i < Options.MaxPlayerSwitches; i++)
                {
                    writer.WriteElementString("Switch" + i, Convert.ToInt32(((Player) (en)).Switches[i]).ToString());
                }
                writer.WriteEndElement();

                writer.WriteStartElement("Variables");
                for (int i = 0; i < Options.MaxPlayerVariables; i++)
                {
                    writer.WriteElementString("Variable" + i, ((Player) (en)).Variables[i].ToString());
                }
                writer.WriteEndElement();

                writer.WriteStartElement("Inventory");
                for (int i = 0; i < Options.MaxInvItems; i++)
                {
                    writer.WriteElementString("Slot" + i + "Num", en.Inventory[i].ItemNum.ToString());
                    writer.WriteElementString("Slot" + i + "Val", en.Inventory[i].ItemVal.ToString());
                    for (int x = 0; x < (int) Enums.Stats.StatCount; x++)
                    {
                        writer.WriteElementString("Slot" + i + "Buff" + x, en.Inventory[i].StatBoost[x].ToString());
                    }

                }
                writer.WriteEndElement();

                writer.WriteStartElement("Spells");
                for (int i = 0; i < Options.MaxPlayerSkills; i++)
                {
                    writer.WriteElementString("Slot" + i + "Num", en.Spells[i].SpellNum.ToString());
                }
                writer.WriteEndElement();


                writer.WriteStartElement("Hotbar");
                for (int i = 0; i < Options.MaxHotbar; i++)
                {
                    writer.WriteElementString("Slot" + i + "Type", en.Hotbar[i].Type.ToString());
                    writer.WriteElementString("Slot" + i + "Slot", en.Hotbar[i].Slot.ToString());
                }
                writer.WriteEndElement();

                writer.WriteStartElement("Bank");
                for (int i = 0; i < Options.MaxBankSlots; i++)
                {
                    if (en.Bank[i] != null)
                    {
                        writer.WriteElementString("Slot" + i + "Num", en.Bank[i].ItemNum.ToString());
                        writer.WriteElementString("Slot" + i + "Val", en.Bank[i].ItemVal.ToString());
                        for (int x = 0; x < (int) Enums.Stats.StatCount; x++)
                        {
                            writer.WriteElementString("Slot" + i + "Buff" + x, en.Bank[i].StatBoost[x].ToString());
                        }
                    }
                    else
                    {
                        writer.WriteElementString("Slot" + i + "Num", "-1");
                        writer.WriteElementString("Slot" + i + "Val", "0");
                        for (int x = 0; x < (int) Enums.Stats.StatCount; x++)
                        {
                            writer.WriteElementString("Slot" + i + "Buff" + x, "0");
                        }
                    }
                }
                writer.WriteEndElement();



                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Flush();
            writer.Close();
        }
        public static int CheckPower(string username)
        {
            var playerdata = new XmlDocument();
            var sha = new SHA256Managed();
            playerdata.Load("resources\\accounts\\" + username + "\\" + username + ".xml");
            int power = Int32.Parse(playerdata.SelectSingleNode("//PlayerData/Power").InnerText);
            return power;
        }

        //Maps
        public static void LoadMaps()
        {
            if (!Directory.Exists("Resources/Maps"))
            {
                Directory.CreateDirectory("Resources/Maps");
            }
            var mapNames = Directory.GetFiles("Resources/Maps", "*.map");
            Globals.MapCount = mapNames.Length;
            Globals.GameMaps = new MapStruct[mapNames.Length];
            if (Globals.MapCount == 0)
            {
                Console.WriteLine("No maps found! - Creating empty first map!");
                Globals.MapCount = 1;
                Globals.GameMaps = new MapStruct[1];
                Globals.GameMaps[0] = new MapStruct(0);
                Globals.GameMaps[0].Save(true);
            }
            else
            {
                for (var i = 0; i < mapNames.Length; i++)
                {
                    Globals.GameMaps[i] = new MapStruct(i);
                    if (!Globals.GameMaps[i].Load(File.ReadAllBytes("Resources/Maps/" + i + ".map")))
                        Globals.GameMaps[i] = null;

                }
            }
            GenerateMapGrids();
            LoadMapFolders();
            CheckAllMapConnections();
        }
        public static void CheckAllMapConnections()
        {
            for (int i = 0; i < Globals.GameMaps.Length; i++)
            {
                if (MapHelper.IsMapValid(i))
                {
                    CheckMapConnections(i);
                }
            }
        }
        public static void CheckMapConnections(int mapNum)
        {
            bool updated = false;
            if (!MapHelper.IsMapValid(Globals.GameMaps[mapNum].Up)) { Globals.GameMaps[mapNum].Up = -1; updated = true; }
            if (!MapHelper.IsMapValid(Globals.GameMaps[mapNum].Down)) { Globals.GameMaps[mapNum].Down = -1; updated = true; }
            if (!MapHelper.IsMapValid(Globals.GameMaps[mapNum].Left)) { Globals.GameMaps[mapNum].Left = -1; updated = true; }
            if (!MapHelper.IsMapValid(Globals.GameMaps[mapNum].Right)) { Globals.GameMaps[mapNum].Right = -1; updated = true; }
            if (updated)
            {
                Globals.GameMaps[mapNum].Save();
                PacketSender.SendMapToEditors(mapNum);
            }
        }
        public static void GenerateMapGrids()
        {
            lock (MapGridLock)
            {
                MapGrids.Clear();
                for (var i = 0; i < Globals.MapCount; i++)
                {
                    if (Globals.GameMaps[i] == null) continue;
                    if (!MapGrids.Any())
                    {
                        MapGrids.Add(new MapGrid(i, 0));
                    }
                    else
                    {
                        for (var y = 0; y < MapGrids.Count(); y++)
                        {
                            if (!MapGrids[y].HasMap(i))
                            {
                                if (y != MapGrids.Count() - 1) continue;
                                MapGrids.Add(new MapGrid(i, MapGrids.Count()));
                                break;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
                for (var i = 0; i < Globals.MapCount; i++)
                {
                    if (Globals.GameMaps[i] == null) continue;
                    Globals.GameMaps[i].SurroundingMaps.Clear();
                    var myGrid = Globals.GameMaps[i].MapGrid;
                    for (var x = Globals.GameMaps[i].MapGridX - 1; x <= Globals.GameMaps[i].MapGridX + 1; x++)
                    {
                        for (var y = Globals.GameMaps[i].MapGridY - 1; y <= Globals.GameMaps[i].MapGridY + 1; y++)
                        {
                            if ((x == Globals.GameMaps[i].MapGridX) && (y == Globals.GameMaps[i].MapGridY))
                                continue;
                            if (x >= MapGrids[myGrid].XMin && x < MapGrids[myGrid].XMax && y >= MapGrids[myGrid].YMin &&
                                y < MapGrids[myGrid].YMax && MapGrids[myGrid].MyGrid[x, y] > -1)
                            {
                                Globals.GameMaps[i].SurroundingMaps.Add(MapGrids[myGrid].MyGrid[x, y]);
                            }
                        }
                    }
                }
            }
        }
        public static int AddMap()
        {
            var tmpMaps = (MapStruct[])Globals.GameMaps.Clone();
            Globals.MapCount++;
            Globals.GameMaps = new MapStruct[Globals.MapCount];
            tmpMaps.CopyTo(Globals.GameMaps, 0);
            Globals.GameMaps[Globals.MapCount - 1] = new MapStruct(Globals.MapCount - 1);
            Globals.GameMaps[Globals.MapCount - 1].Save(true);
            return Globals.MapCount - 1;
        }
        public static void LoadNpcs()
        {
            if (!Directory.Exists("Resources/Npcs"))
            {
                Directory.CreateDirectory("Resources/Npcs");
            }
            Globals.GameNpcs = new NpcStruct[Options.MaxNpcs];
            for (var i = 0; i < Options.MaxNpcs; i++)
            {
                Globals.GameNpcs[i] = new NpcStruct();
                if (!File.Exists("Resources/Npcs/" + i + ".npc"))
                {
                    Globals.GameNpcs[i].Save(i);
                }
                else
                {
                    Globals.GameNpcs[i].Load(File.ReadAllBytes("Resources/Npcs/" + i + ".npc"), i);
                }

            }
        }

        //Items
        public static void LoadItems()
        {
            if (!Directory.Exists("Resources/Items"))
            {
                Directory.CreateDirectory("Resources/Items");
            }

            Globals.GameItems = new ItemStruct[Options.MaxItems];
            for (var i = 0; i < Options.MaxItems; i++)
            {
                Globals.GameItems[i] = new ItemStruct();
                if (!File.Exists("Resources/Items/" + i + ".item"))
                {
                    Globals.GameItems[i].Save(i);
                }
                else
                {
                    Globals.GameItems[i].Load(File.ReadAllBytes("Resources/Items/" + i + ".item"), i);
                }
            }
        }

        //Shops
        public static void LoadShops()
        {
            if (!Directory.Exists("Resources/Shops"))
            {
                Directory.CreateDirectory("Resources/Shops");
            }

            Globals.GameShops = new ShopStruct[Options.MaxShops];
            for (var i = 0; i < Options.MaxShops; i++)
            {
                Globals.GameShops[i] = new ShopStruct();
                if (!File.Exists("Resources/Shops/" + i + ".shop"))
                {
                    Globals.GameShops[i].Save(i);
                }
                else
                {
                    Globals.GameShops[i].Load(File.ReadAllBytes("Resources/Shops/" + i + ".shop"), i);
                }
            }
        }

        //Spells
        public static void LoadSpells()
        {
            if (!Directory.Exists("Resources/Spells"))
            {
                Directory.CreateDirectory("Resources/Spells");
            }

            Globals.GameSpells = new SpellStruct[Options.MaxSpells];
            for (var i = 0; i < Options.MaxSpells; i++)
            {
                Globals.GameSpells[i] = new SpellStruct();
                if (!File.Exists("Resources/Spells/" + i + ".spell"))
                {
                    Globals.GameSpells[i].Save(i);
                }
                else
                {
                    Globals.GameSpells[i].Load(File.ReadAllBytes("Resources/Spells/" + i + ".spell"), i);
                }
            }
        }

        //Animations
        public static void LoadAnimations()
        {
            if (!Directory.Exists("Resources/Animations"))
            {
                Directory.CreateDirectory("Resources/Animations");
            }

            Globals.GameAnimations = new AnimationStruct[Options.MaxAnimations];
            for (var i = 0; i < Options.MaxAnimations; i++)
            {
                Globals.GameAnimations[i] = new AnimationStruct();
                if (!File.Exists("Resources/Animations/" + i + ".anim"))
                {
                    Globals.GameAnimations[i].Save(i);
                }
                else
                {
                    Globals.GameAnimations[i].Load(File.ReadAllBytes("Resources/Animations/" + i + ".anim"), i);
                }
            }
        }

        // Resources
        public static void LoadResources()
        {
            if (!Directory.Exists("Resources/Resources"))
            {
                Directory.CreateDirectory("Resources/Resources");
            }
            Globals.GameResources = new ResourceStruct[Options.MaxResources];
            for (var i = 0; i < Options.MaxResources; i++)
            {
                Globals.GameResources[i] = new ResourceStruct();
                if (!File.Exists("Resources/Resources/" + i + ".res"))
                {
                    Globals.GameResources[i].Save(i);
                }
                else
                {
                    Globals.GameResources[i].Load(File.ReadAllBytes("Resources/Resources/" + i + ".res"), i);
                }

            }
        }

        // Quests
        public static void LoadQuests()
        {
            if (!Directory.Exists("Resources/Quests"))
            {
                Directory.CreateDirectory("Resources/Quests");
            }
            Globals.GameQuests = new QuestStruct[Options.MaxQuests];
            for (var i = 0; i < Options.MaxQuests; i++)
            {
                Globals.GameQuests[i] = new QuestStruct();
                if (!File.Exists("Resources/Quests/" + i + ".qst"))
                {
                    Globals.GameQuests[i].Save(i);
                }
                else
                {
                    Globals.GameQuests[i].Load(File.ReadAllBytes("Resources/Quests/" + i + ".qst"), i);
                }

            }
        }

        // Projectiles
        public static void LoadProjectiles()
        {
            if (!Directory.Exists("Resources/Projectiles"))
            {
                Directory.CreateDirectory("Resources/Projectiles");
            }
            Globals.GameProjectiles = new ProjectileStruct[Options.MaxProjectiles];
            for (var i = 0; i < Options.MaxProjectiles; i++)
            {
                Globals.GameProjectiles[i] = new ProjectileStruct();
                if (!File.Exists("Resources/Projectiles/" + i + ".prj"))
                {
                    Globals.GameProjectiles[i].Save(i);
                }
                else
                {
                    Globals.GameProjectiles[i].Load(File.ReadAllBytes("Resources/Projectiles/" + i + ".prj"), i);
                }

            }
        }

        // Classes
        public static int LoadClasses()
        {
            int x = 0;
            if (!Directory.Exists("Resources/Classes"))
            {
                Directory.CreateDirectory("Resources/Classes");
            }
            Globals.GameClasses = new ClassStruct[Options.MaxClasses];
            for (var i = 0; i < Options.MaxClasses; i++)
            {
                Globals.GameClasses[i] = new ClassStruct();
                if (!File.Exists("Resources/Classes/" + i + ".cls"))
                {
                    Globals.GameClasses[i].Save(i);
                }
                else
                {
                    Globals.GameClasses[i].Load(File.ReadAllBytes("Resources/Classes/" + i + ".cls"), i);
                }
                if (String.IsNullOrEmpty(Globals.GameClasses[i].Name)) { x++; }
            }
            return x;
        }
        public static void CreateDefaultClass()
        {
            Globals.GameClasses[0].Name = "Default";
            ClassSprite defaultMale = new ClassSprite();
            defaultMale.Sprite = "1.png";
            defaultMale.Gender = 0;
            ClassSprite defaultFemale = new ClassSprite();
            defaultFemale.Sprite = "2.png";
            defaultFemale.Gender = 1;
            Globals.GameClasses[0].Sprites.Add(defaultMale);
            Globals.GameClasses[0].Sprites.Add(defaultFemale);
            for (int i = 0; i < (int)Enums.Vitals.VitalCount; i++)
            {
                Globals.GameClasses[0].MaxVital[i] = 20;
            }
            for (int i = 0; i < (int)Enums.Stats.StatCount; i++)
            {
                Globals.GameClasses[0].Stat[i] = 20;
            }
            Globals.GameClasses[0].Save(0);
        }

        //Map Folders
        private static void LoadMapFolders()
        {
            if (File.Exists("Resources/Maps/MapStructure.dat"))
            {
                ByteBuffer myBuffer = new ByteBuffer();
                myBuffer.WriteBytes(File.ReadAllBytes("Resources/Maps/MapStructure.dat"));
                MapStructure.Load(myBuffer);
                for (int i = 0; i < Globals.GameMaps.Length; i++)
                {
                    if (MapHelper.IsMapValid(i))
                    {
                        if (MapStructure.FindMap(i) == null)
                        {
                            MapStructure.AddMap(i);
                        }
                    }
                }
                File.WriteAllBytes("Resources/Maps/MapStructure.dat", MapStructure.Data());
                PacketSender.SendMapListToEditors();
            }
            else
            {
                for (int i = 0; i < Globals.GameMaps.Length; i++)
                {
                    if (MapHelper.IsMapValid(i))
                    {
                        MapStructure.AddMap(i);
                    }
                }
                File.WriteAllBytes("Resources/Maps/MapStructure.dat", MapStructure.Data());
            }
        }

        //Common Events
        public static void LoadCommonEvents()
        {
            if (!Directory.Exists("Resources/Common Events"))
            {
                Directory.CreateDirectory("Resources/Common Events");
            }
            Globals.CommonEvents = new EventStruct[Options.MaxCommonEvents];
            for (var i = 0; i < Options.MaxCommonEvents; i++)
            {
                Globals.CommonEvents[i] = new EventStruct(i, -1, -1, true);
                if (!File.Exists("Resources/Common Events/" + i + ".evt"))
                {
                    File.WriteAllBytes("Resources/Common Events/" + i + ".evt", Globals.CommonEvents[i].EventData());
                }
                else
                {
                    ByteBuffer bf = new ByteBuffer();
                    bf.WriteBytes(File.ReadAllBytes("Resources/Common Events/" + i + ".evt"));
                    Globals.CommonEvents[i] = new EventStruct(i, bf, true);
                }

            }
        }

        //Switches and Variables
        public static void LoadSwitchesAndVariables()
        {
            //Gonna do xml
            Globals.ServerSwitches = new string[Options.MaxServerSwitches];
            Globals.ServerVariables = new string[Options.MaxServerVariables];
            Globals.ServerSwitchValues = new bool[Options.MaxServerSwitches];
            Globals.ServerVariableValues = new int[Options.MaxServerVariables];
            Globals.PlayerSwitches = new string[Options.MaxPlayerSwitches];
            Globals.PlayerVariables = new string[Options.MaxPlayerVariables];
            LoadSwitchesOrVariabes(Globals.ServerSwitches, Globals.ServerSwitchValues, null, "GlobalSwitch", "ServerSwitches", Options.MaxServerSwitches);
            LoadSwitchesOrVariabes(Globals.PlayerSwitches, null, null, "Switch", "PlayerSwitches", Options.MaxPlayerSwitches);
            LoadSwitchesOrVariabes(Globals.ServerVariables, null, Globals.ServerVariableValues, "GlobalVariable", "ServerVariables", Options.MaxServerVariables);
            LoadSwitchesOrVariabes(Globals.PlayerVariables, null, null, "Variable", "PlayerVariables", Options.MaxPlayerVariables);
        }

        public static void SaveSwitchesAndVariables()
        {
            SaveSwitchesOrVariables(Globals.ServerSwitches, Globals.ServerSwitchValues, null, "GlobalSwitch", "ServerSwitches", Options.MaxServerSwitches);
            SaveSwitchesOrVariables(Globals.PlayerSwitches, null, null, "Switch", "PlayerSwitches", Options.MaxPlayerSwitches);
            SaveSwitchesOrVariables(Globals.ServerVariables, null, Globals.ServerVariableValues, "GlobalVariable", "ServerVariables", Options.MaxServerVariables);
            SaveSwitchesOrVariables(Globals.PlayerVariables, null, null, "Variable", "PlayerVariables", Options.MaxPlayerVariables);
        }
        private static void LoadSwitchesOrVariabes(string[] names, bool[] boolValues, int[] intValues, string prefix, string header, int count)
        {
            for (int i = 0; i < count; i++)
            {
                names[i] = prefix;
            }
            if (File.Exists("Resources/" + header + ".xml"))
            {
                try
                {
                    var xml = new XmlDocument();
                    xml.Load("resources\\" + header + ".xml");
                    for (int i = 0; i < Options.MaxServerSwitches; i++)
                    {

                        names[i] = xml.SelectSingleNode("//" + header + "/" + prefix + (i + 1) + "Name").InnerText;
                        if (boolValues != null)
                        {
                            boolValues[i] = Convert.ToBoolean(xml.SelectSingleNode("//" + header + "/" + prefix + (i + 1) + "Value").InnerText);
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }
            else
            {
                SaveSwitchesOrVariables(names, boolValues, intValues, prefix, header, count);
            }
        }
        public static void SaveSwitchesOrVariables(string[] names, bool[] boolValues, int[] intValues, string prefix, string header, int count)
        {
            var xml = new XmlWriterSettings { Indent = true };
            xml.ConformanceLevel = ConformanceLevel.Auto;
            var writer = XmlWriter.Create("resources\\" + header + ".xml", xml);

            writer.WriteStartDocument();
            writer.WriteStartElement(header);
            for (int i = 0; i < count; i++)
            {
                writer.WriteElementString(prefix + (i + 1) + "Name", names[i]);
                if (boolValues != null) writer.WriteElementString(prefix + (i + 1) + "Value", boolValues[i].ToString());
                if (intValues != null) writer.WriteElementString(prefix + (i + 1) + "Value", intValues[i].ToString());
            }
            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Flush();
            writer.Close();
        }
    }
}

