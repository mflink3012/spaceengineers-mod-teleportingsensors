using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using MyDetectedEntityType = Sandbox.ModAPI.Ingame.MyDetectedEntityType;
using MyDetectedEntityInfo = Sandbox.ModAPI.Ingame.MyDetectedEntityInfo;

namespace SpaceEngineers.Mod.TeleportingSensor
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SensorBlock), true)]
    public class LogicComponent : MyGameLogicComponent
    {
        public static string PROPERTY_PREFIX = "teleporter.";
        public static string PROPERTY_KEY_NAME = $"{PROPERTY_PREFIX}name";
        public static string PROPERTY_KEY_TARGET = $"{PROPERTY_PREFIX}target";
        public static string PROPERTY_KEY_OFFSET = $"{PROPERTY_PREFIX}offset";
        public static string  PROPERTY_KEY_DEBUG = $"{PROPERTY_PREFIX}debug";

        public static Dictionary<String, LogicComponent> TeleporterNetwork = new Dictionary<string, LogicComponent>();
        public bool Valid = false;

        public static Vector3D DEFAULT_OFFSET = Vector3D.Zero; //new Vector3D(0);

        public string Name;
        public string Target;
        public Vector3D Offset = DEFAULT_OFFSET;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Init(true);
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation100()
        {
            Init();

            IMySensorBlock sensorBlock = Entity as IMySensorBlock;

            if (Target != null && sensorBlock.IsActive && TeleporterNetwork.ContainsKey(Target))
            {
                PrintDebugMessage($"'{sensorBlock.DisplayNameText}' is an active teleporter to '{Target}'.");
                List<MyDetectedEntityInfo> detectedEntityInfos = new List<MyDetectedEntityInfo>();
                sensorBlock.DetectedEntities(detectedEntityInfos);
                List<IMyPlayer> players = new List<IMyPlayer>();

                foreach (MyDetectedEntityInfo entityInfo in detectedEntityInfos)
                {
                    if (entityInfo.Type == MyDetectedEntityType.CharacterHuman) {
                        MyAPIGateway.Players.GetPlayers(players, p => p.Character.EntityId == entityInfo.EntityId);
                        IMyPlayer player = players[0];

                        PrintDebugMessage($"'{sensorBlock.DisplayNameText}' detected '{player.DisplayName}' and will teleport it to '{TeleporterNetwork[Target].Name}'.");
                        Teleport(player.Character, TeleporterNetwork[Target]);
                    }
                }
            }
        }

        private void Init(bool firstTime = false)
        {
            IMySensorBlock sensorBlock = Entity as IMySensorBlock;

            if (sensorBlock.CustomData.Contains($"{PROPERTY_KEY_NAME}="))
            {
                Dictionary<string, string> props = ReadProperties(sensorBlock.CustomData);

                if (!props.ContainsKey(PROPERTY_KEY_NAME)) {
                    PrintDebugMessage($"'{sensorBlock.DisplayNameText}' has '{PROPERTY_KEY_NAME}=' in Custom Data, but it can not be used/read.");
                    return;
                }

                // Idea: Check for changed name and update the teleporter-network automatically ?!
                Name = props[PROPERTY_KEY_NAME];

                if (props.ContainsKey(PROPERTY_KEY_OFFSET))
                {
                    string[] coordStrs = props[PROPERTY_KEY_OFFSET].Split(',');
                    try
                    {
                        Offset = ParseVector3D(coordStrs);
                    }
                    catch (Exception e)
                    {
                        PrintErrorMessage($"While reading '{PROPERTY_KEY_OFFSET}' from '{sensorBlock.DisplayNameText}': {e.Message}");
                    }
                }
                else
                {
                    Offset = DEFAULT_OFFSET;
                }

                try
                {
                    Register(this);
                }
                catch (Exception e)
                {
                    PrintErrorMessage(e.Message);
                    return;
                }

                if (!props.ContainsKey(PROPERTY_KEY_TARGET))
                {
                    // One-Way teleporter. This might be a target only.
                    //PrintWarningMessage($"'{PROPERTY_KEY_TARGET}' not set in Custom Data at '{sensorBlock.DisplayNameText}'. It might be a target only.");
                    return;
                }

                Target = props[PROPERTY_KEY_TARGET];

                if (!TeleporterNetwork.ContainsKey(Target) && !firstTime && sensorBlock.IsActive) {
                    PrintErrorMessage($"No teleporter found with the name '{Target}'!");
                }
            }
        }

        private static void Teleport(IMyEntity entity, LogicComponent target)
        {
            Vector3D newPosition = target.Entity.GetPosition() + target.Entity.WorldMatrix.Forward * 3f + target.Offset;
            //entity.SetPosition(newPosition);
            MatrixD newPosMatrix = MatrixD.CreateWorld(newPosition, target.Entity.WorldMatrix.Forward, target.Entity.WorldMatrix.Up);
            entity.Teleport(newPosMatrix);
        }

        private static void Register(LogicComponent teleporter)
        {
            if (!TeleporterNetwork.ContainsKey(teleporter.Name))
            {
                TeleporterNetwork.Add(teleporter.Name, teleporter);
            }
            else if (TeleporterNetwork[teleporter.Name] != teleporter)
            {
                throw new Exception($"There is already a teleporter registered with the name '{teleporter.Name}'!");
            }
        }

        private static Dictionary<string, string> ReadProperties(string source) {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string[] lines = source.Split('\n');
            string[] pair;

            foreach (var line in lines)
            {
                pair = line.Split(new char[1] { '=' }, 2);
                if (pair.Length == 2) {
                    result.Add(pair[0].ToLower(), pair[1]);
                } else {
                    result.Add(pair[0].ToLower(), "");
                }
            }

            return result;
        }

        private static float ParseFloat(string src, string errDetails)
        {
            float result;

            if (!float.TryParse(src, out result))
            {
                throw new Exception(errDetails);
            }

            return result;
        }

        private static Vector3D ParseVector3D(string[] coords)
        {
            // TODO: Be sure it is also handling 0, 1 or 2 coordinates.
            float x = ParseFloat(coords[0].Trim(), "x-coordinate is not a number.");
            float y = ParseFloat(coords[1].Trim(), "y-coordinate is not a number.");
            float z = ParseFloat(coords[2].Trim(), "z-coordinate is not a number.");

            return new Vector3D(x, y, z);
        }

        private static void PrintMessage(string text, int disappearTimeMs = 3000, string fontColor = MyFontEnum.Green)
        {
            MyAPIGateway.Utilities.ShowNotification(text, disappearTimeMs, fontColor);
        }

        private static void PrintErrorMessage(string text, int disappearTimeMs = 3000, string fontColor = MyFontEnum.Red)
        {
            PrintMessage($"ERROR: {text}", disappearTimeMs, fontColor);
        }

        private static void PrintWarningMessage(string text, int disappearTimeMs = 3000, string fontColor = MyFontEnum.DarkBlue)
        {
            PrintMessage($"WARNING: {text}", disappearTimeMs, fontColor);
        }

        private void PrintDebugMessage(string text, int disappearTimeMs = 3000, string fontColor = MyFontEnum.Debug)
        {
            if ((Entity as IMyTerminalBlock).CustomData.Contains($"{PROPERTY_KEY_DEBUG}=true")) {
                PrintMessage($"DEBUG: {text}", disappearTimeMs, fontColor);
            }
        }
    }
}
