﻿//using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Items.Components;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Joints;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    public enum LimbType
    {
        None, LeftHand, RightHand, LeftArm, RightArm,
        LeftLeg, RightLeg, LeftFoot, RightFoot, Head, Torso, Tail, Legs, RightThigh, LeftThigh, Waist
    };
    
    partial class LimbJoint : RevoluteJoint
    {
        public bool IsSevered;
        public bool CanBeSevered => jointParams.CanBeSevered;
        public readonly JointParams jointParams;
        public readonly Ragdoll ragdoll;
        public readonly Limb LimbA, LimbB;

        public LimbJoint(Limb limbA, Limb limbB, JointParams jointParams, Ragdoll ragdoll) : this(limbA, limbB, Vector2.One, Vector2.One)
        {
            this.jointParams = jointParams;
            this.ragdoll = ragdoll;
            LoadParams();
        }

        public LimbJoint(Limb limbA, Limb limbB, Vector2 anchor1, Vector2 anchor2)
            : base(limbA.body.FarseerBody, limbB.body.FarseerBody, anchor1, anchor2)
        {
            CollideConnected = false;
            MotorEnabled = true;
            MaxMotorTorque = 0.25f;
            LimbA = limbA;
            LimbB = limbB;
        }

        public void SaveParams()
        {
            if (ragdoll.IsFlipped)
            {
                jointParams.Limb1Anchor = ConvertUnits.ToDisplayUnits(new Vector2(-LocalAnchorA.X, LocalAnchorA.Y) / jointParams.Ragdoll.JointScale);
                jointParams.Limb2Anchor = ConvertUnits.ToDisplayUnits(new Vector2(-LocalAnchorB.X, LocalAnchorB.Y) / jointParams.Ragdoll.JointScale);
                if (!float.IsNaN(jointParams.LowerLimit))
                {
                    jointParams.UpperLimit = MathHelper.ToDegrees(-LowerLimit);
                }
                if (!float.IsNaN(jointParams.UpperLimit))
                {
                    jointParams.LowerLimit = MathHelper.ToDegrees(-UpperLimit);
                }
            }
            else
            {
                jointParams.Limb1Anchor = ConvertUnits.ToDisplayUnits(LocalAnchorA / jointParams.Ragdoll.JointScale);
                jointParams.Limb2Anchor = ConvertUnits.ToDisplayUnits(LocalAnchorB / jointParams.Ragdoll.JointScale);
                if (!float.IsNaN(jointParams.UpperLimit))
                {
                    jointParams.UpperLimit = MathHelper.ToDegrees(UpperLimit);
                }
                if (!float.IsNaN(jointParams.LowerLimit))
                {
                    jointParams.LowerLimit = MathHelper.ToDegrees(LowerLimit);
                }
            }
        }

        public void LoadParams()
        {
            // If neither of the limits have been defined, disable the limits
            if (float.IsNaN(jointParams.LowerLimit) && float.IsNaN(jointParams.UpperLimit))
            {
                jointParams.LimitEnabled = false;
            }
            LimitEnabled = jointParams.LimitEnabled;
            if (LimitEnabled)
            {
                // If limits are enabled, don't allow NaN
                if (float.IsNaN(jointParams.LowerLimit))
                {
                    jointParams.LowerLimit = 0;
                }
                if (float.IsNaN(jointParams.UpperLimit))
                {
                    jointParams.UpperLimit = 0;
                }
            }
            if (ragdoll.IsFlipped)
            {
                LocalAnchorA = ConvertUnits.ToSimUnits(new Vector2(-jointParams.Limb1Anchor.X, jointParams.Limb1Anchor.Y) * jointParams.Ragdoll.JointScale);
                LocalAnchorB = ConvertUnits.ToSimUnits(new Vector2(-jointParams.Limb2Anchor.X, jointParams.Limb2Anchor.Y) * jointParams.Ragdoll.JointScale);
                if (!float.IsNaN(jointParams.LowerLimit) && !float.IsNaN(jointParams.UpperLimit))
                {
                    UpperLimit = MathHelper.ToRadians(-jointParams.LowerLimit);
                    LowerLimit = MathHelper.ToRadians(-jointParams.UpperLimit);
                }
            }
            else
            {
                LocalAnchorA = ConvertUnits.ToSimUnits(jointParams.Limb1Anchor * jointParams.Ragdoll.JointScale);
                LocalAnchorB = ConvertUnits.ToSimUnits(jointParams.Limb2Anchor * jointParams.Ragdoll.JointScale);
                if (!float.IsNaN(jointParams.LowerLimit) && !float.IsNaN(jointParams.UpperLimit))
                {
                    UpperLimit = MathHelper.ToRadians(jointParams.UpperLimit);
                    LowerLimit = MathHelper.ToRadians(jointParams.LowerLimit);
                }
            }
        }
    }
    
    partial class Limb : ISerializableEntity
    {
        private const float LimbDensity = 15;
        private const float LimbAngularDamping = 7;

        //how long it takes for severed limbs to fade out
        private const float SeveredFadeOutTime = 10.0f;

        public readonly Character character;
        /// <summary>
        /// Note that during the limb initialization, character.AnimController returns null, whereas this field is already assigned.
        /// </summary>
        public readonly Ragdoll ragdoll;
        public readonly LimbParams limbParams;

        //the physics body of the limb
        public PhysicsBody body;
                        
        public Vector2 StepOffset => ConvertUnits.ToSimUnits(limbParams.StepOffset) * ragdoll.RagdollParams.JointScale;

        public Sprite Sprite { get; protected set; }
        public DeformableSprite DeformSprite { get; protected set; }
        public Sprite ActiveSprite => DeformSprite != null ? DeformSprite.Sprite : Sprite;

        public Sprite DamagedSprite { get; set; }

        public bool inWater;

        private readonly FixedMouseJoint pullJoint;

        public readonly LimbType type;

        public readonly bool ignoreCollisions;
        
        private bool isSevered;
        private float severedFadeOutTimer;
                
        public Vector2? MouthPos;
        
        public readonly Attack attack;
        private List<DamageModifier> damageModifiers;

        private Direction dir;
        
        public float AttackTimer;

        public int HealthIndex => limbParams.HealthIndex;
        public float Scale => limbParams.Ragdoll.LimbScale;
        public float AttackPriority => limbParams.AttackPriority;
        public bool DoesFlip => limbParams.Flip;
        public float SteerForce => limbParams.SteerForce;

        public bool IsSevered
        {
            get { return isSevered; }
            set
            {
                isSevered = value;
                if (!isSevered) severedFadeOutTimer = 0.0f;
#if CLIENT
                if (isSevered) damageOverlayStrength = 100.0f;
#endif
            }
        }

        public Vector2 WorldPosition
        {
            get { return character.Submarine == null ? Position : Position + character.Submarine.Position; }
        }

        public Vector2 Position
        {
            get { return ConvertUnits.ToDisplayUnits(body.SimPosition); }
        }

        public Vector2 SimPosition
        {
            get { return body.SimPosition; }
        }

        public float Rotation
        {
            get { return body.Rotation; }
        }

        //where an animcontroller is trying to pull the limb, only used for debug visualization
        public Vector2 AnimTargetPos { get; private set; }

        public float Mass
        {
            get { return body.Mass; }
        }

        public bool Disabled { get; set; }
 
        public Vector2 LinearVelocity
        {
            get { return body.LinearVelocity; }
        }

        public float Dir
        {
            get { return ((dir == Direction.Left) ? -1.0f : 1.0f); }
            set { dir = (value==-1.0f) ? Direction.Left : Direction.Right; }
        }

        public int RefJointIndex { get; private set; }

        private List<WearableSprite> wearingItems;
        public List<WearableSprite> WearingItems
        {
            get { return wearingItems; }
        }

        public bool PullJointEnabled
        {
            get { return pullJoint.Enabled; }
            set { pullJoint.Enabled = value; }
        }

        public float PullJointMaxForce
        {
            get { return pullJoint.MaxForce; }
            set { pullJoint.MaxForce = value; }
        }

        public Vector2 PullJointWorldAnchorA
        {
            get { return pullJoint.WorldAnchorA; }
            set
            {
                if (!MathUtils.IsValid(value))
                {
                    string errorMsg = "Attempted to set the anchor of a limb's pull joint to an invalid value (" + value + ")\n" + Environment.StackTrace;
                    DebugConsole.ThrowError(errorMsg);
                    GameAnalyticsManager.AddErrorEventOnce("Limb.SetPullJointAnchorA:InvalidValue", GameAnalyticsSDK.Net.EGAErrorSeverity.Error, errorMsg);
                    return;
                }

                if (Vector2.DistanceSquared(pullJoint.WorldAnchorB, value) > 50.0f * 50.0f)
                {
                    string errorMsg = "Attempted to move the anchor of a limb's pull joint extremely far from the limb (" + value + ")\n" + Environment.StackTrace;
                    DebugConsole.ThrowError(errorMsg);
                    GameAnalyticsManager.AddErrorEventOnce("Limb.SetPullJointAnchorA:ExcessiveValue", GameAnalyticsSDK.Net.EGAErrorSeverity.Error, errorMsg);
                    return;
                }

                pullJoint.WorldAnchorA = value;
            }
        }
        
        public Vector2 PullJointWorldAnchorB
        {
            get { return pullJoint.WorldAnchorB; }
            set
            {
                if (!MathUtils.IsValid(value))
                {
                    string errorMsg = "Attempted to set the anchor of a limb's pull joint to an invalid value (" + value + ")\n" + Environment.StackTrace;
                    DebugConsole.ThrowError(errorMsg);
                    GameAnalyticsManager.AddErrorEventOnce("Limb.SetPullJointAnchorB:InvalidValue", GameAnalyticsSDK.Net.EGAErrorSeverity.Error, errorMsg);
                    return;
                }

                if (Vector2.DistanceSquared(pullJoint.WorldAnchorA, value) > 50.0f * 50.0f)
                {
                    string errorMsg = "Attempted to move the anchor of a limb's pull joint extremely far from the limb (" + value + ")\n" + Environment.StackTrace;
                    DebugConsole.ThrowError(errorMsg);
                    GameAnalyticsManager.AddErrorEventOnce("Limb.SetPullJointAnchorB:ExcessiveValue", GameAnalyticsSDK.Net.EGAErrorSeverity.Error, errorMsg);
                    return;
                }

                pullJoint.WorldAnchorB = value;                
            }
        }

        public Vector2 PullJointLocalAnchorA
        {
            get { return pullJoint.LocalAnchorA; }
        }
        
        public string Name
        {
            get;
            private set;
        }

        public Dictionary<string, SerializableProperty> SerializableProperties
        {
            get;
            private set;
        }

        public Limb(Ragdoll ragdoll, Character character, LimbParams limbParams)
        {
            this.ragdoll = ragdoll;
            this.character = character;
            this.limbParams = limbParams;
            wearingItems = new List<WearableSprite>();            
            dir = Direction.Right;
            body = new PhysicsBody(limbParams, Scale);
            type = limbParams.Type;
            Name = type.ToString();
            if (limbParams.IgnoreCollisions)
            {
                body.CollisionCategories = Category.None;
                body.CollidesWith = Category.None;
                ignoreCollisions = true;
            }
            else
            {
                //limbs don't collide with each other
                body.CollisionCategories = Physics.CollisionCharacter;
                body.CollidesWith = Physics.CollisionAll & ~Physics.CollisionCharacter & ~Physics.CollisionItem & ~Physics.CollisionItemBlocking;
            }
            body.UserData = this;
            Vector2 pullJointPos = Vector2.Zero;
            pullJointPos = ConvertUnits.ToSimUnits(limbParams.PullPos * Scale);
            RefJointIndex = limbParams.RefJoint;
            pullJoint = new FixedMouseJoint(body.FarseerBody, pullJointPos)
            {
                Enabled = false,
                MaxForce = ((type == LimbType.LeftHand || type == LimbType.RightHand) ? 400.0f : 150.0f) * body.Mass
            };

            GameMain.World.AddJoint(pullJoint);

            var element = limbParams.Element;
            if (element.Attribute("mouthpos") != null)
            {
                MouthPos = ConvertUnits.ToSimUnits(element.GetAttributeVector2("mouthpos", Vector2.Zero));
            }

            // Overrides the settings in the params, on purpose?
            //body.BodyType = BodyType.Dynamic;
            body.FarseerBody.AngularDamping = LimbAngularDamping;

            damageModifiers = new List<DamageModifier>();

            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    // DeformableSprites handled by client only
                    case "sprite":
                        string spritePath = subElement.Attribute("texture").Value;
                        string spritePathWithTags = spritePath;
                        if (character.Info != null)
                        {
                            spritePath = spritePath.Replace("[GENDER]", (character.Info.Gender == Gender.Female) ? "f" : "");
                            spritePath = spritePath.Replace("[HEADID]", character.Info.HeadSpriteId.ToString());

                            if (character.Info.HeadSprite != null && character.Info.SpriteTags.Any())
                            {
                                string tags = "";
                                character.Info.SpriteTags.ForEach(tag => tags += "[" + tag + "]");

                                spritePathWithTags = Path.Combine(
                                    Path.GetDirectoryName(spritePath),
                                    Path.GetFileNameWithoutExtension(spritePath) + tags + Path.GetExtension(spritePath));
                            }
                        }
                        if (File.Exists(spritePathWithTags))
                        {
                            Sprite = new Sprite(subElement, "", spritePathWithTags);
                        }
                        else
                        {
                            Sprite = new Sprite(subElement, "", spritePath);
                        }
                        break;
                    case "damagedsprite":
                        string damagedSpritePath = subElement.Attribute("texture").Value;
                        if (character.Info != null)
                        {
                            damagedSpritePath = damagedSpritePath.Replace("[GENDER]", (character.Info.Gender == Gender.Female) ? "f" : "");
                            damagedSpritePath = damagedSpritePath.Replace("[HEADID]", character.Info.HeadSpriteId.ToString());
                        }
                        DamagedSprite = new Sprite(subElement, "", damagedSpritePath);
                        break;
                    case "attack":
                        attack = new Attack(subElement, (character == null ? "null" : character.Name) + ", limb " + type);
                        break;
                    case "damagemodifier":
                        damageModifiers.Add(new DamageModifier(subElement, character.Name));
                        break;
                }
            }

            SerializableProperties = SerializableProperty.GetProperties(this);

            InitProjSpecific(element);
        }
        partial void InitProjSpecific(XElement element);

        public void MoveToPos(Vector2 pos, float force, bool pullFromCenter = false)
        {
            Vector2 pullPos = body.SimPosition;
            if (!pullFromCenter)
            {
                pullPos = pullJoint.WorldAnchorA;
            }

            AnimTargetPos = pos;

            body.MoveToPos(pos, force, pullPos);
        }

        public void MirrorPullJoint()
        {
            pullJoint.LocalAnchorA = new Vector2(-pullJoint.LocalAnchorA.X, pullJoint.LocalAnchorA.Y);
        }
        
        public AttackResult AddDamage(Vector2 position, float damage, float bleedingDamage, float burnDamage, bool playSound)
        {
            List<Affliction> afflictions = new List<Affliction>();
            if (damage > 0.0f) afflictions.Add(AfflictionPrefab.InternalDamage.Instantiate(damage));
            if (bleedingDamage > 0.0f) afflictions.Add(AfflictionPrefab.Bleeding.Instantiate(bleedingDamage));
            if (burnDamage > 0.0f) afflictions.Add(AfflictionPrefab.Burn.Instantiate(burnDamage));

            return AddDamage(position, afflictions, playSound);
        }

        public AttackResult AddDamage(Vector2 position, List<Affliction> afflictions, bool playSound)
        {
            List<DamageModifier> appliedDamageModifiers = new List<DamageModifier>();
            //create a copy of the original affliction list to prevent modifying the afflictions of an Attack/StatusEffect etc
            afflictions = new List<Affliction>(afflictions);
            for (int i = 0; i < afflictions.Count; i++)
            {
                foreach (DamageModifier damageModifier in damageModifiers)
                {
                    if (!damageModifier.MatchesAffliction(afflictions[i])) continue;
                    if (SectorHit(damageModifier.ArmorSector, position))
                    {
                        afflictions[i] = afflictions[i].CreateMultiplied(damageModifier.DamageMultiplier);
                        appliedDamageModifiers.Add(damageModifier);
                    }
                }

                foreach (WearableSprite wearable in wearingItems)
                {
                    foreach (DamageModifier damageModifier in wearable.WearableComponent.DamageModifiers)
                    {
                        if (!damageModifier.MatchesAffliction(afflictions[i])) continue;
                        if (SectorHit(damageModifier.ArmorSector, position))
                        {
                            afflictions[i] = afflictions[i].CreateMultiplied(damageModifier.DamageMultiplier);
                            appliedDamageModifiers.Add(damageModifier);
                        }
                    }
                }
            }

            AddDamageProjSpecific(position, afflictions, playSound, appliedDamageModifiers);

            return new AttackResult(afflictions, this, appliedDamageModifiers);
        }

        partial void AddDamageProjSpecific(Vector2 position, List<Affliction> afflictions, bool playSound, List<DamageModifier> appliedDamageModifiers);

        public bool SectorHit(Vector2 armorSector, Vector2 simPosition)
        {
            if (armorSector == Vector2.Zero) return false;
            
            float rot = body.Rotation;
            if (Dir == -1) rot -= MathHelper.Pi;

            Vector2 armorLimits = new Vector2(rot - armorSector.X * Dir, rot - armorSector.Y * Dir);

            float mid = (armorLimits.X + armorLimits.Y) / 2.0f;
            float angleDiff = MathUtils.GetShortestAngle(MathUtils.VectorToAngle(simPosition - SimPosition), mid);

            return (Math.Abs(angleDiff) < (armorSector.Y - armorSector.X) / 2.0f);
        }

        public void Update(float deltaTime)
        {
            UpdateProjSpecific(deltaTime);
            
            if (LinearVelocity.X > 500.0f)
            {
                //DebugConsole.ThrowError("CHARACTER EXPLODED");
                body.ResetDynamics();
                body.SetTransform(character.SimPosition, 0.0f);           
            }

            if (inWater)
            {
                body.ApplyWaterForces();
            }

            if (isSevered)
            {
                severedFadeOutTimer += deltaTime;
                if (severedFadeOutTimer > SeveredFadeOutTime)
                {
                    body.Enabled = false;
                }
            }
        }

        partial void UpdateProjSpecific(float deltaTime);
        
        public void UpdateAttack(float deltaTime, Vector2 attackPosition, IDamageable damageTarget)
        {
            float dist = ConvertUnits.ToDisplayUnits(Vector2.Distance(SimPosition, attackPosition));

            AttackTimer += deltaTime;

            body.ApplyTorque(Mass * character.AnimController.Dir * attack.Torque);

            bool wasHit = false;

            if (damageTarget != null)
            {
                switch (attack.HitDetectionType)
                {
                    case HitDetection.Distance:
                        wasHit = dist < attack.DamageRange;
                        break;
                    case HitDetection.Contact:
                        List<Body> targetBodies = new List<Body>();
                        if (damageTarget is Character targetCharacter)
                        {
                            foreach (Limb limb in targetCharacter.AnimController.Limbs)
                            {
                                if (!limb.IsSevered && limb.body?.FarseerBody != null) targetBodies.Add(limb.body.FarseerBody);
                            }
                        }
                        else if (damageTarget is Structure targetStructure)
                        {
                            if (character.Submarine == null && targetStructure.Submarine != null)
                            {
                                targetBodies.Add(targetStructure.Submarine.PhysicsBody.FarseerBody);
                            }
                            else
                            {
                                targetBodies.AddRange(targetStructure.Bodies);
                            }
                        }
                        else if (damageTarget is Item)
                        {
                            Item targetItem = damageTarget as Item;
                            if (targetItem.body?.FarseerBody != null) targetBodies.Add(targetItem.body.FarseerBody);
                        }

                        if (targetBodies != null)
                        {
                            ContactEdge contactEdge = body.FarseerBody.ContactList;
                            while (contactEdge != null)
                            {
                                if (contactEdge.Contact != null &&
                                    contactEdge.Contact.IsTouching &&
                                    targetBodies.Any(b => b == contactEdge.Contact.FixtureA?.Body || b == contactEdge.Contact.FixtureB?.Body))
                                {
                                    wasHit = true;
                                    break;
                                }

                                contactEdge = contactEdge.Next;
                            }
                        }
                        break;
                }
            }

            if (wasHit)
            {
                if (AttackTimer >= attack.Duration && damageTarget != null)
                {
#if CLIENT
                    bool playSound = LastAttackSoundTime < Timing.TotalTime - SoundInterval;
                    attack.DoDamage(character, damageTarget, WorldPosition, 1.0f, playSound);
                    if (playSound)
                    {
                        LastAttackSoundTime = (float)SoundInterval;
                    }
#else
                    attack.DoDamage(character, damageTarget, WorldPosition, 1.0f, false);
#endif
                }
            }

            Vector2 diff = attackPosition - SimPosition;
            if (diff.LengthSquared() < 0.00001f) return;
            
            if (attack.ApplyForceOnLimbs != null)
            {
                foreach (int limbIndex in attack.ApplyForceOnLimbs)
                {
                    if (limbIndex < 0 || limbIndex >= character.AnimController.Limbs.Length) continue;

                    Limb limb = character.AnimController.Limbs[limbIndex];
                    Vector2 forcePos = limb.pullJoint == null ? limb.body.SimPosition : limb.pullJoint.WorldAnchorA;
                    limb.body.ApplyLinearImpulse(
                        limb.Mass * attack.Force * Vector2.Normalize(attackPosition - SimPosition), forcePos);
                }
            }
            else
            {
                Vector2 forcePos = pullJoint == null ? body.SimPosition : pullJoint.WorldAnchorA;
                body.ApplyLinearImpulse(Mass * attack.Force *
                    Vector2.Normalize(attackPosition - SimPosition), forcePos);
            }

        }
        
        public void Remove()
        {
            if (Sprite != null)
            {
                Sprite.Remove();
                Sprite = null;
            }
            
            if (DamagedSprite != null)
            {
                DamagedSprite.Remove();
                DamagedSprite = null;
            }

            if (DeformSprite != null)
            {
                DeformSprite.Sprite?.Remove();
                DeformSprite = null;
            }

            if (body != null)
            {
                body.Remove();
                body = null;
            }

#if CLIENT
            if (LightSource != null)
            {
                LightSource.Remove();
            }
#endif
        }

        public void LoadParams()
        {
            bool isFlipped = dir == Direction.Left;
            Sprite?.LoadParams(limbParams.normalSpriteParams, isFlipped);
            DamagedSprite?.LoadParams(limbParams.damagedSpriteParams, isFlipped);
            DeformSprite?.Sprite.LoadParams(limbParams.deformSpriteParams, isFlipped);
        }
    }
}
