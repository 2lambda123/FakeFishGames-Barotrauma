﻿using Microsoft.Xna.Framework;
using System;

namespace Barotrauma
{
    partial class HumanAIController : AIController
    {
        public override void DebugDraw(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            if (selectedAiTarget != null)
            {
                GUI.DrawLine(spriteBatch,
                    new Vector2(Character.DrawPosition.X, -Character.DrawPosition.Y),
                    new Vector2(selectedAiTarget.WorldPosition.X, -selectedAiTarget.WorldPosition.Y), Color.Red);
            }

            IndoorsSteeringManager pathSteering = steeringManager as IndoorsSteeringManager;
            if (pathSteering == null || pathSteering.CurrentPath == null || pathSteering.CurrentPath.CurrentNode == null) return;

            GUI.DrawLine(spriteBatch,
                new Vector2(Character.DrawPosition.X, -Character.DrawPosition.Y),
                new Vector2(pathSteering.CurrentPath.CurrentNode.DrawPosition.X, -pathSteering.CurrentPath.CurrentNode.DrawPosition.Y),
                Color.LightGreen);


            for (int i = 1; i < pathSteering.CurrentPath.Nodes.Count; i++)
            {
                GUI.DrawLine(spriteBatch,
                    new Vector2(pathSteering.CurrentPath.Nodes[i].DrawPosition.X, -pathSteering.CurrentPath.Nodes[i].DrawPosition.Y),
                    new Vector2(pathSteering.CurrentPath.Nodes[i - 1].DrawPosition.X, -pathSteering.CurrentPath.Nodes[i - 1].DrawPosition.Y),
                    Color.LightGreen);

                GUI.SmallFont.DrawString(spriteBatch,
                    pathSteering.CurrentPath.Nodes[i].ID.ToString(),
                    new Vector2(pathSteering.CurrentPath.Nodes[i].DrawPosition.X, -pathSteering.CurrentPath.Nodes[i].DrawPosition.Y - 10),
                    Color.LightGreen);
            }
        }
    }
}
