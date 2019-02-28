﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Barotrauma
{
    partial class EntitySpawner : Entity, IServerSerializable
    {
        const int MaxEntitiesPerWrite = 10;

        private enum SpawnableType { Item, Character };
        
        interface IEntitySpawnInfo
        {
            Entity Spawn();
        }

        class ItemSpawnInfo : IEntitySpawnInfo
        {
            public readonly ItemPrefab Prefab;

            public readonly Vector2 Position;
            public readonly Inventory Inventory;
            public readonly Submarine Submarine;
            public readonly float Condition;

            public ItemSpawnInfo(ItemPrefab prefab, Vector2 worldPosition, float? condition = null)
            {
                Prefab = prefab ?? throw new ArgumentException("ItemSpawnInfo prefab cannot be null.");
                Position = worldPosition;
                Condition = condition ?? prefab.Health;
            }

            public ItemSpawnInfo(ItemPrefab prefab, Vector2 position, Submarine sub, float? condition = null)
            {
                Prefab = prefab ?? throw new ArgumentException("ItemSpawnInfo prefab cannot be null.");
                Position = position;
                Submarine = sub;
                Condition = condition ?? prefab.Health;
            }
            
            public ItemSpawnInfo(ItemPrefab prefab, Inventory inventory, float? condition = null)
            {
                Prefab = prefab ?? throw new ArgumentException("ItemSpawnInfo prefab cannot be null.");
                Inventory = inventory;
                Condition = condition ?? prefab.Health;
            }

            public Entity Spawn()
            {                
                if (Prefab == null)
                {
                    return null;
                }
                Item spawnedItem = null;
                if (Inventory != null)
                {
                    spawnedItem = new Item(Prefab, Vector2.Zero, null);
                    Inventory.TryPutItem(spawnedItem, null, spawnedItem.AllowedSlots);
                }
                else
                {
                    spawnedItem = new Item(Prefab, Position, Submarine);
                }
                return spawnedItem;
            }
        }

        private readonly Queue<IEntitySpawnInfo> spawnQueue;
        private readonly Queue<Entity> removeQueue;

        class SpawnOrRemove
        {
            public readonly Entity Entity;

            public readonly bool Remove = false;

            public SpawnOrRemove(Entity entity, bool remove)
            {
                Entity = entity;
                Remove = remove;
            }
        }
        
        public EntitySpawner()
            : base(null)
        {
            spawnQueue = new Queue<IEntitySpawnInfo>();
            removeQueue = new Queue<Entity>();
        }

        public void AddToSpawnQueue(ItemPrefab itemPrefab, Vector2 worldPosition, float? condition = null)
        {
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return; }
            if (itemPrefab == null)
            {
                string errorMsg = "Attempted to add a null item to entity spawn queue.\n" + Environment.StackTrace;
                DebugConsole.ThrowError(errorMsg);
                GameAnalyticsManager.AddErrorEventOnce("EntitySpawner.AddToSpawnQueue3:ItemPrefabNull", GameAnalyticsSDK.Net.EGAErrorSeverity.Error, errorMsg);
                return;
            }
            spawnQueue.Enqueue(new ItemSpawnInfo(itemPrefab, worldPosition, condition));
        }

        public void AddToSpawnQueue(ItemPrefab itemPrefab, Vector2 position, Submarine sub, float? condition = null)
        {
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return; }
            if (itemPrefab == null)
            {
                string errorMsg = "Attempted to add a null item to entity spawn queue.\n" + Environment.StackTrace;
                DebugConsole.ThrowError(errorMsg);
                GameAnalyticsManager.AddErrorEventOnce("EntitySpawner.AddToSpawnQueue3:ItemPrefabNull", GameAnalyticsSDK.Net.EGAErrorSeverity.Error, errorMsg);
                return;
            }
            spawnQueue.Enqueue(new ItemSpawnInfo(itemPrefab, position, sub, condition));
        }

        public void AddToSpawnQueue(ItemPrefab itemPrefab, Inventory inventory, float? condition = null)
        {
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return; }
            if (itemPrefab == null)
            {
                string errorMsg = "Attempted to add a null item to entity spawn queue.\n" + Environment.StackTrace;
                DebugConsole.ThrowError(errorMsg);
                GameAnalyticsManager.AddErrorEventOnce("EntitySpawner.AddToSpawnQueue3:ItemPrefabNull", GameAnalyticsSDK.Net.EGAErrorSeverity.Error, errorMsg);
                return;
            }
            spawnQueue.Enqueue(new ItemSpawnInfo(itemPrefab, inventory, condition));
        }

        public void AddToRemoveQueue(Entity entity)
        {
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return; }
            if (removeQueue.Contains(entity) || entity.Removed || entity == null) { return; }
            if (entity is Character)
            {
                Character character = entity as Character;
#if SERVER
                if (GameMain.Server != null)
                {
                    Client client = GameMain.Server.ConnectedClients.Find(c => c.Character == character);
                    if (client != null) GameMain.Server.SetClientCharacter(client, null);
                }
#endif
            }            

            removeQueue.Enqueue(entity);
        }

        public void AddToRemoveQueue(Item item)
        {
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return; }
            if (removeQueue.Contains(item) || item.Removed) { return; }

            removeQueue.Enqueue(item);
            if (item.ContainedItems == null) return;
            foreach (Item containedItem in item.ContainedItems)
            {
                if (containedItem != null) AddToRemoveQueue(containedItem);
            }
        }

        public void Update()
        {
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return; }
            while (spawnQueue.Count > 0)
            {
                var entitySpawnInfo = spawnQueue.Dequeue();

                var spawnedEntity = entitySpawnInfo.Spawn();
                if (spawnedEntity != null)
                {
#if SERVER
                    CreateNetworkEvent(spawnedEntity, false);
#endif
                    if (spawnedEntity is Item)
                    {
                        ((Item)spawnedEntity).Condition = ((ItemSpawnInfo)entitySpawnInfo).Condition;
                    }
                }
            }

            while (removeQueue.Count > 0)
            {
                var removedEntity = removeQueue.Dequeue();

#if SERVER
                if (GameMain.Server != null)
                {
                    CreateNetworkEvent(removedEntity, true);
                }
#endif

                removedEntity.Remove();
            }
        }

        public void Reset()
        {
            removeQueue.Clear();
            spawnQueue.Clear();
        }
    }
}
