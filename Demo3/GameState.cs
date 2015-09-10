using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Web;
using Demo3.Models;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using System.Security.Cryptography;
using System.Text;

namespace Demo3
{
    public class GameState
    {
        private static readonly Lazy<GameState> _instance = new Lazy<GameState>(
            () => new GameState(GlobalHost.ConnectionManager.GetHubContext<GameHub>()));

        private readonly ConcurrentDictionary<string, Player> _players = 
            new ConcurrentDictionary<string, Player>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, Game> _games =
            new ConcurrentDictionary<string, Game>(StringComparer.OrdinalIgnoreCase);

        public GameState(IHubContext context)
        {
            Clients = context.Clients;
            Groups = context.Groups;
        }

        public static GameState Instance
        {
            get { return _instance.Value; }
        }

        public IHubConnectionContext Clients {get;set;} 
        public IGroupManager Groups { get; set; }

        public Player CreatePlayer(string username)
        {
            var player = new Player(username, GetMD5Hash(username));
            _players[username] = player;
            return player;
        }

        private string GetMD5Hash(string username)
        {
            return String.Join("", MD5.Create()
                        .ComputeHash(Encoding.Default.GetBytes(username))
                        .Select(b => b.ToString("x2")));
        }


        public Player GetPlayer(string username)
        {
            return _players.Values.FirstOrDefault(p => p.Name == username);
        }

        public Player GetNewOpponent(Player player)
        {
            return _players.Values.FirstOrDefault(p=> !p.IsPlaying && p.Id != player.Id);
        }

        public Player GetOpponent(Player player, Game game)
        {
            if (game.Player1.Id == player.Id)
            {
                return game.Player2;
            }
            else
            {
                return game.Player1;
            }
        }

        public Game CreateGame(Player player1, Player player2)
        {
            var game = new Game
            {
                Player1 = player1,
                Player2 = player2,
                Board = new Board()
            };

            var group = Guid.NewGuid().ToString("d");
            _games[group] = game;

            player1.IsPlaying = player2.IsPlaying = true;
            player1.Group = player2.Group = group;

            Groups.Add(player1.ConnectionId, group);
            Groups.Add(player2.ConnectionId, group);


            return game;
        }

        public Game FindGame(Player player, out Player opponent)
        {
            opponent = null;
            if (player.Group == null)
            {
                return null;
            }
            Game game;
            _games.TryGetValue(player.Group, out game);
            if (game != null)
            {
                if (player.Id == game.Player1.Id)
                {
                    opponent = game.Player2;
                    return game;
                }
                opponent = game.Player1;
                return game;
            }
            return null;
        }

        public void ResetGame(Game game)
        {
            var groupName = game.Player1.Group;
            var player1Name = game.Player1.Name;
            var player2Name = game.Player2.Name;

            Groups.Remove(game.Player1.ConnectionId, groupName);
            Groups.Remove(game.Player2.ConnectionId, groupName);

            Player p1;
            _players.TryRemove(player1Name, out p1);

            Player p2;
            _players.TryRemove(player2Name, out p2);

            Game g;
            _games.TryRemove(groupName, out g);
        }
    }
}