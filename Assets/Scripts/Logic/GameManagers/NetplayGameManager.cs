﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Common;
using Logic;
using Logic.Characters;
using Network;
using Network.Rollback;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class NetplayGameManager : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PingPackage
    {
        public uint Id;
        public long Created;
        public long Received;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct InputPackage
    {
        public int Frame;
        public InputState State;
    }

    
    private static NetplayGameManager _instance;

    private GameSynchronizer<GameState, InputState> _synchronizer;
    private DiscordLobby _lobby;

    private GameState _gameState;
    private bool _gameStarted;
    
    private PlayerHandle _localPlayer;
    private PlayerHandle _remotePlayer;
    private uint _pingPackageId;

    [SerializeField] private Transform _player1Transform;
    [SerializeField] private Transform _player2Transform;

    private void Awake()
    {
        if (_instance != null)
        {
            throw new Exception($"Only one instance of {nameof(NetplayGameManager)} may exist.");
        }
        
        DontDestroyOnLoad(this);
        _instance = this;
    }

    private void Start()
    {
    }

    public static void JoinMatch(DiscordLobby lobby) => _instance.JoinMatchInternal(lobby);
    private async void JoinMatchInternal(DiscordLobby lobby)
    {
        if (_lobby != null) throw new NotImplementedException();
        
        await lobby.Connect();
        _lobby = lobby;
        _lobby.ConnectNetwork();
        _lobby.NetworkMessageReceived += NetworkMessageReceived;
        
        _synchronizer = new GameSynchronizer<GameState, InputState>();
        _synchronizer.SimulateGame += SimulateGame;
        _synchronizer.SaveGame += SaveGame; 
        _synchronizer.LoadGame += LoadGame;
        _synchronizer.BroadcastInput += BroadcastInput;
        
        _remotePlayer = _synchronizer.AddPlayer(PlayerType.Remote);
        _localPlayer  = _synchronizer.AddPlayer(PlayerType.Local);

        _lobby.SendNetworkMessage(0, Encoding.UTF8.GetBytes("READY"));
    }
    
    public static void CreateMatch(DiscordLobby lobby) => _instance.CreateMatchInternal(lobby);
    private void CreateMatchInternal(DiscordLobby lobby)
    {
        if (_lobby != null) throw new NotImplementedException();

        _lobby = lobby;
        _lobby.ConnectNetwork();
        _lobby.NetworkMessageReceived += NetworkMessageReceived;
        
        _synchronizer = new GameSynchronizer<GameState, InputState>();
        _synchronizer.SimulateGame += SimulateGame;
        _synchronizer.SaveGame += SaveGame; 
        _synchronizer.LoadGame += LoadGame;
        _synchronizer.BroadcastInput += BroadcastInput;
        
        _localPlayer  = _synchronizer.AddPlayer(PlayerType.Local);
        _remotePlayer = _synchronizer.AddPlayer(PlayerType.Remote);
    }

    private void Update()
    {
        if (_lobby == null) return;
        
        PingPlayers();

        if (!_gameStarted) return;
    
        _synchronizer.AddLocalInput(_localPlayer, InputState.ReadLocalInputs());
        _synchronizer.Update(Time.deltaTime * 1000);
        
        _player1Transform.position = new Vector2(_gameState.Player1.Position.x, _gameState.Player1.Position.y);
        _player2Transform.position = new Vector2(_gameState.Player2.Position.x, _gameState.Player2.Position.y);
        
        _gameState.DrawHitboxes();
    }

    private void PingPlayers()
    {
        _lobby.SendNetworkMessage(2, new PingPackage
        {
            Id = _pingPackageId,
            Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }
        .ToBytes());
        
        _pingPackageId++;
    }

    private GameState SaveGame()
    {
        return _gameState;
    }

    private void LoadGame(GameState gameState)
    {
        _gameState = gameState;
    }

    private void SimulateGame(InputState[] inputStates)
    {
        _gameState.Update(inputStates[0], inputStates[1]);
    }
    
    private void BroadcastInput(PlayerHandle player, int frame, InputState state)
    {
        _lobby.SendNetworkMessage(1, new InputPackage {Frame = frame, State = state}.ToBytes());
    }

    private void NetworkMessageReceived(long userId, byte channelId, byte[] data)
    {
        if (channelId == 0)
        {
            if (Encoding.UTF8.GetString(data) == "READY" && _gameStarted == false)
            {
                _lobby.SendNetworkMessage(0, Encoding.UTF8.GetBytes("READY"));
                _gameStarted = true;
            }
        }
        else if (channelId == 1)
        {
            var inputPackage = data.ToStruct<InputPackage>();
            _synchronizer.AddRemoteInput(_remotePlayer, inputPackage.Frame, inputPackage.State);
        }
        else if (channelId == 2)
        {
            var package = data.ToStruct<PingPackage>();
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            if (package.Received == 0)
            {
                package.Received = now;
                _lobby.SendNetworkMessage(userId, 2, package.ToBytes());
            }
            else
            {
                var rtt = now - package.Created;
                _synchronizer.SetPing(_remotePlayer, rtt / 2f);
            }
        }
    }
}
