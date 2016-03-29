﻿/*
    The MIT License (MIT)

    Copyright (c) 2015 JC Snider, Joe Bridges
  
    Website: http://ascensiongamedev.com
    Contact Email: admin@ascensiongamedev.com

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System;
using System.Collections.Generic;
using IntersectClientExtras.GenericClasses;
using IntersectClientExtras.Graphics;
using IntersectClientExtras.Gwen;
using IntersectClientExtras.Gwen.Control;
using IntersectClientExtras.Gwen.Control.EventArguments;
using IntersectClientExtras.Gwen.Input;
using IntersectClientExtras.Input;
using Intersect_Client.Classes.Core;
using Intersect_Client.Classes.General;
using Intersect_Client.Classes.Networking;

namespace Intersect_Client.Classes.UI.Game
{
    public class InventoryWindow : IGUIElement
    {
        //Controls
        private WindowControl _inventoryWindow;
        private ScrollControl _itemContainer;

        //Location
        public int X;
        public int Y;

        //Item List
        public List<InventoryItem> Items = new List<InventoryItem>();
        private List<Label> _values = new List<Label>();

        //Init
        public InventoryWindow(Canvas _gameCanvas)
        {
            _inventoryWindow = new WindowControl(_gameCanvas, "Inventory");
            _inventoryWindow.SetSize(200, 300);
            _inventoryWindow.SetPosition(GameGraphics.Renderer.GetScreenWidth() - 210, GameGraphics.Renderer.GetScreenHeight() - 500);
            _inventoryWindow.DisableResizing();
            _inventoryWindow.Margin = Margin.Zero;
            _inventoryWindow.Padding = Padding.Zero;
            _inventoryWindow.IsHidden = true;

            _itemContainer = new ScrollControl(_inventoryWindow);
            _itemContainer.SetPosition(0, 0);
            _itemContainer.SetSize(_inventoryWindow.Width, _inventoryWindow.Height - 24);
            _itemContainer.EnableScroll(false, true);
            InitItemContainer();
        }

        //Methods
        public void Update()
        {
            if (_inventoryWindow.IsHidden == true) { return; }
            X = _inventoryWindow.X;
            Y = _inventoryWindow.Y;
            for (int i = 0; i < Options.MaxInvItems; i++)
            {
                if (Globals.Me.Inventory[i].ItemNum > -1)
                {
                    Items[i].pnl.IsHidden = false;

                    if (Globals.GameItems[Globals.Me.Inventory[i].ItemNum].Type == (int)Enums.ItemTypes.Consumable || //Allow Stacking on Currency, Consumable, Spell, and item types of none.
                        Globals.GameItems[Globals.Me.Inventory[i].ItemNum].Type == (int)Enums.ItemTypes.Currency ||
                        Globals.GameItems[Globals.Me.Inventory[i].ItemNum].Type == (int)Enums.ItemTypes.None ||
                        Globals.GameItems[Globals.Me.Inventory[i].ItemNum].Type == (int)Enums.ItemTypes.Spell)
                    {
                        _values[i].IsHidden = false;
                        _values[i].Text = Globals.Me.Inventory[i].ItemVal.ToString();
                    }
                    else
                    {
                        _values[i].IsHidden = true;
                    }

                    if (Items[i].IsDragging)
                    {
                        Items[i].pnl.IsHidden = true;
                        _values[i].IsHidden = true;
                    }
                    Items[i].Update();
                }
                else
                {
                    Items[i].pnl.IsHidden = true;
                    _values[i].IsHidden = true;
                }
            }
        }
        private void InitItemContainer()
        {

            for (int i = 0; i < Options.MaxInvItems; i++)
            {
                Items.Add(new InventoryItem(this, i));
                Items[i].pnl = new ImagePanel(_itemContainer);
                Items[i].pnl.SetSize(32, 32);
                Items[i].pnl.SetPosition((i % (_itemContainer.Width / (32 + Constants.ItemXPadding))) * (32 + Constants.ItemXPadding) + Constants.ItemXPadding, (i / (_itemContainer.Width / (32 + Constants.ItemXPadding))) * (32 + Constants.ItemYPadding) + Constants.ItemYPadding);
                Items[i].pnl.Clicked += InventoryWindow_Clicked;
                Items[i].pnl.IsHidden = true;
                Items[i].Setup();

                _values.Add(new Label(_itemContainer));
                _values[i].Text = "";
                _values[i].SetPosition((i % (_itemContainer.Width / (32 + Constants.ItemXPadding))) * (32 + Constants.ItemXPadding) + Constants.ItemXPadding, (i / (_itemContainer.Width / (32 + Constants.ItemXPadding))) * (32 + Constants.ItemYPadding) + Constants.ItemYPadding + 24);
                _values[i].TextColor = Color.Black;
                _values[i].MakeColorDark();
                _values[i].IsHidden = true;
            }
        }

        void InventoryWindow_Clicked(Base sender, ClickedEventArgs arguments)
        {

        }
        public void Show()
        {
            _inventoryWindow.IsHidden = false;
        }
        public bool IsVisible()
        {
            return !_inventoryWindow.IsHidden;
        }
        public void Hide()
        {
            _inventoryWindow.IsHidden = true;
        }
        public FloatRect RenderBounds()
        {
            FloatRect rect = new FloatRect();
            rect.X = _inventoryWindow.LocalPosToCanvas(new Point(0, 0)).X - Constants.ItemXPadding / 2;
            rect.Y = _inventoryWindow.LocalPosToCanvas(new Point(0, 0)).Y - Constants.ItemYPadding / 2;
            rect.Width = _inventoryWindow.Width + Constants.ItemXPadding;
            rect.Height = _inventoryWindow.Height + Constants.ItemYPadding;
            return rect;
        }
    }

    public class InventoryItem
    {
        public ImagePanel pnl;
        private ItemDescWindow _descWindow;

        //Mouse Event Variables
        private bool MouseOver = false;
        private int MouseX = -1;
        private int MouseY = -1;
        private long ClickTime = 0;

        //Dragging
        private bool CanDrag = false;
        private Draggable dragIcon;
        public bool IsDragging;

        //Slot info
        private int _mySlot;
        private bool _isEquipped;
        private int _currentItem = -2;

        //Textures
        private Texture gwenTex;
        private GameRenderTexture sfTex;

        //Drag/Drop References
        private InventoryWindow _inventoryWindow;
 

        public InventoryItem(InventoryWindow inventoryWindow, int index)
        {
            _inventoryWindow = inventoryWindow;
            _mySlot = index;
        }

        public void Setup()
        {
            pnl.HoverEnter += pnl_HoverEnter;
            pnl.HoverLeave += pnl_HoverLeave;
            pnl.RightClicked += pnl_RightClicked;
            pnl.DoubleClicked += Pnl_DoubleClicked;
            pnl.Clicked += pnl_Clicked;
        }

        private void Pnl_DoubleClicked(Base sender, ClickedEventArgs arguments)
        {
            if (Globals.GameShop != null)
            {
                Globals.Me.TrySellItem(_mySlot);
            }
            else if (Globals.InBank)
            {
                Globals.Me.TryDepositItem(_mySlot);
            }
        }

        void pnl_Clicked(Base sender, ClickedEventArgs arguments)
        {
            ClickTime = Globals.System.GetTimeMS() + 500;
        }

        void pnl_RightClicked(Base sender, ClickedEventArgs arguments)
        {
            Globals.Me.TryDropItem(_mySlot);
        }

        void pnl_HoverLeave(Base sender, EventArgs arguments)
        {
            MouseOver = false;
            MouseX = -1;
            MouseY = -1;
            if (_descWindow != null) { _descWindow.Dispose(); _descWindow = null; }
        }

        void pnl_HoverEnter(Base sender, EventArgs arguments)
        {
            MouseOver = true;
            CanDrag = true;
            if (Globals.InputManager.MouseButtonDown(GameInput.MouseButtons.Left)){ CanDrag = false; return; }
            if (_descWindow != null) { _descWindow.Dispose(); _descWindow = null; }
            if (Globals.GameShop == null)
            {
                _descWindow = new ItemDescWindow(Globals.Me.Inventory[_mySlot].ItemNum, Globals.Me.Inventory[_mySlot].ItemVal, _inventoryWindow.X - 220, _inventoryWindow.Y, Globals.Me.Inventory[_mySlot].StatBoost);
            }
            else
            {
                int foundItem = -1;
                for (int i = 0; i < Globals.GameShop.BuyingItems.Count; i++)
                {
                    if (Globals.GameShop.BuyingItems[i].ItemNum == Globals.Me.Inventory[_mySlot].ItemNum)
                    {
                        foundItem = i;
                        break;
                    }
                }
                if ((foundItem > -1 && Globals.GameShop.BuyingWhitelist) || (foundItem == -1 && !Globals.GameShop.BuyingWhitelist))
                {
                    if (foundItem > -1)
                    {
                        _descWindow = new ItemDescWindow(Globals.Me.Inventory[_mySlot].ItemNum,
                            Globals.Me.Inventory[_mySlot].ItemVal, _inventoryWindow.X - 220, _inventoryWindow.Y,
                            Globals.Me.Inventory[_mySlot].StatBoost, "",
                            "Sells for " + Globals.GameShop.BuyingItems[foundItem].CostItemVal + " " +
                            Globals.GameItems[Globals.GameShop.BuyingItems[foundItem].CostItemNum].Name + "(s)");
                    }
                    else
                    {
                        _descWindow = new ItemDescWindow(Globals.Me.Inventory[_mySlot].ItemNum,
                            Globals.Me.Inventory[_mySlot].ItemVal, _inventoryWindow.X - 220, _inventoryWindow.Y,
                            Globals.Me.Inventory[_mySlot].StatBoost, "",
                            "Sells for " + Globals.GameItems[Globals.GameShop.BuyingItems[foundItem].ItemNum].Price +
                            " " + Globals.GameItems[Globals.GameShop.DefaultCurrency].Name + "(s)");
                    }
                }
                else
                {
                    _descWindow = new ItemDescWindow(Globals.Me.Inventory[_mySlot].ItemNum, Globals.Me.Inventory[_mySlot].ItemVal, _inventoryWindow.X - 220, _inventoryWindow.Y, Globals.Me.Inventory[_mySlot].StatBoost, "", "Shop Will Not Buy This Item");
                }
            }
        }

        public FloatRect RenderBounds()
        {
            FloatRect rect = new FloatRect();
            rect.X = pnl.LocalPosToCanvas(new Point(0, 0)).X;
            rect.Y = pnl.LocalPosToCanvas(new Point(0, 0)).Y;
            rect.Width = pnl.Width;
            rect.Height = pnl.Height;
            return rect;
        }

        public void Update()
        {
            bool equipped = false;
            for (int i = 0; i < Options.EquipmentSlots.Count; i++)
            {
                if (Globals.Me.Equipment[i] == _mySlot)
                {
                    equipped = true;
                }
            }
            if (Globals.Me.Inventory[_mySlot].ItemNum != _currentItem || equipped != _isEquipped)
            {
                _currentItem = Globals.Me.Inventory[_mySlot].ItemNum;
                _isEquipped = equipped;
                sfTex = Gui.CreateItemTex(_currentItem, 0, 0, 32, 32, _isEquipped, null);
                gwenTex = Gui.ToGwenTexture(sfTex);
                pnl.Texture = gwenTex;
            }
            if (!IsDragging)
            {
                if (MouseOver)
                {
                    if (!Globals.InputManager.MouseButtonDown(GameInput.MouseButtons.Left))
                    {
                        CanDrag = true;
                        MouseX = -1;
                        MouseY = -1;
                        if (Globals.System.GetTimeMS() < ClickTime)
                        {
                            Globals.Me.TryUseItem(_mySlot);
                            ClickTime = 0;
                        }
                    }
                    else
                    {
                        if (CanDrag)
                        {
                            if (MouseX == -1 || MouseY == -1)
                            {
                                MouseX = InputHandler.MousePosition.X - pnl.LocalPosToCanvas(new Point(0, 0)).X;
                                MouseY = InputHandler.MousePosition.Y - pnl.LocalPosToCanvas(new Point(0, 0)).Y;

                            }
                            else
                            {
                                int xdiff = MouseX - (InputHandler.MousePosition.X - pnl.LocalPosToCanvas(new Point(0, 0)).X);
                                int ydiff = MouseY - (InputHandler.MousePosition.Y - pnl.LocalPosToCanvas(new Point(0, 0)).Y);
                                if (Math.Sqrt(Math.Pow(xdiff, 2) + Math.Pow(ydiff, 2)) > 5)
                                {
                                    IsDragging = true;
                                    dragIcon = new Draggable(pnl.LocalPosToCanvas(new Point(0, 0)).X + MouseX, pnl.LocalPosToCanvas(new Point(0, 0)).X + MouseY, pnl.Texture);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (dragIcon.Update())
                {
                    //Drug the item and now we stopped
                    IsDragging = false;
                    FloatRect dragRect = new FloatRect(dragIcon.x - Constants.ItemXPadding / 2, dragIcon.y - Constants.ItemYPadding / 2, Constants.ItemXPadding/2 + 32, Constants.ItemYPadding / 2 + 32);

                    float  bestIntersect = 0;
                    int bestIntersectIndex = -1;
                    //So we picked up an item and then dropped it. Lets see where we dropped it to.
                    //Check inventory first.
                    if (_inventoryWindow.RenderBounds().IntersectsWith(dragRect))
                    {
                        for (int i = 0; i < Options.MaxInvItems; i++)
                        {
                            if (_inventoryWindow.Items[i].RenderBounds().IntersectsWith(dragRect))
                            {
                                if (FloatRect.Intersect(_inventoryWindow.Items[i].RenderBounds(), dragRect).Width * FloatRect.Intersect(_inventoryWindow.Items[i].RenderBounds(), dragRect).Height > bestIntersect)
                                {
                                    bestIntersect = FloatRect.Intersect(_inventoryWindow.Items[i].RenderBounds(), dragRect).Width * FloatRect.Intersect(_inventoryWindow.Items[i].RenderBounds(), dragRect).Height;
                                    bestIntersectIndex = i;
                                }
                            }
                        }
                        if (bestIntersectIndex > -1)
                        {
                            if (_mySlot != bestIntersectIndex)
                            {
                                //Try to swap....
                                PacketSender.SendSwapItems(bestIntersectIndex, _mySlot);
                                Globals.Me.SwapItems(bestIntersectIndex, _mySlot);
                            }
                        }
                    }
                    else if (Gui.GameUI.Hotbar.RenderBounds().IntersectsWith(dragRect))
                    {
                        for (int i = 0; i < Options.MaxHotbar; i++)
                        {
                            if (Gui.GameUI.Hotbar.Items[i].RenderBounds().IntersectsWith(dragRect))
                            {
                                if (FloatRect.Intersect(Gui.GameUI.Hotbar.Items[i].RenderBounds(), dragRect).Width * FloatRect.Intersect(Gui.GameUI.Hotbar.Items[i].RenderBounds(), dragRect).Height > bestIntersect)
                                {
                                    bestIntersect = FloatRect.Intersect(Gui.GameUI.Hotbar.Items[i].RenderBounds(), dragRect).Width * FloatRect.Intersect(Gui.GameUI.Hotbar.Items[i].RenderBounds(), dragRect).Height;
                                    bestIntersectIndex = i;
                                }
                            }
                        }
                        if (bestIntersectIndex > -1)
                        {
                            Globals.Me.AddToHotbar(bestIntersectIndex, 0, _mySlot);
                        }
                    }

                    dragIcon.Dispose();
                }
            }
        }
    }
}
