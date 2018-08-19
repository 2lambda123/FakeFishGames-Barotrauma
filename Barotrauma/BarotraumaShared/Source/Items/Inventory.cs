﻿using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    partial class Inventory : IServerSerializable, IClientSerializable
    {
        public readonly Entity Owner;

        protected int capacity;

        public Item[] Items;
        protected bool[] hideEmptySlot;
        
        public bool Locked;

        private ushort[] receivedItemIDs;
        private float syncItemsDelay;
        private CoroutineHandle syncItemsCoroutine;

        public int Capacity
        {
            get { return capacity; }
        }

        public Inventory(Entity owner, int capacity, Vector2? centerPos = null, int slotsPerRow = 5)
        {
            this.capacity = capacity;

            this.Owner = owner;

            Items = new Item[capacity];
            hideEmptySlot = new bool[capacity];

#if CLIENT
            this.slotsPerRow = slotsPerRow;
            CenterPos = (centerPos == null) ? new Vector2(0.5f, 0.5f) : (Vector2)centerPos;

            if (slotSpriteSmall == null)
            {
                slotSpriteSmall = new Sprite("Content/UI/inventoryAtlas.png", new Rectangle(532, 395, 75, 71), null, 0);
                slotSpriteVertical = new Sprite("Content/UI/inventoryAtlas.png", new Rectangle(672, 218, 75, 144), null, 0);
                slotSpriteHorizontal = new Sprite("Content/UI/inventoryAtlas.png", new Rectangle(476, 186, 160, 75), null, 0);
                slotSpriteRound = new Sprite("Content/UI/inventoryAtlas.png", new Rectangle(681, 373, 58, 64), null, 0);
                EquipIndicator = new Sprite("Content/UI/inventoryAtlas.png", new Rectangle(673, 182, 73, 27), null, 0);
                EquipIndicatorOn = new Sprite("Content/UI/inventoryAtlas.png", new Rectangle(679, 108, 67, 21), null, 0);
            }
#endif
        }

        public int FindIndex(Item item)
        {
            for (int i = 0; i < capacity; i++)
            {
                if (Items[i] == item) return i;
            }
            return -1;
        }
        
        /// Returns true if the item owns any of the parent inventories
        public virtual bool ItemOwnsSelf(Item item)
        {
            if (Owner == null) return false;
            if (!(Owner is Item)) return false;
            Item ownerItem = Owner as Item;
            if (ownerItem == item) return true;
            if (ownerItem.ParentInventory == null) return false;
            return ownerItem.ParentInventory.ItemOwnsSelf(item);
        }

        public virtual int FindAllowedSlot(Item item)
        {
            if (ItemOwnsSelf(item)) return -1;

            for (int i = 0; i < capacity; i++)
            {
                //item is already in the inventory!
                if (Items[i] == item) return -1;
            }

            for (int i = 0; i < capacity; i++)
            {
                if (Items[i] == null) return i;                   
            }
            
            return -1;
        }

        public virtual bool CanBePut(Item item, int i)
        {
            if (ItemOwnsSelf(item)) return false;
            if (i < 0 || i >= Items.Length) return false;
            return (Items[i] == null);            
        }
        
        /// <summary>
        /// If there is room, puts the item in the inventory and returns true, otherwise returns false
        /// </summary>
        public virtual bool TryPutItem(Item item, Character user, List<InvSlotType> allowedSlots = null, bool createNetworkEvent = true)
        {
            int slot = FindAllowedSlot(item);
            if (slot < 0) return false;

            PutItem(item, slot, user, true, createNetworkEvent);
            return true;
        }

        public virtual bool TryPutItem(Item item, int i, bool allowSwapping, bool allowCombine, Character user, bool createNetworkEvent = true)
        {
            if (Owner == null) return false;
            if (CanBePut(item, i))
            {
                PutItem(item, i, user, true, createNetworkEvent);
                return true;
            }
            else
            {
#if CLIENT
                if (slots != null && createNetworkEvent) slots[i].ShowBorderHighlight(Color.Red, 0.1f, 0.9f);
#endif
                return false;
            }
        }

        protected virtual void PutItem(Item item, int i, Character user, bool removeItem = true, bool createNetworkEvent = true)
        {
            if (Owner == null) return;

            if (removeItem)
            {
                item.Drop(user);
                if (item.ParentInventory != null) item.ParentInventory.RemoveItem(item);
            }

            Items[i] = item;
            item.ParentInventory = this;

#if CLIENT
            if (slots != null) slots[i].ShowBorderHighlight(Color.White, 0.1f, 0.4f);
#endif

            if (item.body != null)
            {
                item.body.Enabled = false;
            }

            if (createNetworkEvent)
            {
                CreateNetworkEvent();
            }
        }

        protected virtual void CreateNetworkEvent()
        {
#if SERVER
            if (GameMain.Server != null)
            {
                GameMain.Server.CreateEntityEvent(Owner as IServerSerializable, new object[] { NetEntityEvent.Type.InventoryState });
            }
#endif
#if CLIENT
            if (GameMain.Client != null)
            {
                GameMain.Client.CreateEntityEvent(Owner as IClientSerializable, new object[] { NetEntityEvent.Type.InventoryState });
            }
#endif
        }

        public Item FindItemByTag(string tag)
        {
            if (tag == null) return null;
            return Items.FirstOrDefault(i => i != null && i.HasTag(tag));
        }

        public Item FindItemByIdentifier(string identifier)
        {
            if (identifier == null) return null;
            return Items.FirstOrDefault(i => i != null && i.Prefab.Identifier == identifier);
        }

        /*public Item FindItem(string[] itemNames)
        {
            if (itemNames == null) return null;

            foreach (string itemName in itemNames)
            {
                var item = FindItem(itemName);
                if (item != null) return item;
            }
            return null;
        }*/

        public virtual void RemoveItem(Item item)
        {
            if (item == null) return;

            //go through the inventory and remove the item from all slots
            for (int n = 0; n < capacity; n++)
            {
                if (Items[n] != item) continue;
                
                Items[n] = null;
                item.ParentInventory = null;                
            }
        }

        public void SharedWrite(NetBuffer msg, object[] extraData = null)
        {
            for (int i = 0; i < capacity; i++)
            {
                msg.Write((ushort)(Items[i] == null ? 0 : Items[i].ID));
            }
        }

        public void ClientWrite(NetBuffer msg, object[] extraData = null)
        {
            SharedWrite(msg, extraData);

            syncItemsDelay = 1.0f;
        }
    }
}
