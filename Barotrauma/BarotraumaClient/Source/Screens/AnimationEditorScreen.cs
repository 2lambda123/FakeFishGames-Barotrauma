﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Barotrauma.Extensions;
using FarseerPhysics;

namespace Barotrauma
{
    class AnimationEditorScreen : Screen
    {
        private Camera cam;
        public override Camera Cam
        {
            get
            {
                if (cam == null)
                {
                    cam = new Camera();
                }
                return cam;
            }
        }

        private Character character;
        private Vector2 spawnPosition;
        private bool showWidgets = true;

        public override void Select()
        {
            base.Select();
            Submarine.RefreshSavedSubs();
            Submarine.MainSub = Submarine.SavedSubmarines.First(s => s.Name.Contains("AnimEditor"));
            Submarine.MainSub.Load(true);
            Submarine.MainSub.GodMode = true;
            CalculateMovementLimits();
            character = SpawnCharacter(Character.HumanConfigFile);
            AnimParams.ForEach(p => p.AddToEditor());
            CreateButtons();
        }

        #region Inifinite runner
        private int min;
        private int max;
        private void CalculateMovementLimits()
        {
            min = CurrentWalls.Select(w => w.Rect.Left).OrderBy(p => p).First();
            max = CurrentWalls.Select(w => w.Rect.Right).OrderBy(p => p).Last();
        }

        private List<Structure> _originalWalls;
        private List<Structure> OriginalWalls
        {
            get
            {
                if (_originalWalls == null)
                {
                    _originalWalls = Structure.WallList;
                }
                return _originalWalls;
            }
        }

        private List<Structure> clones = new List<Structure>();
        private List<Structure> previousWalls;

        private List<Structure> _currentWalls;
        private List<Structure> CurrentWalls
        {
            get
            {
                if (_currentWalls == null)
                {
                    _currentWalls = OriginalWalls;
                }
                return _currentWalls;
            }
            set
            {
                _currentWalls = value;
            }
        }

        private void CloneWalls(bool right)
        {
            previousWalls = CurrentWalls;
            if (previousWalls == null)
            {
                previousWalls = OriginalWalls;
            }
            if (clones.None())
            {
                OriginalWalls.ForEachMod(w => clones.Add(w.Clone() as Structure));
                CurrentWalls = clones;
            }
            else
            {
                // Select by position
                var lastWall = right ?
                    previousWalls.OrderBy(w => w.Rect.Right).Last() :
                    previousWalls.OrderBy(w => w.Rect.Left).First();

                CurrentWalls = clones.Contains(lastWall) ? clones : OriginalWalls;
            }
            if (CurrentWalls != OriginalWalls)
            {
                // Move the clones
                for (int i = 0; i < CurrentWalls.Count; i++)
                {
                    int amount = right ? previousWalls[i].Rect.Width : -previousWalls[i].Rect.Width;
                    CurrentWalls[i].Move(new Vector2(amount, 0));
                }
            }
            GameMain.World.ProcessChanges();
            CalculateMovementLimits();
        }
        #endregion

        #region Character spawning
        private int characterIndex = -1;
        private List<string> allFiles;
        private List<string> AllFiles
        {
            get
            {
                if (allFiles == null)
                {
                    allFiles = GameMain.Instance.GetFilesOfType(ContentType.Character).Where(f => !f.Contains("husk")).ToList();
                    allFiles.ForEach(f => DebugConsole.NewMessage(f, Color.White));
                }
                return allFiles;
            }
        }

        private string GetNextConfigFile()
        {
            CheckAndGetIndex();
            IncreaseIndex();
            return AllFiles[characterIndex];
        }

        private string GetPreviousConfigFile()
        {
            CheckAndGetIndex();
            ReduceIndex();
            return AllFiles[characterIndex];
        }

        // Check if the index is not set, in which case we'll get the index from the current species name.
        private void CheckAndGetIndex()
        {
            if (characterIndex == -1)
            {
                characterIndex = AllFiles.IndexOf(GetConfigFile(character.SpeciesName));
            }
        }

        private void IncreaseIndex()
        {
            characterIndex++;
            if (characterIndex > AllFiles.Count - 1)
            {
                characterIndex = 0;
            }
        }

        private void ReduceIndex()
        {
            characterIndex--;
            if (characterIndex < 0)
            {
                characterIndex = AllFiles.Count - 1;
            }
        }

        private string GetConfigFile(string speciesName)
        {
            return AllFiles.Find(c => c.EndsWith(speciesName + ".xml"));
        }

