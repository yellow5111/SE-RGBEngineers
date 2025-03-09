using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using System;
using System.Collections.Generic;

[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
public class RGBEngineers : MySessionComponentBase
{
    private float accumulator = 0f;
    private float updateInterval = 1f;
    private Random rnd = new Random();
    private bool modEnabled = true;

    private const string UpdateIntervalKey = "RGBEngineers_UpdateInterval";
    private const string ModEnabledKey = "RGBEngineers_ModEnabled";

    private const byte ClientToServerMessageId = 123;
    private const byte ServerToClientMessageId = 124;

    public override void LoadData()
    {
        float savedInterval = 0f;
        if (MyAPIGateway.Utilities.GetVariable<float>(UpdateIntervalKey, out savedInterval))
        {
            updateInterval = savedInterval;
        }
        bool savedEnabled = true;
        if (MyAPIGateway.Utilities.GetVariable<bool>(ModEnabledKey, out savedEnabled))
        {
            modEnabled = savedEnabled;
        }

        if (MyAPIGateway.Multiplayer.IsServer)
        {
            MyAPIGateway.Multiplayer.RegisterMessageHandler(ClientToServerMessageId, OnClientToServerMessage);
        }
        MyAPIGateway.Multiplayer.RegisterMessageHandler(ServerToClientMessageId, OnServerToClientMessage);
        MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
    }

    public override void UpdateAfterSimulation()
    {
        if (!modEnabled)
            return;

        float dt = 1f / 4.5f;
        accumulator += dt;
        if (accumulator >= updateInterval)
        {
            accumulator -= updateInterval;
            var player = MyAPIGateway.Session != null ? MyAPIGateway.Session.Player : null;
            if (player != null && player.Character != null)
            {
                Color randomColor = new Color(rnd.Next(256), rnd.Next(256), rnd.Next(256));
                long identityId = player.IdentityId;
                int colorPacked = (int)randomColor.PackedValue;
                byte[] data = new byte[12];
                Buffer.BlockCopy(BitConverter.GetBytes(identityId), 0, data, 0, 8);
                Buffer.BlockCopy(BitConverter.GetBytes(colorPacked), 0, data, 8, 4);

                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    UpdateCharacterSuitColor(player.Character, randomColor);
                    MyAPIGateway.Multiplayer.SendMessageToOthers(ServerToClientMessageId, data);
                }
                else
                {
                    MyAPIGateway.Multiplayer.SendMessageToServer(ClientToServerMessageId, data);
                }
            }
        }
    }

    private void OnMessageEntered(string messageText, ref bool sendToOthers)
    {
        if (messageText.StartsWith("/rgbengineers", StringComparison.InvariantCultureIgnoreCase))
        {
            string commandBody = messageText.Substring("/rgbengineers".Length).Trim();
            string[] tokens = commandBody.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 1)
            {
                if (tokens[0].Equals("speed", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (tokens.Length >= 2)
                    {
                        float newInterval;
                        if (float.TryParse(tokens[1], out newInterval) && newInterval > 0)
                        {
                            updateInterval = newInterval;
                            MyAPIGateway.Utilities.SetVariable<float>(UpdateIntervalKey, updateInterval);
                            MyAPIGateway.Utilities.ShowMessage("RGB Engineers", "Update interval set to: " + newInterval.ToString("0.00") + " sec");
                        }
                        else
                        {
                            MyAPIGateway.Utilities.ShowMessage("RGB Engineers", "Usage: /rgbengineers speed <seconds> (must be > 0)");
                        }
                    }
                    else
                    {
                        MyAPIGateway.Utilities.ShowMessage("RGB Engineers", "Usage: /rgbengineers speed <seconds>");
                    }
                }
                else if (tokens[0].Equals("toggle", StringComparison.InvariantCultureIgnoreCase))
                {
                    modEnabled = !modEnabled;
                    MyAPIGateway.Utilities.SetVariable<bool>(ModEnabledKey, modEnabled);
                    string status = modEnabled ? "enabled" : "disabled";
                    MyAPIGateway.Utilities.ShowMessage("RGB Engineers", "RGB Engineers is now " + status);

                    if (!modEnabled)
                    {
                        var player = MyAPIGateway.Session != null ? MyAPIGateway.Session.Player : null;
                        if (player != null && player.Character != null)
                        {
                            Color defaultColor = Color.White;
                            UpdateCharacterSuitColor(player.Character, defaultColor);

                            long identityId = player.IdentityId;
                            int colorPacked = (int)defaultColor.PackedValue;
                            byte[] data = new byte[12];
                            Buffer.BlockCopy(BitConverter.GetBytes(identityId), 0, data, 0, 8);
                            Buffer.BlockCopy(BitConverter.GetBytes(colorPacked), 0, data, 8, 4);

                            if (MyAPIGateway.Multiplayer.IsServer)
                            {
                                MyAPIGateway.Multiplayer.SendMessageToOthers(ServerToClientMessageId, data);
                            }
                            else
                            {
                                MyAPIGateway.Multiplayer.SendMessageToServer(ClientToServerMessageId, data);
                            }
                        }
                    }
                }
            }
            sendToOthers = false;
        }
    }

    private void OnClientToServerMessage(byte[] data)
    {
        if (data == null || data.Length < 12)
            return;
        try
        {
            long identityId = BitConverter.ToInt64(data, 0);
            int colorPacked = BitConverter.ToInt32(data, 8);
            Color color = new Color();
            color.PackedValue = (uint)colorPacked;
            IMyPlayer target = GetPlayerById(identityId);
            if (target != null && target.Character != null)
            {
                UpdateCharacterSuitColor(target.Character, color);
                MyAPIGateway.Multiplayer.SendMessageToOthers(ServerToClientMessageId, data);
            }
        }
        catch (Exception ex)
        {
            MyAPIGateway.Utilities.ShowMessage("RGB Engineers Error", ex.Message);
        }
    }

    private void OnServerToClientMessage(byte[] data)
    {
        if (data == null || data.Length < 12)
            return;
        try
        {
            long identityId = BitConverter.ToInt64(data, 0);
            int colorPacked = BitConverter.ToInt32(data, 8);
            Color color = new Color();
            color.PackedValue = (uint)colorPacked;
            IMyPlayer target = GetPlayerById(identityId);
            if (target != null && target.Character != null)
            {
                UpdateCharacterSuitColor(target.Character, color);
            }
        }
        catch (Exception ex)
        {
            MyAPIGateway.Utilities.ShowMessage("RGB Engineers Error", ex.Message);
        }
    }

    private IMyPlayer GetPlayerById(long identityId)
    {
        var players = new List<IMyPlayer>();
        MyAPIGateway.Players.GetPlayers(players);
        foreach (var player in players)
        {
            if (player.IdentityId == identityId)
                return player;
        }
        return null;
    }

    private void UpdateCharacterSuitColor(IMyCharacter character, Color color)
    {
        string[] parts = new string[]
        {
            "Emissive", "Gear", "Backpack", "Cloth", "Arms", "Spacesuit_hood",
            "Head", "Astronaut_head", "Astronaut_skull", "Bag", "RightArm",
            "RightGlove", "LeftGlove", "Boots"
        };

        foreach (var part in parts)
        {
            character.SetEmissiveParts(part, color, 50000.5f);
        }
    }

    protected override void UnloadData()
    {
        if (MyAPIGateway.Multiplayer != null)
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(ClientToServerMessageId, OnClientToServerMessage);
            }
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(ServerToClientMessageId, OnServerToClientMessage);
        }
        MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;
    }
}