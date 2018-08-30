﻿using Barotrauma.Networking;
using Lidgren.Network;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class Deconstructor : Powered, IServerSerializable, IClientSerializable
    {
        private float progressTimer;
        private ItemContainer container;
        
        public Deconstructor(Item item, XElement element) 
            : base(item, element)
        {
            InitProjSpecific(element);
        }

        partial void InitProjSpecific(XElement element);

        public override void Update(float deltaTime, Camera cam)
        {
            if (container == null || container.Inventory.Items.All(i => i == null))
            {
                SetActive(false);
                return;
            }

            if (voltage < minVoltage) return;

            ApplyStatusEffects(ActionType.OnActive, deltaTime, null);

            if (powerConsumption == 0.0f) voltage = 1.0f;

            progressTimer += deltaTime * voltage;
            Voltage -= deltaTime * 10.0f;

            var targetItem = container.Inventory.Items.FirstOrDefault(i => i != null);
#if CLIENT
            progressBar.BarSize = System.Math.Min(progressTimer / targetItem.Prefab.DeconstructTime, 1.0f);
#endif
            if (progressTimer > targetItem.Prefab.DeconstructTime)
            {
                var containers = item.GetComponents<ItemContainer>();
                if (containers.Count < 2)
                {
                    DebugConsole.ThrowError("Error in Deconstructor.Update: Deconstructors must have two ItemContainer components!");

                    return;
                }

                foreach (DeconstructItem deconstructProduct in targetItem.Prefab.DeconstructItems)
                {
                    float percentageHealth = targetItem.Condition / targetItem.Prefab.Health;
                    if (percentageHealth <= deconstructProduct.MinCondition || percentageHealth > deconstructProduct.MaxCondition) continue;

                    var itemPrefab = MapEntityPrefab.Find(null, deconstructProduct.ItemIdentifier) as ItemPrefab;
                    if (itemPrefab == null)
                    {
                        DebugConsole.ThrowError("Tried to deconstruct item \"" + targetItem.Name + "\" but couldn't find item prefab \"" + deconstructProduct + "\"!");
                        continue;
                    }

                    //container full, drop the items outside the deconstructor
                    if (containers[1].Inventory.Items.All(i => i != null))
                    {
                        Entity.Spawner.AddToSpawnQueue(itemPrefab, item.Position, item.Submarine, itemPrefab.Health * deconstructProduct.OutCondition);
                    }
                    else
                    {
                        Entity.Spawner.AddToSpawnQueue(itemPrefab, containers[1].Inventory, itemPrefab.Health * deconstructProduct.OutCondition);
                    }
                }

                container.Inventory.RemoveItem(targetItem);
#if SERVER
                Entity.Spawner.AddToRemoveQueue(targetItem);
#endif

                if (container.Inventory.Items.Any(i => i != null))
                {
                    progressTimer = 0.0f;
#if CLIENT
                    progressBar.BarSize = 0.0f;
#endif
                }
            }
        }

        private void SetActive(bool active, Character user = null)
        {
            container = item.GetComponent<ItemContainer>();
            if (container == null)
            {
                DebugConsole.ThrowError("Error in Deconstructor.Activate: Deconstructors must have two ItemContainer components");
                return;
            }

            if (container.Inventory.Items.All(i => i == null)) active = false;

            IsActive = active;

#if SERVER
            if (user != null)
            {
                GameServer.Log(user.LogName + (IsActive ? " activated " : " deactivated ") + item.Name, ServerLog.MessageType.ItemInteraction);
            }
#endif

#if CLIENT
            if (!IsActive)
            {
                progressBar.BarSize = 0.0f;
                progressTimer = 0.0f;

                activateButton.Text = "Deconstruct";
            }
            else
            {

                activateButton.Text = "Cancel";
            }
#endif

            container.Inventory.Locked = IsActive;            
        }
    }
}