        private Character SpawnCharacter(string configFile)
        {
            DebugConsole.NewMessage($"Trying to spawn {configFile}", Color.HotPink);
            spawnPosition = WayPoint.GetRandom(sub: Submarine.MainSub).WorldPosition;
            var character = Character.Create(configFile, spawnPosition, ToolBox.RandomSeed(8), hasAi: false);
            character.Submarine = Submarine.MainSub;
            character.AnimController.forceStanding = character.IsHumanoid;
            character.dontFollowCursor = true;
            Character.Controlled = character;
            float size = ConvertUnits.ToDisplayUnits(character.AnimController.Collider.radius * 2);
            float margin = 100;
            float distance = Vector2.Distance(spawnPosition, new Vector2(spawnPosition.X, OriginalWalls.First().WorldPosition.Y)) - margin;
            if (size > distance)
            {
                character.AnimController.Teleport(ConvertUnits.ToSimUnits(new Vector2(0, size * 1.5f)), Vector2.Zero);
            }
            GameMain.World.ProcessChanges();
            return character;
        }
        #endregion

        #region GUI
        private GUIFrame panel;
        private GUIButton widgetsButton;
        private void CreateButtons()
        {
            if (panel != null)
            {
                panel.RectTransform.Parent = null;
            }
            panel = new GUIFrame(new RectTransform(new Vector2(0.1f, 0.9f), parent: Frame.RectTransform, anchor: Anchor.CenterRight) { RelativeOffset = new Vector2(0.01f, 0) });
            var layoutGroup = new GUILayoutGroup(new RectTransform(Vector2.One, panel.RectTransform));
            var charButtons = new GUIFrame(new RectTransform(new Vector2(1, 0.1f), parent: layoutGroup.RectTransform), style: null);
            var prevCharacterButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1), charButtons.RectTransform, Anchor.TopLeft), "Previous \nCharacter");
            prevCharacterButton.OnClicked += (b, obj) =>
            {
                character = SpawnCharacter(GetPreviousConfigFile());
                ResetEditor();
                CreateButtons();
                return true;
            };
            var nextCharacterButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1), charButtons.RectTransform, Anchor.TopRight), "Next \nCharacter");
            nextCharacterButton.OnClicked += (b, obj) =>
            {
                character = SpawnCharacter(GetNextConfigFile());
                ResetEditor();
                CreateButtons();
                return true;
            };
            // TODO: use tick boxes?
            widgetsButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), showWidgets ? "Hide Widgets" : "Show Widgets");
            widgetsButton.OnClicked += (b, obj) =>
            {
                showWidgets = !showWidgets;
                widgetsButton.Text = showWidgets ? "Hide Widgets" : "Show Widgets";
                return true;
            };
            var swimButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), character.AnimController.forceStanding ? "Swim" : "Grounded");
            swimButton.OnClicked += (b, obj) =>
            {
                character.AnimController.forceStanding = !character.AnimController.forceStanding;
                swimButton.Text = character.AnimController.forceStanding ? "Swim" : "Grounded";
                return true;
            };
            swimButton.Enabled = character.AnimController.CanWalk;
            var autoMoveButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), character.OverrideMovement.HasValue ? "Disable Auto Move" : "Auto Move");
            autoMoveButton.OnClicked += (b, obj) =>
            {
                character.OverrideMovement = character.OverrideMovement.HasValue ? null : new Vector2(-1, 0) as Vector2?;
                autoMoveButton.Text = character.OverrideMovement.HasValue ? "Disable Auto Move" : "Auto Move";
                return true;
            };
            var speedButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), character.ForceRun ? "Slow" : "Fast");
            speedButton.OnClicked += (b, obj) =>
            {
                character.ForceRun = !character.ForceRun;
                speedButton.Text = character.ForceRun ? "Slow" : "Fast";
                return true;
            };
            var followCursorButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), character.dontFollowCursor ? "Follow Cursor" : "Don't Follow Cursor");
            followCursorButton.OnClicked += (b, obj) =>
            {
                character.dontFollowCursor = !character.dontFollowCursor;
                followCursorButton.Text = character.dontFollowCursor ? "Follow Cursor" : "Don't Follow Cursor";
                return true;
            };
            var saveButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), "Save");
            saveButton.OnClicked += (b, obj) =>
            {
                AnimParams.ForEach(p => p.Save());
                return true;
            };
            var resetButton = new GUIButton(new RectTransform(new Vector2(1, 0.1f), layoutGroup.RectTransform), "Reset");
            resetButton.OnClicked += (b, obj) =>
            {
                AnimParams.ForEach(p => p.Reset());
                ResetEditor();
                return true;
            };
        }
        #endregion

        #region AnimParams
        private List<AnimationParams> AnimParams => character.AnimController.AllAnimParams;

        private void ResetEditor()
        {
            AnimationParams.CreateEditor();
            AnimParams.ForEach(p => p.AddToEditor());
        }
        #endregion

        public override void AddToGUIUpdateList()
        {
            base.AddToGUIUpdateList();
            AnimationParams.Editor.AddToGUIUpdateList();
        }

        public override void Update(double deltaTime)
        {
            base.Update(deltaTime);

            Submarine.MainSub.SetPrevTransform(Submarine.MainSub.Position);
            Submarine.MainSub.Update((float)deltaTime);

            PhysicsBody.List.ForEach(pb => pb.SetPrevTransform(pb.SimPosition, pb.Rotation));

            character.ControlLocalPlayer((float)deltaTime, Cam, false);
            character.Control((float)deltaTime, Cam);
            character.AnimController.UpdateAnim((float)deltaTime);
            character.AnimController.Update((float)deltaTime, Cam);

            if (character.Position.X < min)
            {
                CloneWalls(false);
            }
            else if (character.Position.X > max)
            {
                CloneWalls(true);
            }

            //Cam.TargetPos = Vector2.Zero;
            Cam.MoveCamera((float)deltaTime, allowMove: false, allowZoom: GUI.MouseOn == null);
            Cam.Position = character.Position;
 
            GameMain.World.Step((float)deltaTime);
        }

        public override void Draw(double deltaTime, GraphicsDevice graphics, SpriteBatch spriteBatch)
        {
            base.Draw(deltaTime, graphics, spriteBatch);
            graphics.Clear(Color.CornflowerBlue);
            Cam.UpdateTransform(true);

            // Submarine
            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, transformMatrix: Cam.Transform);
            Submarine.Draw(spriteBatch, true);
            spriteBatch.End();

            // Character
            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, transformMatrix: Cam.Transform);
            character.Draw(spriteBatch);
            spriteBatch.End();

            // GUI
            spriteBatch.Begin(SpriteSortMode.Immediate, rasterizerState: GameMain.ScissorTestEnable);
            Structure wall = clones.FirstOrDefault();
            Vector2 indicatorPos = wall == null ? OriginalWalls.First().DrawPosition : wall.DrawPosition;
            GUI.DrawIndicator(spriteBatch, indicatorPos, Cam, 700, GUI.SubmarineIcon, Color.White);
            GUI.Draw((float)deltaTime, spriteBatch);

            if (showWidgets)
            {
                DrawWidgetEditor(spriteBatch);
            }
            //DrawJointEditor(spriteBatch);

            // Debug
            if (GameMain.DebugDraw)
            {
                // Limb positions
                foreach (Limb limb in character.AnimController.Limbs)
                {
                    Vector2 limbDrawPos = Cam.WorldToScreen(limb.WorldPosition);
                    GUI.DrawLine(spriteBatch, limbDrawPos + Vector2.UnitY * 5.0f, limbDrawPos - Vector2.UnitY * 5.0f, Color.White);
                    GUI.DrawLine(spriteBatch, limbDrawPos + Vector2.UnitX * 5.0f, limbDrawPos - Vector2.UnitX * 5.0f, Color.White);
                }

                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 0), $"Cursor World Pos: {character.CursorWorldPosition}", Color.White, font: GUI.SmallFont);
                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 20), $"Cursor Pos: {character.CursorPosition}", Color.White, font: GUI.SmallFont);
                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 40), $"Cursor Screen Pos: {PlayerInput.MousePosition}", Color.White, font: GUI.SmallFont);


                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 80), $"Character World Pos: {character.WorldPosition}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 100), $"Character Pos: {character.Position}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 120), $"Character Sim Pos: {character.SimPosition}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 140), $"Character Draw Pos: {character.DrawPosition}", Color.White, font: GUI.SmallFont);


                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 180), $"Submarine World Pos: {Submarine.MainSub.WorldPosition}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 200), $"Submarine Pos: {Submarine.MainSub.Position}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 220), $"Submarine Sim Pos: {Submarine.MainSub.Position}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 240), $"Submarine Draw Pos: {Submarine.MainSub.DrawPosition}", Color.White, font: GUI.SmallFont);

                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 280), $"Movement Limits: MIN: {min} MAX: {max}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 300), $"Clones: {clones.Count}", Color.White, font: GUI.SmallFont);
                //GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 320), $"Total amount of walls: {Structure.WallList.Count}", Color.White, font: GUI.SmallFont);

                // Collider
                var collider = character.AnimController.Collider;
                var colliderDrawPos = SimToScreenPoint(collider.SimPosition);
                Vector2 forward = Vector2.Transform(Vector2.UnitY, Matrix.CreateRotationZ(collider.Rotation));
                var endPos = SimToScreenPoint(collider.SimPosition + Vector2.Normalize(forward) * collider.radius);
                //Vector2 forward = VectorExtensions.Forward(collider.Rotation, 1);
                //var endPos = SimToScreenPoint(collider.SimPosition) - forward * ConvertUnits.ToDisplayUnits(collider.radius);
                GUI.DrawLine(spriteBatch, colliderDrawPos, endPos, Color.LightGreen);
                ShapeExtensions.DrawCircle(spriteBatch, colliderDrawPos, (endPos - colliderDrawPos).Length(), 40, Color.LightGreen);
                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth - 300, 0), $"Collider rotation: {MathHelper.ToDegrees(MathUtils.WrapAngleTwoPi(collider.Rotation))}", Color.White, font: GUI.SmallFont);
            }
            spriteBatch.End();
        }

        #region Widgets
        private Vector2 ScreenToSimPoint(float x, float y) => ScreenToSimPoint(new Vector2(x, y));
        private Vector2 ScreenToSimPoint(Vector2 p) => ConvertUnits.ToSimUnits(Cam.ScreenToWorld(p));
        private Vector2 SimToScreenPoint(float x, float y) => SimToScreenPoint(new Vector2(x, y));
        private Vector2 SimToScreenPoint(Vector2 p) => Cam.WorldToScreen(ConvertUnits.ToDisplayUnits(p));

        private void DrawWidgetEditor(SpriteBatch spriteBatch)
        {
            var collider = character.AnimController.Collider;
            var charDrawPos = SimToScreenPoint(collider.SimPosition);
            var animParams = character.AnimController.CurrentAnimationParams;
            var groundedParams = animParams as GroundedMovementParams;
            var humanGroundedParams = animParams as HumanGroundedParams;
            var fishGroundedParams = animParams as FishGroundedParams;
            var fishSwimParams = animParams as FishSwimParams;
            var humanSwimParams = animParams as HumanSwimParams;
            var head = character.AnimController.GetLimb(LimbType.Head);
            var torso = character.AnimController.GetLimb(LimbType.Torso);
            var tail = character.AnimController.GetLimb(LimbType.Tail);
            var legs = character.AnimController.GetLimb(LimbType.Legs);
            var thigh = character.AnimController.GetLimb(LimbType.RightThigh) ?? character.AnimController.GetLimb(LimbType.RightThigh);
            var foot = character.AnimController.GetLimb(LimbType.RightFoot) ?? character.AnimController.GetLimb(LimbType.LeftFoot);
            var hand = character.AnimController.GetLimb(LimbType.RightHand) ?? character.AnimController.GetLimb(LimbType.LeftHand);
            var arm = character.AnimController.GetLimb(LimbType.RightArm) ?? character.AnimController.GetLimb(LimbType.LeftArm);
            int widgetDefaultSize = 10;
            Vector2 colliderBottom = character.AnimController.GetColliderBottom();
            Vector2 centerOfMass = character.AnimController.GetCenterOfMass();
            // TODO: solve collider does not always point forward
            Vector2 simSpaceForward = Vector2.Normalize(Vector2.Transform(Vector2.UnitY, Matrix.CreateRotationZ(collider.Rotation)));
            //Vector2 simSpaceLeft = Vector2.Normalize(Vector2.Transform(-Vector2.UnitX, Matrix.CreateRotationZ(collider.Rotation)));
            Vector2 screenSpaceForward = -VectorExtensions.Forward(collider.Rotation, 1);
            Vector2 screenSpaceLeft = -VectorExtensions.Forward(MathHelper.ToDegrees(MathUtils.WrapAngleTwoPi(collider.Rotation)) + MathHelper.ToRadians(90), 1);
            Vector2 forward = animParams.IsSwimAnimation ? screenSpaceForward : screenSpaceLeft;
            float dir = character.AnimController.Dir;

            // Widgets for all anims -->
            // Speed
            float multiplier = 0.02f;
            Vector2 referencePoint = SimToScreenPoint(collider.SimPosition);
            Vector2 drawPos = referencePoint;
            drawPos += forward * animParams.Speed / multiplier;
            DrawWidget(spriteBatch, drawPos, WidgetType.Circle, 20, Color.Turquoise, "Speed", () =>
            {
                TryUpdateValue("speed", MathHelper.Clamp(animParams.Speed + Vector2.Multiply(PlayerInput.MouseSpeed, forward).Combine() * multiplier, 0.1f, 6));
                GUI.DrawLine(spriteBatch, drawPos, referencePoint, Color.Turquoise);
            });
            GUI.DrawLine(spriteBatch, drawPos + forward * 10, drawPos + forward * 15, Color.Turquoise);
            if (head != null)
            {
                // Head angle
                DrawCircularWidget(spriteBatch, SimToScreenPoint(head.SimPosition), animParams.HeadAngle, "Head Angle", Color.White, 
                    angle => TryUpdateValue("headangle", angle), circleRadius: 25, rotationOffset: collider.Rotation, clockWise: dir < 0);
                // Head position and leaning
                if (animParams.IsGroundedAnimation)
                {
                    if (humanGroundedParams != null)
                    {
                        var widgetDrawPos = SimToScreenPoint(head.SimPosition.X - humanGroundedParams.HeadLeanAmount, head.pullJoint.WorldAnchorB.Y);
                        DrawWidget(spriteBatch, widgetDrawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Red, "Head", () =>
                        {
                            TryUpdateValue("headleanamount", humanGroundedParams.HeadLeanAmount + 0.01f * -PlayerInput.MouseSpeed.X);
                            TryUpdateValue("headposition", humanGroundedParams.HeadPosition + 0.015f * -PlayerInput.MouseSpeed.Y);
                        });
                        var origin = widgetDrawPos - new Vector2(widgetDefaultSize / 2, 0);
                        GUI.DrawLine(spriteBatch, origin, origin - Vector2.UnitX * 5, Color.Red);
                    }
                    else
                    {
                        drawPos = SimToScreenPoint(head.SimPosition.X, head.pullJoint.WorldAnchorB.Y);
                        DrawWidget(spriteBatch, drawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Red, "Head Position",
                            () => TryUpdateValue("headposition", groundedParams.HeadPosition + 0.015f * -PlayerInput.MouseSpeed.Y));
                    }
                }
            }
            if (torso != null)
            {
                // Torso angle
                DrawCircularWidget(spriteBatch, SimToScreenPoint(torso.SimPosition), animParams.TorsoAngle, "Torso Angle", Color.White, 
                    angle => TryUpdateValue("torsoangle", angle), rotationOffset: collider.Rotation, clockWise: dir < 0);

                if (animParams.IsGroundedAnimation)
                {
                    // Torso position and leaning
                    if (humanGroundedParams != null)
                    {
                        drawPos = SimToScreenPoint(torso.SimPosition.X - humanGroundedParams.TorsoLeanAmount, torso.SimPosition.Y);
                        DrawWidget(spriteBatch, drawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Red, "Torso", () =>
                        {
                            TryUpdateValue("torsoleanamount", humanGroundedParams.TorsoLeanAmount + 0.01f * -PlayerInput.MouseSpeed.X);
                            TryUpdateValue("torsoposition", humanGroundedParams.TorsoPosition + 0.015f * -PlayerInput.MouseSpeed.Y);
                        });
                        var origin = drawPos - new Vector2(widgetDefaultSize / 2, 0);
                        GUI.DrawLine(spriteBatch, origin, origin - Vector2.UnitX * 5, Color.Red);
                    }
                    else
                    {
                        drawPos = SimToScreenPoint(torso.SimPosition.X, torso.pullJoint.WorldAnchorB.Y);
                        DrawWidget(spriteBatch, drawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Red, "Torso Position",
                            () => TryUpdateValue("torsoposition", groundedParams.TorsoPosition + 0.015f * -PlayerInput.MouseSpeed.Y));
                    }
                }
            }
            if (foot != null)
            {
                // Fish grounded only
                if (fishGroundedParams != null)
                {
                    DrawCircularWidget(spriteBatch, SimToScreenPoint(colliderBottom), fishGroundedParams.FootRotation, "Foot Rotation", Color.White,
                        angle => TryUpdateValue("footrotation", angle), circleRadius: 25, rotationOffset: collider.Rotation, clockWise: dir < 0);
                }
                // Both
                if (groundedParams != null)
                {
                    multiplier = 0.005f;
                    referencePoint = SimToScreenPoint(colliderBottom);
                    drawPos = referencePoint - groundedParams.StepSize / multiplier;
                    var origin = drawPos - new Vector2(widgetDefaultSize / 2, 0);
                    DrawWidget(spriteBatch, drawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Blue, "Step Size", () =>
                    {
                        TryUpdateValue("stepsize", groundedParams.StepSize -PlayerInput.MouseSpeed * multiplier);
                        GUI.DrawLine(spriteBatch, origin, referencePoint, Color.Blue);
                    });
                    GUI.DrawLine(spriteBatch, origin, origin - Vector2.UnitX * 5, Color.Blue);
                }
            }
            // Human grounded only -->
            if (humanGroundedParams != null)
            {
                if (legs != null || foot != null)
                {
                    multiplier = 10;
                    drawPos = SimToScreenPoint(colliderBottom + simSpaceForward * 0.3f);
                    DrawCircularWidget(spriteBatch, drawPos, humanGroundedParams.LegCorrectionTorque * multiplier, "Leg Correction Torque", Color.Chartreuse, angle =>
                    {
                        TryUpdateValue("legcorrectiontorque", angle / multiplier);
                        GUI.DrawString(spriteBatch, drawPos, humanGroundedParams.LegCorrectionTorque.FormatAsSingleDecimal(), Color.Black, Color.Chartreuse, font: GUI.SmallFont);
                    },circleRadius: 25, rotationOffset: collider.Rotation, clockWise: dir < 0, displayAngle: false);
                }
                if (hand != null || arm != null)
                {
                    multiplier = 0.02f;
                    referencePoint = SimToScreenPoint(character.SimPosition) + Vector2.UnitX;
                    drawPos = referencePoint - humanGroundedParams.HandMoveAmount / multiplier;
                    var origin = drawPos - new Vector2(widgetDefaultSize / 2, 0);
                    DrawWidget(spriteBatch, drawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Blue, "Hand Move Amount", () =>
                    {
                        TryUpdateValue("handmoveamount", humanGroundedParams.HandMoveAmount - PlayerInput.MouseSpeed * multiplier);
                        GUI.DrawLine(spriteBatch, origin, referencePoint, Color.Blue);
                    });
                    GUI.DrawLine(spriteBatch, origin, origin - Vector2.UnitX * 5, Color.Blue);
                }
            }
            // Fish swim only -->
            else if (tail != null && fishSwimParams != null)
            {
                float lengthMultiplier = 0.1f;
                float amplitudeMultiplier = 0.01f;
                referencePoint = charDrawPos - screenSpaceForward * ConvertUnits.ToDisplayUnits(collider.radius) * 2;
                // TODO: the widget is at the wrong position when moving vertically
                Vector2 widgetDrawPos = referencePoint + new Vector2(
                    fishSwimParams.WaveLength / lengthMultiplier * -screenSpaceForward.X,
                    fishSwimParams.WaveAmplitude / amplitudeMultiplier * -screenSpaceLeft.Y * dir);
                GUI.DrawString(spriteBatch, new Vector2(GameMain.GraphicsWidth / 2, 0), widgetDrawPos.ToString(), Color.White);
                DrawWidget(spriteBatch, widgetDrawPos, WidgetType.Rectangle, widgetDefaultSize, Color.Red, "Tail", () =>
                {
                    TryUpdateValue("wavelength", fishSwimParams.WaveLength - Vector2.Multiply(PlayerInput.MouseSpeed, screenSpaceForward).Combine() * lengthMultiplier);
                    TryUpdateValue("waveamplitude", fishSwimParams.WaveAmplitude + Vector2.Multiply(PlayerInput.MouseSpeed, screenSpaceLeft * -dir).Combine() * amplitudeMultiplier);
                    GUI.DrawLine(spriteBatch, referencePoint, widgetDrawPos, Color.Red);
                });
                GUI.DrawLine(spriteBatch, referencePoint, widgetDrawPos, Color.Red);
            }
            // Human swim only -->
            else if (humanSwimParams != null)
            {
                multiplier = 100;
                drawPos = SimToScreenPoint(character.SimPosition - simSpaceForward / 2);
                DrawCircularWidget(spriteBatch, drawPos, humanSwimParams.LegMoveAmount * multiplier, "Leg Movement", Color.Chartreuse, amount =>
                {
                    TryUpdateValue("legmoveamount", amount / multiplier);
                    GUI.DrawString(spriteBatch, drawPos, humanSwimParams.LegMoveAmount.FormatAsSingleDecimal(), Color.Black, Color.Chartreuse, font: GUI.SmallFont);
                }, circleRadius: 25, rotationOffset: collider.Rotation, clockWise: true, displayAngle: false);
            }
        }

        private void TryUpdateValue(string name, object value)
        {
            var animParams = character.AnimController.CurrentAnimationParams;
            if (animParams.SerializableProperties.TryGetValue(name, out SerializableProperty p))
            {
                UpdateValue(p, animParams, value);
            }
        }

        /// <summary>
        /// Note: currently only handles floats and vector2s.
        /// </summary>
        private void UpdateValue(SerializableProperty property, AnimationParams animationParams, object newValue)
        {
            if (!animationParams.SerializableEntityEditor.Fields.TryGetValue(property, out GUIComponent[] fields))
            {
                return;
            }
            if (newValue is float f)
            {
                foreach (var field in fields)
                {
                    if (field is GUINumberInput numInput)
                    {
                        if (numInput.InputType == GUINumberInput.NumberType.Float)
                        {
                            numInput.FloatValue = f;
                        }
                    }
                }
            }
            else if (newValue is Vector2 v)
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    if (field is GUINumberInput numInput)
                    {
                        if (numInput .InputType == GUINumberInput.NumberType.Float)
                        {
                            numInput.FloatValue = i == 0 ? v.X : v.Y;
                        }
                    }
                }
            }
        }

        private void DrawCircularWidget(SpriteBatch spriteBatch, Vector2 drawPos, float value, string toolTip, Color color, Action<float> onClick, 
            float circleRadius = 30, int widgetSize = 10, float rotationOffset = 0, bool clockWise = true, bool displayAngle = true)
        {
            var angle = value;
            if (!MathUtils.IsValid(angle))
            {
                angle = 0;
            }
            var forward = VectorExtensions.Forward(rotationOffset, circleRadius);
            var widgetDrawPos = drawPos - forward;
            widgetDrawPos = MathUtils.RotatePointAroundTarget(widgetDrawPos, drawPos, angle, clockWise);
            GUI.DrawLine(spriteBatch, drawPos, widgetDrawPos, color);
            DrawWidget(spriteBatch, widgetDrawPos, WidgetType.Rectangle, 10, color, toolTip, () =>
            {
                GUI.DrawLine(spriteBatch, drawPos, drawPos - forward, Color.Red);
                ShapeExtensions.DrawCircle(spriteBatch, drawPos, circleRadius, 40, color, thickness: 1);
                float x = PlayerInput.MouseSpeed.X * 1.5f;
                float y = PlayerInput.MouseSpeed.Y * 1.5f;
                var transformedRot = angle + MathHelper.ToDegrees(-(float)Math.Atan2(forward.X, forward.Y));
                if (!clockWise)
                {
                    // TODO: replace this hack with a proper calculation (doesn't work when swimming diagonally toward south east or north east)
                    var rotationOffsetInDegrees = MathHelper.ToDegrees(MathUtils.WrapAngleTwoPi(rotationOffset));
                    //GUI.DrawString(spriteBatch, drawPos + Vector2.UnitX * 30, rotationOffsetInDegrees.FormatAsInt(), Color.White);
                    if (rotationOffsetInDegrees > 180 && rotationOffsetInDegrees < 359)
                    {
                        y = -y;
                    }
                    if (rotationOffsetInDegrees > 270 || rotationOffsetInDegrees < 90)
                    {
                        x = -x;
                    }
                }
                if ((transformedRot > 90 && transformedRot < 270) || (transformedRot < -90 && transformedRot > -270))
                {
                    x = -x;
                }
                if (transformedRot > 180 || (transformedRot < 0 && transformedRot > -180))
                {
                    y = -y;
                }
                angle += x + y;
                if (angle > 360 || angle < -360)
                {
                    angle = 0;
                }
                if (displayAngle)
                {
                    GUI.DrawString(spriteBatch, drawPos, angle.FormatAsInt(), Color.Black, backgroundColor: color, font: GUI.SmallFont);
                }
                onClick(angle);
            });
        }

        public enum WidgetType { Rectangle, Circle }
        private string selectedWidget;
        private void DrawWidget(SpriteBatch spriteBatch, Vector2 drawPos, WidgetType widgetType, int size, Color color, string name, Action onPressed)
        {
            var drawRect = new Rectangle((int)drawPos.X - size / 2, (int)drawPos.Y - size / 2, size, size);
            var inputRect = drawRect;
            inputRect.Inflate(size, size);
            bool isMouseOn = inputRect.Contains(PlayerInput.MousePosition);
            // Unselect
            if (!isMouseOn && selectedWidget == name)
            {
                selectedWidget = null;
            }
            bool isSelected = isMouseOn && (selectedWidget == null || selectedWidget == name);
            switch (widgetType)
            {
                case WidgetType.Rectangle:
                    GUI.DrawRectangle(spriteBatch, drawRect, color, false, thickness: isSelected ? 3 : 1);
                    break;
                case WidgetType.Circle:
                    ShapeExtensions.DrawCircle(spriteBatch, drawPos, size / 2, 40, color, thickness: isSelected ? 3 : 1);
                    break;
                default: throw new NotImplementedException(widgetType.ToString());
            }
            if (isSelected)
            {
                selectedWidget = name;
                // Label/tooltip
                GUI.DrawString(spriteBatch, new Vector2(drawRect.Right + 5, drawRect.Y - drawRect.Height / 2), name, Color.White, Color.Black * 0.5f);
                if (PlayerInput.LeftButtonHeld())
                {
                    onPressed();
                }
            }
            // Bezier test
            //if (PlayerInput.LeftButtonHeld())
            //{
            //    Vector2 start = drawPoint.ToVector2();
            //    Vector2 end = start + new Vector2(50, 0);
            //    Vector2 dir = end - start;
            //    Vector2 control = start + dir / 2 + new Vector2(0, -20);
            //    var points = new Vector2[10];
            //    for (int i = 0; i < points.Length; i++)
            //    {
            //        float t = (float)i / (points.Length - 1);
            //        Vector2 pos = MathUtils.Bezier(start, control, end, t);
            //        points[i] = pos;
            //        //DebugConsole.NewMessage(i.ToString(), Color.White);
            //        //DebugConsole.NewMessage(t.ToString(), Color.Blue);
            //        //DebugConsole.NewMessage(pos.ToString(), Color.Red);
            //        ShapeExtensions.DrawPoint(spriteBatch, pos, Color.White, size: 2);
            //    }
            //}
        }
        #endregion

        #region Joint edit (test)
        private void DrawJointEditor(SpriteBatch spriteBatch)
        {
            foreach (Limb limb in character.AnimController.Limbs)
            {
                Vector2 limbBodyPos = Cam.WorldToScreen(limb.WorldPosition);
                GUI.DrawRectangle(spriteBatch, new Rectangle(limbBodyPos.ToPoint(), new Point(5, 5)), Color.Red);

                DrawJoints(spriteBatch, limb, limbBodyPos);

                GUI.DrawLine(spriteBatch, limbBodyPos + Vector2.UnitY * 5.0f, limbBodyPos - Vector2.UnitY * 5.0f, Color.White);
                GUI.DrawLine(spriteBatch, limbBodyPos + Vector2.UnitX * 5.0f, limbBodyPos - Vector2.UnitX * 5.0f, Color.White);

                if (Vector2.Distance(PlayerInput.MousePosition, limbBodyPos) < 5.0f && PlayerInput.LeftButtonHeld())
                {
                    limb.sprite.Origin += PlayerInput.MouseSpeed;
                }
            }
        }

        private void DrawJoints(SpriteBatch spriteBatch, Limb limb, Vector2 limbBodyPos)
        {
            foreach (var joint in character.AnimController.LimbJoints)
            {
                Vector2 jointPos = Vector2.Zero;

                if (joint.BodyA == limb.body.FarseerBody)
                {
                    jointPos = ConvertUnits.ToDisplayUnits(joint.LocalAnchorA);

                }
                else if (joint.BodyB == limb.body.FarseerBody)
                {
                    jointPos = ConvertUnits.ToDisplayUnits(joint.LocalAnchorB);
                }
                else
                {
                    continue;
                }

                Vector2 tformedJointPos = jointPos /= limb.Scale;
                tformedJointPos.Y = -tformedJointPos.Y;
                tformedJointPos += limbBodyPos;

                if (joint.BodyA == limb.body.FarseerBody)
                {
                    float a1 = joint.UpperLimit - MathHelper.PiOver2;
                    float a2 = joint.LowerLimit - MathHelper.PiOver2;
                    float a3 = (a1 + a2) / 2.0f;
                    GUI.DrawLine(spriteBatch, tformedJointPos, tformedJointPos + new Vector2((float)Math.Cos(a1), -(float)Math.Sin(a1)) * 30.0f, Color.Green);
                    GUI.DrawLine(spriteBatch, tformedJointPos, tformedJointPos + new Vector2((float)Math.Cos(a2), -(float)Math.Sin(a2)) * 30.0f, Color.DarkGreen);

                    GUI.DrawLine(spriteBatch, tformedJointPos, tformedJointPos + new Vector2((float)Math.Cos(a3), -(float)Math.Sin(a3)) * 30.0f, Color.LightGray);
                }

                GUI.DrawRectangle(spriteBatch, tformedJointPos, new Vector2(5.0f, 5.0f), Color.Red, true);
                if (Vector2.Distance(PlayerInput.MousePosition, tformedJointPos) < 10.0f)
                {
                    GUI.DrawString(spriteBatch, tformedJointPos + Vector2.One * 10.0f, jointPos.ToString(), Color.White, Color.Black * 0.5f);
                    GUI.DrawRectangle(spriteBatch, tformedJointPos - new Vector2(3.0f, 3.0f), new Vector2(11.0f, 11.0f), Color.Red, false);
                    if (PlayerInput.LeftButtonHeld())
                    {
                        Vector2 speed = ConvertUnits.ToSimUnits(PlayerInput.MouseSpeed);
                        speed.Y = -speed.Y;
                        if (joint.BodyA == limb.body.FarseerBody)
                        {
                            joint.LocalAnchorA += speed;
                        }
                        else
                        {
                            joint.LocalAnchorB += speed;
                        }
                    }
                }
            }
        }
        #endregion
    }
}
