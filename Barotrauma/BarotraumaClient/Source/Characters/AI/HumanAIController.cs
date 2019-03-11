﻿using Microsoft.Xna.Framework;
using FarseerPhysics;

namespace Barotrauma
{
    partial class HumanAIController : AIController
    {
        partial void InitProjSpecific()
        {
            /*if (GameMain.GameSession != null && GameMain.GameSession.CrewManager != null)
            {
                CurrentOrder = Order.PrefabList.Find(o => o.AITag == "dismissed");
                objectiveManager.SetOrder(CurrentOrder, "", null);
                GameMain.GameSession.CrewManager.SetCharacterOrder(Character, CurrentOrder, null, null);
            }*/
        }

        partial void SetOrderProjSpecific(Order order)
        {
            GameMain.GameSession.CrewManager.DisplayCharacterOrder(Character, order);
        }

        public override void DebugDraw(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            Vector2 pos = Character.WorldPosition;
            pos.Y = -pos.Y;
            Vector2 textOffset = new Vector2(-40, -120);

            if (SelectedAiTarget?.Entity != null)
            {
                //GUI.DrawLine(spriteBatch, pos, new Vector2(SelectedAiTarget.WorldPosition.X, -SelectedAiTarget.WorldPosition.Y), Color.Red);
                //GUI.DrawString(spriteBatch, pos + textOffset, $"AI TARGET: {SelectedAiTarget.Entity.ToString()}", Color.White, Color.Black);
            }

            if (ObjectiveManager != null)
            {
                var currentOrder = ObjectiveManager.CurrentOrder;
                if (currentOrder != null)
                {
                    GUI.DrawString(spriteBatch, pos + textOffset, $"ORDER: {currentOrder.DebugTag} ({currentOrder.GetPriority(ObjectiveManager).FormatZeroDecimal()})", Color.White, Color.Black);
                }
                var currentObjective = ObjectiveManager.CurrentObjective;
                if (currentObjective != null)
                {
                    GUI.DrawString(spriteBatch, pos + textOffset + new Vector2(0, 20), $"OBJECTIVE: {currentObjective.DebugTag} ({currentObjective.GetPriority(ObjectiveManager).FormatZeroDecimal()})", Color.White, Color.Black);
                    var subObjective = currentObjective.CurrentSubObjective;
                    if (subObjective != null)
                    {
                        GUI.DrawString(spriteBatch, pos + textOffset + new Vector2(0, 40), $"SUBOBJECTIVE: {subObjective.DebugTag} ({subObjective.GetPriority(ObjectiveManager).FormatZeroDecimal()})", Color.White, Color.Black);
                    }
                }
            }

            if (steeringManager is IndoorsSteeringManager pathSteering)
            {
                var path = pathSteering.CurrentPath;
                if (path != null)
                {
                    if (path.CurrentNode != null)
                    {
                        GUI.DrawLine(spriteBatch, pos,
                            new Vector2(path.CurrentNode.DrawPosition.X, -path.CurrentNode.DrawPosition.Y),
                            Color.BlueViolet, 0, 3);

                        GUI.DrawString(spriteBatch, pos + textOffset - new Vector2(0, 20), "Path cost: " + path.Cost.FormatZeroDecimal(), Color.White, Color.Black * 0.5f);
                    }
                    for (int i = 1; i < path.Nodes.Count; i++)
                    {
                        var previousNode = path.Nodes[i - 1];
                        var currentNode = path.Nodes[i];
                        GUI.DrawLine(spriteBatch,
                            new Vector2(currentNode.DrawPosition.X, -currentNode.DrawPosition.Y),
                            new Vector2(previousNode.DrawPosition.X, -previousNode.DrawPosition.Y),
                            Color.Blue * 0.5f, 0, 3);

                        GUI.SmallFont.DrawString(spriteBatch,
                            currentNode.ID.ToString(),
                            new Vector2(currentNode.DrawPosition.X, -currentNode.DrawPosition.Y - 10),
                            Color.LightGreen);
                    }
                }
            }
            GUI.DrawLine(spriteBatch, pos, pos + ConvertUnits.ToDisplayUnits(new Vector2(Steering.X, -Steering.Y)), Color.Blue, width: 3);

            //if (Character.IsKeyDown(InputType.Aim))
            //{
            //    GUI.DrawLine(spriteBatch, pos, new Vector2(Character.CursorWorldPosition.X, -Character.CursorWorldPosition.Y), Color.Yellow, width: 4);
            //}
        }
    }
}
