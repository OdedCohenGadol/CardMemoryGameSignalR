using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using Demo3.Models;

namespace Demo3
{
    public class GameHub : Hub
    {
        public bool join(string username)
        {
            var player = GameState.Instance.GetPlayer(username);
            if (player != null)
            {
                Clients.Caller.playerExists();
                return true;
            }

            //player is new' add him
            player = GameState.Instance.CreatePlayer(username);
            player.ConnectionId = Context.ConnectionId;
            Clients.Caller.name = player.Name;
            Clients.Caller.hash = player.Hash;
            Clients.Caller.id = player.Id;

            Clients.Caller.playerJoined(player);

            return StartGame(player);
        }

        private bool StartGame(Player player)
        {
            if (player != null)
            {
                Player player2;
                var game = GameState.Instance.FindGame(player, out player2);
                if (game != null)
                {
                    Clients.Group(player.Group).buildBoard(game);
                    return true;
                }

                //no game founded -- create new
                player2 = GameState.Instance.GetNewOpponent(player);
                if (player2 == null)
                {
                    //no one looking for play, sit and wait
                    Clients.Caller.waitingList();
                    return true;
                }

                game = GameState.Instance.CreateGame(player, player2);
                game.WhosTurn = player.Id;

                Clients.Group(player.Group).buildBoard(game);
                return true;
            }
            return false;
        }

        public bool Flip(string cardName)
        {
            var username = Clients.Caller.name;
            var player = GameState.Instance.GetPlayer(username);
            if (player != null)
            {
                Player playerOpponent;
                var game = GameState.Instance.FindGame(player, out playerOpponent);
                if (game != null)
                {
                    if (!string.IsNullOrEmpty(game.WhosTurn) && game.WhosTurn != player.id )
                    {
                        return true;
                    }

                    var card = FindCard(game, cardName);
                    Clients.Group(player.group).flipCard(card);
                    return true;
                }
            }
            return false;
        }

        private Card FindCard(Game game, string cardName)
        {
            return game.Board.Pieces.FirstOrDefault(p => p.Name == cardName);
        }

        public bool CheckCard(string cardName)
        {
            var username = Clients.Caller.name;
            Player player = GameState.Instance.GetPlayer(username);
            if (player != null)
            {
                Player playerOpponent;
                var game = GameState.Instance.FindGame(player, out playerOpponent);
                if (game != null)
                {
                    if (!string.IsNullOrEmpty(game.WhosTurn) && game.WhosTurn != player.Id)
                    {
                        return true;
                    }
                    Card card = FindCard(game, cardName);
                    if (game.LastCard == null)
                    {
                        game.WhosTurn = player.Id;
                        game.LastCard = card;
                        return true;
                    }

                    //second flip
                    bool isMatch = IsMatch(game, card);
                    if (isMatch)
                    {
                        StoreMatch(player, card);
                        game.LastCard = null;
                        Clients.Group(player.Group).showMatch(card, username);

                        if (player.Matches.Count >= 16)
                        {
                            Clients.Group(player.Group).winner(card, username);
                            GameState.Instance.ResetGame(game);
                            return true;
                        }
                        return true;
                    }

                    Player opponent = GameState.Instance.GetOpponent(player,game);
                    //shift turn
                    game.WhosTurn = opponent.Id;

                    Clients.Group(player.Group).resetFlip(game.LastCard, card);
                    game.LastCard = null;
                    return true;
                }
            }
            return false;
        }

        private void StoreMatch(Player player, Card card)
        {
            player.Matches.Add(card.Id);
            player.Matches.Add(card.Pair);
        }

        private bool IsMatch(Game game, Card card)
        {
            if (card == null)
            {
                return false;
            }
            if (game.LastCard != null)
            {
                //two cards have picked, lets check them
                if (game.LastCard.Pair == card.Id)
                {
                    //we have a match
                    return true;
                }
                return false;
            }
            return false;
        }
    }
}