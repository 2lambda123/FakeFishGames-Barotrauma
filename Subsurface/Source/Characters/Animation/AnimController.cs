﻿using System.Xml.Linq;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Barotrauma
{
    class AnimController : Ragdoll
    {
        public enum Animation { None, Climbing, UsingConstruction, Struggle, CPR };
        public Animation Anim;

        protected Character character;

        protected float walkSpeed, swimSpeed;  
        
        protected float stunTimer;
        
        protected float walkPos;

        protected readonly Vector2 stepSize;
        protected readonly float legTorque;



        public float StunTimer
        {
            get { return stunTimer; }
            set 
            {
                if (!MathUtils.IsValid(value)) return;
                stunTimer = value; 
            }
        }
        
        public AnimController(Character character, XElement element)
            : base(character, element)
        {
            this.character = character;

            stepSize = ToolBox.GetAttributeVector2(element, "stepsize", Vector2.One);
            stepSize = ConvertUnits.ToSimUnits(stepSize);

            walkSpeed = ToolBox.GetAttributeFloat(element, "walkspeed", 1.0f);
            swimSpeed = ToolBox.GetAttributeFloat(element, "swimspeed", 1.0f);

            legTorque = ToolBox.GetAttributeFloat(element, "legtorque", 0.0f);
        }

        public virtual void UpdateAnim(float deltaTime) { }

        public virtual void HoldItem(float deltaTime, Item item, Vector2[] handlePos, Vector2 holdPos, Vector2 aimPos, bool aim, float holdAngle) { }

        public virtual void DragCharacter(Character target, LimbType rightHandTarget = LimbType.RightHand, LimbType leftHandTarget = LimbType.LeftHand) { }


   }
}
