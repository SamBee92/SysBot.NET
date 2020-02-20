﻿using System.Collections.Generic;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public class LedyDistributor<T> where T : PKM, new()
    {
        public readonly Dictionary<string, LedyRequest<T>> UserRequests = new Dictionary<string, LedyRequest<T>>();
        public readonly Dictionary<string, LedyRequest<T>> Distribution = new Dictionary<string, LedyRequest<T>>();
        public readonly PokemonPool<T> Pool;

        public LedyDistributor(PokemonPool<T> pool) => Pool = pool;

        private const int NoMatchSpecies = -1;

        public LedyResponse<T> GetResponse(T pk, int speciesMatch = -1)
        {
            if (speciesMatch != NoMatchSpecies && pk.Species != speciesMatch)
                return GetRandomResponse();

            var nick = pk.Nickname;
            if (UserRequests.TryGetValue(nick, out var match))
                return new LedyResponse<T>(match.RequestInfo.Receive, LedyResponseType.MatchRequest);
            if (Distribution.TryGetValue(nick, out match))
                return new LedyResponse<T>(match.RequestInfo.Receive, LedyResponseType.MatchPool);

            return GetRandomResponse();
        }

        private LedyResponse<T> GetRandomResponse()
        {
            var gift = Pool.GetRandomPoke();
            return new LedyResponse<T>(gift, LedyResponseType.Random);
        }
    }
}