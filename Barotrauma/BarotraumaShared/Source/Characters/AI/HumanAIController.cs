﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    partial class HumanAIController : AIController
    {
        public static bool DisableCrewAI;

        const float UpdateObjectiveInterval = 0.5f;

        private AIObjectiveManager objectiveManager;
        
        private float updateObjectiveTimer;

        private bool shouldCrouch;
        private float crouchRaycastTimer;
        const float CrouchRaycastInterval = 1.0f;

        public const float HULL_SAFETY_THRESHOLD = 50;

        public HashSet<Hull> UnsafeHulls { get; private set; } = new HashSet<Hull>();

        private SteeringManager outsideSteering, insideSteering;

        public IndoorsSteeringManager PathSteering => insideSteering as IndoorsSteeringManager;
        public HumanoidAnimController AnimController => Character.AnimController as HumanoidAnimController;

        public override AIObjectiveManager ObjectiveManager
        {
            get { return objectiveManager; }
        }

        public Order CurrentOrder
        {
            get;
            private set;
        }

        public string CurrentOrderOption
        {
            get;
            private set;
        }

        public HumanAIController(Character c) : base(c)
        {
            insideSteering = new IndoorsSteeringManager(this, true, false);
            outsideSteering = new SteeringManager(this);

            objectiveManager = new AIObjectiveManager(c);
            objectiveManager.AddObjective(new AIObjectiveFindSafety(c));
            objectiveManager.AddObjective(new AIObjectiveIdle(c));

            updateObjectiveTimer = Rand.Range(0.0f, UpdateObjectiveInterval);

            InitProjSpecific();
        }
        partial void InitProjSpecific();

        public override void Update(float deltaTime)
        {
            if (DisableCrewAI || Character.IsUnconscious) return;
            
            if (Character.Submarine != null || SelectedAiTarget?.Entity?.Submarine != null)
            {
                if (steeringManager != insideSteering) insideSteering.Reset();
                steeringManager = insideSteering;
            }
            else
            {
                if (steeringManager != outsideSteering) outsideSteering.Reset();
                steeringManager = outsideSteering;
            }

            AnimController.Crouching = shouldCrouch;
            CheckCrouching(deltaTime);
            Character.ClearInputs();

            objectiveManager.UpdateObjectives(deltaTime);
            if (updateObjectiveTimer > 0.0f)
            {
                updateObjectiveTimer -= deltaTime;
            }
            else
            {
                objectiveManager.SortObjectives();
                updateObjectiveTimer = UpdateObjectiveInterval;
            }

            if (Character.SpeechImpediment < 100.0f)
            {
                ReportProblems();
                UpdateSpeaking();
            }

            objectiveManager.DoCurrentObjective(deltaTime);

            bool run = objectiveManager.GetCurrentPriority() > AIObjectiveManager.OrderPriority;
            if (ObjectiveManager.CurrentObjective is AIObjectiveGoTo goTo && goTo.Target != null)
            {
                if (Vector2.DistanceSquared(Character.SimPosition, goTo.Target.SimPosition) > 3 * 3)
                {
                    run = true;
                }
            }
            if (!run)
            {
                run = objectiveManager.CurrentObjective.ForceRun;
            }
            if (run)
            {
                run = !AnimController.Crouching && !AnimController.IsMovingBackwards;
            }
            float currentSpeed = Character.AnimController.GetCurrentSpeed(run);
            steeringManager.Update(currentSpeed);

            bool ignorePlatforms = Character.AnimController.TargetMovement.Y < -0.5f &&
                (-Character.AnimController.TargetMovement.Y > Math.Abs(Character.AnimController.TargetMovement.X));

            if (steeringManager == insideSteering)
            {
                var currPath = PathSteering.CurrentPath;
                if (currPath != null && currPath.CurrentNode != null)
                {
                    if (currPath.CurrentNode.SimPosition.Y < Character.AnimController.GetColliderBottom().Y)
                    {
                        // Don't allow to jump from too high. The formula might require tweaking.
                        float allowedJumpHeight = Character.AnimController.ImpactTolerance / 2;
                        float height = Math.Abs(currPath.CurrentNode.SimPosition.Y - Character.SimPosition.Y);
                        ignorePlatforms = height < allowedJumpHeight;
                    }
                }

                if (Character.IsClimbing && PathSteering.IsNextLadderSameAsCurrent)
                {
                    Character.AnimController.TargetMovement = new Vector2(0.0f, Math.Sign(Character.AnimController.TargetMovement.Y));
                }
            }

            Character.AnimController.IgnorePlatforms = ignorePlatforms;

            Vector2 targetMovement = AnimController.TargetMovement;

            if (!Character.AnimController.InWater)
            {
                targetMovement = new Vector2(Character.AnimController.TargetMovement.X, MathHelper.Clamp(Character.AnimController.TargetMovement.Y, -1.0f, 1.0f));
            }

            float maxSpeed = Character.ApplyTemporarySpeedLimits(currentSpeed);
            targetMovement.X = MathHelper.Clamp(targetMovement.X, -maxSpeed, maxSpeed);
            targetMovement.Y = MathHelper.Clamp(targetMovement.Y, -maxSpeed, maxSpeed);

            //apply speed multiplier if 
            //  a. it's boosting the movement speed and the character is trying to move fast (= running)
            //  b. it's a debuff that decreases movement speed
            float speedMultiplier = Character.SpeedMultiplier;
            if (run || speedMultiplier <= 0.0f) targetMovement *= speedMultiplier;
            Character.ResetSpeedMultiplier();   // Reset, items will set the value before the next update
            Character.AnimController.TargetMovement = targetMovement;

            if (!NeedsDivingGear(Character.CurrentHull))
            {
                bool oxygenLow = Character.OxygenAvailable < CharacterHealth.LowOxygenThreshold;
                bool highPressure = Character.CurrentHull == null || Character.CurrentHull.LethalPressure > 0 && Character.PressureProtection <= 0;
                bool shouldKeepTheGearOn = objectiveManager.CurrentObjective.KeepDivingGearOn;

                bool removeDivingSuit = (oxygenLow && !highPressure) || (!shouldKeepTheGearOn && Character.CurrentHull.WaterPercentage < 1 && !Character.IsClimbing && steeringManager == insideSteering && !PathSteering.InStairs);
                if (removeDivingSuit)
                {
                    var divingSuit = Character.Inventory.FindItemByIdentifier("divingsuit") ?? Character.Inventory.FindItemByTag("divingsuit");
                    if (divingSuit != null)
                    {
                        // TODO: take the item where it was taken from?
                        divingSuit.Drop(Character);
                    }
                }
                bool takeMaskOff = oxygenLow || (!shouldKeepTheGearOn && Character.CurrentHull.WaterPercentage < 20);
                if (takeMaskOff)
                {
                    var mask = Character.Inventory.FindItemByIdentifier("divingmask");
                    if (mask != null && Character.Inventory.IsInLimbSlot(mask, InvSlotType.Head))
                    {
                        // Try to put the mask in an Any slot, and drop it if that fails
                        if (!mask.AllowedSlots.Contains(InvSlotType.Any) || !Character.Inventory.TryPutItem(mask, Character, new List<InvSlotType>() { InvSlotType.Any }))
                        {
                            mask.Drop(Character);
                        }
                    }
                }
            }
            if (!(ObjectiveManager.CurrentOrder is AIObjectiveExtinguishFires) && !(ObjectiveManager.CurrentObjective is AIObjectiveExtinguishFire))
            {
                var extinguisherItem = Character.Inventory.FindItemByIdentifier("extinguisher") ?? Character.Inventory.FindItemByTag("extinguisher");
                if (extinguisherItem != null && Character.HasEquippedItem(extinguisherItem))
                {
                    // TODO: take the item where it was taken from?
                    extinguisherItem.Drop(Character);
                }
            }

            if (Character.IsKeyDown(InputType.Aim))
            {
                var cursorDiffX = Character.CursorPosition.X - Character.Position.X;
                if (cursorDiffX > 10.0f)
                {
                    Character.AnimController.TargetDir = Direction.Right;
                }
                else if (cursorDiffX < -10.0f)
                {
                    Character.AnimController.TargetDir = Direction.Left;
                }

                if (Character.SelectedConstruction != null) Character.SelectedConstruction.SecondaryUse(deltaTime, Character);

            }
            else if (Math.Abs(Character.AnimController.TargetMovement.X) > 0.1f && !Character.AnimController.InWater)
            {
                Character.AnimController.TargetDir = Character.AnimController.TargetMovement.X > 0.0f ? Direction.Right : Direction.Left;
            }

            if (Character.CurrentHull != null)
            {
                PropagateHullSafety(Character, Character.CurrentHull);
            }
        }

        protected void ReportProblems()
        {
            Order newOrder = null;
            if (Character.CurrentHull != null)
            {
                if (Character.CurrentHull.FireSources.Count > 0)
                {
                    var orderPrefab = Order.PrefabList.Find(o => o.AITag == "reportfire");
                    newOrder = new Order(orderPrefab, Character.CurrentHull, null);
                }

                if (Character.CurrentHull.ConnectedGaps.Any(g => !g.IsRoomToRoom && g.ConnectedDoor == null && g.Open > 0.0f))
                {
                    var orderPrefab = Order.PrefabList.Find(o => o.AITag == "reportbreach");
                    newOrder = new Order(orderPrefab, Character.CurrentHull, null);
                }

                foreach (Character c in Character.CharacterList)
                {
                    if (c.CurrentHull == Character.CurrentHull && !c.IsDead &&
                        (c.AIController is EnemyAIController || (c.TeamID != Character.TeamID && Character.TeamID != Character.TeamType.FriendlyNPC && c.TeamID != Character.TeamType.FriendlyNPC)))
                    {
                        var orderPrefab = Order.PrefabList.Find(o => o.AITag == "reportintruders");
                        newOrder = new Order(orderPrefab, Character.CurrentHull, null);
                    }
                }
            }

            if (Character.CurrentHull != null && (Character.Bleeding > 1.0f || Character.Vitality < Character.MaxVitality * 0.1f))
            {
                var orderPrefab = Order.PrefabList.Find(o => o.AITag == "requestfirstaid");
                newOrder = new Order(orderPrefab, Character.CurrentHull, null);
            }

            if (newOrder != null)
            {
                if (GameMain.GameSession?.CrewManager != null && GameMain.GameSession.CrewManager.AddOrder(newOrder, newOrder.FadeOutTime))
                {
                    Character.Speak(
                        newOrder.GetChatMessage("", Character.CurrentHull?.DisplayName, givingOrderToSelf: false), ChatMessageType.Order);
                }
            }
        }

        private void UpdateSpeaking()
        {
            if (Character.Oxygen < 20.0f)
            {
                Character.Speak(TextManager.Get("DialogLowOxygen"), null, 0, "lowoxygen", 30.0f);
            }

            if (Character.Bleeding > 2.0f)
            {
                Character.Speak(TextManager.Get("DialogBleeding"), null, 0, "bleeding", 30.0f);
            }

            if (Character.PressureTimer > 50.0f && Character.CurrentHull != null)
            {
                Character.Speak(TextManager.Get("DialogPressure").Replace("[roomname]", Character.CurrentHull.DisplayName), null, 0, "pressure", 30.0f);
            }
        }

        public override void OnAttacked(Character attacker, AttackResult attackResult)
        {
            // Damage from falling etc.
            if (Character.LastDamageSource == null) { return; }
            float damage = attackResult.Damage;
            if (damage <= 0) { return; }
            if (attacker == null || attacker.IsDead || attacker.Removed)
            {
                AddCombatObjective(AIObjectiveCombat.CombatMode.Retreat, Rand.Range(0.5f, 1f, Rand.RandSync.Unsynced));
            }
            else if (IsFriendly(attacker))
            {
                if (attacker.AnimController.Anim == Barotrauma.AnimController.Animation.CPR && attacker.SelectedCharacter == Character)
                {
                    // Don't attack characters that damage you while doing cpr, because let's assume that they are helping you.
                    // Should not cancel any existing ai objectives (so that if the character attacked you and then helped, we still would want to retaliate).
                    return;
                }
                if (!attacker.IsRemotePlayer && Character.Controlled != attacker && attacker.AIController != null && attacker.AIController.Enabled)
                {
                    // Don't retaliate on damage done by friendly ai, because we know that it's accidental
                    AddCombatObjective(AIObjectiveCombat.CombatMode.Retreat, Rand.Range(0.5f, 1f, Rand.RandSync.Unsynced));
                }
                else
                {
                    float currentVitality = Character.CharacterHealth.Vitality;
                    float dmgPercentage = damage / currentVitality * 100;
                    if (dmgPercentage < currentVitality / 10)
                    {
                        // Don't retaliate on minor (accidental) dmg done by friendly characters
                        AddCombatObjective(AIObjectiveCombat.CombatMode.Retreat, Rand.Range(0.5f, 1f, Rand.RandSync.Unsynced));
                    }
                    else
                    {
                        AddCombatObjective(AIObjectiveCombat.CombatMode.Defensive, Rand.Range(0.5f, 1f, Rand.RandSync.Unsynced));
                    }
                }
            }
            else
            {
                AddCombatObjective(AIObjectiveCombat.CombatMode.Defensive);
            }

            void AddCombatObjective(AIObjectiveCombat.CombatMode mode, float delay = 0)
            {
                if (ObjectiveManager.CurrentObjective is AIObjectiveCombat combatObjective)
                {
                    if (combatObjective.Enemy != attacker || (combatObjective.Enemy == null && attacker == null))
                    {
                        // Replace the old objective with the new.
                        ObjectiveManager.Objectives.Remove(combatObjective);
                        objectiveManager.AddObjective(new AIObjectiveCombat(Character, attacker, mode));
                    }
                }
                else
                {
                    if (delay > 0)
                    {
                        objectiveManager.AddObjective(new AIObjectiveCombat(Character, attacker, mode), delay);
                    }
                    else
                    {
                        objectiveManager.AddObjective(new AIObjectiveCombat(Character, attacker, mode));
                    }
                }
            }
        }

        public void SetOrder(Order order, string option, Character orderGiver, bool speak = true)
        {
            CurrentOrderOption = option;
            CurrentOrder = order;
            objectiveManager.SetOrder(order, option, orderGiver);
            if (speak && Character.SpeechImpediment < 100.0f) Character.Speak(TextManager.Get("DialogAffirmative"), null, 1.0f);

            SetOrderProjSpecific(order);
        }
        partial void SetOrderProjSpecific(Order order);

        public override void SelectTarget(AITarget target)
        {
            SelectedAiTarget = target;
        }

        private void CheckCrouching(float deltaTime)
        {
            crouchRaycastTimer -= deltaTime;
            if (crouchRaycastTimer > 0.0f) return;

            crouchRaycastTimer = CrouchRaycastInterval;

            //start the raycast in front of the character in the direction it's heading to
            Vector2 startPos = Character.SimPosition;
            startPos.X += MathHelper.Clamp(Character.AnimController.TargetMovement.X, -1.0f, 1.0f);

            //do a raycast upwards to find any walls
            float minCeilingDist = Character.AnimController.Collider.height / 2 + Character.AnimController.Collider.radius + 0.1f;
            shouldCrouch = Submarine.PickBody(startPos, startPos + Vector2.UnitY * minCeilingDist, null, Physics.CollisionWall) != null;
        }

        public static bool NeedsDivingGear(Hull hull) => hull == null || hull.OxygenPercentage < 50 || hull.WaterPercentage > 50;

        /// <summary>
        /// Check whether the character has a diving suit in usable condition plus some oxygen.
        /// </summary>
        public static bool HasDivingSuit(Character character) => HasItem(character, "divingsuit", "oxygensource");

        /// <summary>
        /// Check whether the character has a diving mask in usable condition plus some oxygen.
        /// </summary>
        public static bool HasDivingGear(Character character) => HasItem(character, "diving", "oxygensource");

        public static bool HasItem(Character character, string tag, string containedTag, float conditionPercentage = 0)
        {
            var item = character.Inventory.FindItemByTag(tag);
            return item != null &&
                item.ConditionPercentage > conditionPercentage &&
                character.HasEquippedItem(item) &&
                (containedTag == null ||
                (item.ContainedItems != null &&
                item.ContainedItems.Any(i => i.HasTag(containedTag) && i.ConditionPercentage > conditionPercentage)));
        }

        /// <summary>
        /// Updates the hull safety for all ai characters in the team.
        /// </summary>
        public static void PropagateHullSafety(Character character, Hull hull)
        {
            foreach (var c in Character.CharacterList)
            {
                if (c.TeamID == character.TeamID)
                {
                    if (c.AIController is HumanAIController humanAi)
                    {
                        humanAi.RefreshHullSafety(hull);
                    }
                }
            }
        }

        private void RefreshHullSafety(Hull hull)
        {
            if (GetHullSafety(hull) > HULL_SAFETY_THRESHOLD)
            {
                UnsafeHulls.Remove(hull);
            }
            else
            {
                UnsafeHulls.Add(hull);
            }
        }

        public float GetHullSafety(Hull hull)
        {
            if (hull == null) { return 0; }
            bool ignoreFire = ObjectiveManager.CurrentObjective is AIObjectiveExtinguishFire || ObjectiveManager.CurrentOrder is AIObjectiveExtinguishFires;
            bool ignoreWater = HasDivingSuit(Character);
            bool ignoreOxygen = ignoreWater || HasDivingGear(Character);
            bool ignoreEnemies = ObjectiveManager.CurrentObjective is AIObjectiveCombat || ObjectiveManager.CurrentOrder is AIObjectiveCombat;
            return GetHullSafety(hull, Character, ignoreWater, ignoreOxygen, ignoreFire, ignoreEnemies);
        }

        public static float GetHullSafety(Hull hull, Character character, bool ignoreWater = false, bool ignoreOxygen = false, bool ignoreFire = false, bool ignoreEnemies = false)
        {
            if (hull == null) { return 0; }
            if (hull.LethalPressure > 0 && character.PressureProtection <= 0) { return 0; }
            float oxygenFactor = ignoreOxygen ? 1 : MathHelper.Lerp(0.25f, 1, hull.OxygenPercentage / 100);
            float waterFactor = ignoreWater ? 1 : MathHelper.Lerp(1, 0.25f, hull.WaterPercentage / 100);
            if (!character.NeedsAir)
            {
                oxygenFactor = 1;
                waterFactor = 1;
            }
            // Even the smallest fire reduces the safety by 50%
            float fire = hull.FireSources.Count * 0.5f + hull.FireSources.Sum(fs => fs.DamageRange) / hull.Size.X;
            float fireFactor = ignoreFire ? 1 : MathHelper.Lerp(1, 0, MathHelper.Clamp(fire, 0, 1));
                int enemyCount = Character.CharacterList.Count(e => 
                e.CurrentHull == hull && !e.IsDead && !e.IsUnconscious && 
                (e.AIController is EnemyAIController || (e.TeamID != character.TeamID && character.TeamID != Character.TeamType.FriendlyNPC && e.TeamID != Character.TeamType.FriendlyNPC)));
            // The hull safety decreases 90% per enemy up to 100% (TODO: test smaller percentages)
            float enemyFactor = ignoreEnemies ? 1 : MathHelper.Lerp(1, 0, MathHelper.Clamp(enemyCount * 0.9f, 0, 1));
            float safety = oxygenFactor * waterFactor * fireFactor * enemyFactor;
            return MathHelper.Clamp(safety * 100, 0, 100);
        }

        // TODO: If the aliens are quaranteed to be in another team than the player, we wouldn't need to check the species.
        public bool IsFriendly(Character other) => other.TeamID == Character.TeamID && other.SpeciesName == Character.SpeciesName;
    }
}
